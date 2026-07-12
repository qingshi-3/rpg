using System.Collections.Generic;
using System.Linq;
using Rpg.Application.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicBattleEntryRollbackService
{
    private readonly StrategicManagementCommandService _strategicCommands;
    private readonly WorldArmyCommandService _armyCommands;

    public StrategicBattleEntryRollbackService(
        StrategicManagementCommandService strategicCommands,
        WorldArmyCommandService armyCommands)
    {
        _strategicCommands = strategicCommands;
        _armyCommands = armyCommands;
    }

    public StrategicBattleEntryRollbackResult Rollback(
        StrategicManagementState state,
        IDictionary<string, WorldArmyState> armies,
        string armyId,
        string expeditionId,
        string reason)
    {
        string cancellationFailure = _strategicCommands?.GetExpeditionCancellationFailureReason(state, expeditionId)
                                     ?? StrategicFailureReasons.InvalidExpeditionRollbackPlan;
        if (!string.IsNullOrWhiteSpace(cancellationFailure))
        {
            return StrategicBattleEntryRollbackResult.Failed(cancellationFailure);
        }

        WorldArmyState carrier = null;
        if (armies != null && !string.IsNullOrWhiteSpace(armyId))
        {
            armies.TryGetValue(armyId, out carrier);
        }

        List<WorldArmyState> expeditionCarriers = (armies?.Values ?? System.Array.Empty<WorldArmyState>())
            .Where(item => item != null && string.Equals(item.StrategicExpeditionId ?? "", expeditionId ?? "", System.StringComparison.Ordinal))
            .ToList();
        if ((carrier != null && !string.Equals(carrier.StrategicExpeditionId ?? "", expeditionId ?? "", System.StringComparison.Ordinal)) ||
            expeditionCarriers.Count > 1 ||
            (carrier == null && expeditionCarriers.Count == 1 && !string.IsNullOrWhiteSpace(armyId)))
        {
            return StrategicBattleEntryRollbackResult.Failed("army_expedition_mismatch");
        }

        carrier ??= expeditionCarriers.SingleOrDefault();

        // Both plans are validated before Strategic Management or its legacy carrier is mutated.
        StrategicCommandResult cancellation = _strategicCommands.CancelExpedition(state, expeditionId, reason);
        if (!cancellation.Success)
        {
            return StrategicBattleEntryRollbackResult.Failed(cancellation.FailureReason);
        }

        if (armies != null && carrier != null)
        {
            WorldArmyCommandResult carrierRemoval = _armyCommands.RemoveResolvedStrategicExpeditionCarrier(
                armies,
                carrier.ArmyId,
                expeditionId,
                reason);
            if (!carrierRemoval.Success)
            {
                // This indicates a violated prevalidated invariant; never hide the partial rollback.
                GameLog.Error(
                    nameof(StrategicBattleEntryRollbackService),
                    $"StrategicBattleEntryRollbackCarrierInvariantFailed army={carrier.ArmyId} expedition={expeditionId ?? ""} reason={carrierRemoval.FailureReason}");
                throw new System.InvalidOperationException(carrierRemoval.FailureReason);
            }
        }

        return StrategicBattleEntryRollbackResult.Ok(cancellation);
    }
}

public sealed class StrategicBattleEntryRollbackResult
{
    public bool Success { get; private set; }
    public string FailureReason { get; private set; } = "";
    public StrategicCommandResult StrategicResult { get; private set; }

    public static StrategicBattleEntryRollbackResult Ok(StrategicCommandResult result) => new()
    {
        Success = true,
        StrategicResult = result
    };

    public static StrategicBattleEntryRollbackResult Failed(string failureReason) => new()
    {
        FailureReason = failureReason ?? ""
    };
}

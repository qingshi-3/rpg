using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Application.Battle.Commands;

public sealed class BattleCommandSubmissionResult
{
    public bool Accepted { get; init; }
    public CommandRejectionStage RejectionStage { get; init; }
    public string ReasonCode { get; init; } = "";
    public IReadOnlyList<BattleEvent> Events { get; init; } = System.Array.Empty<BattleEvent>();
}

public sealed class BattleCommandSubmissionService
{
    private readonly BattleCommandApplicationValidator _validator = new();

    public BattleCommandSubmissionResult Submit(
        BattleStartSnapshot snapshot,
        string playerFactionId,
        CommandRequest request,
        BattleRuntimeSessionController runtimeController)
    {
        CommandValidationResult validation = _validator.Validate(request, snapshot, playerFactionId);
        if (!validation.Accepted)
        {
            // Application rejection is intentionally event-free: Runtime has not
            // accepted the intent and must not observe or mutate battle state.
            GameLog.Info(
                nameof(BattleCommandSubmissionService),
                $"BattleCommandApplicationRejected battle={request?.BattleId ?? ""} command={request?.CommandId ?? ""} group={request?.BattleGroupId ?? ""} reason={validation.ReasonCode}");
            return Reject(CommandRejectionStage.Application, validation.ReasonCode);
        }

        if (runtimeController == null)
        {
            return Reject(CommandRejectionStage.Application, "runtime_unavailable");
        }

        BattleRuntimeCommandSubmitResult runtimeResult = runtimeController.SubmitCommand(request);
        return new BattleCommandSubmissionResult
        {
            Accepted = runtimeResult?.Accepted == true,
            RejectionStage = runtimeResult?.Accepted == true
                ? CommandRejectionStage.None
                : CommandRejectionStage.Runtime,
            ReasonCode = runtimeResult?.ReasonCode ?? "runtime_unavailable",
            Events = runtimeResult?.Events ?? System.Array.Empty<BattleEvent>()
        };
    }

    private static BattleCommandSubmissionResult Reject(CommandRejectionStage stage, string reasonCode)
    {
        return new BattleCommandSubmissionResult
        {
            Accepted = false,
            RejectionStage = stage,
            ReasonCode = reasonCode ?? "",
            Events = System.Array.Empty<BattleEvent>()
        };
    }
}

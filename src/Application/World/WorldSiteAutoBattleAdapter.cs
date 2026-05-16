using Rpg.Application.Battle;
using Rpg.Application.Battle.Auto;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteAutoBattleResolveResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleStartRequest Request { get; init; }
    public BattleResult BattleResult { get; init; }
    public AutoBattleReport Report { get; init; }
    public AutoBattleRuntimeController RuntimeController { get; init; }
}

public sealed class WorldSiteAutoBattleAdapter
{
    private readonly AutoBattleRuntimeController _runtimeController;

    public WorldSiteAutoBattleAdapter(AutoBattleRuntimeController runtimeController = null)
    {
        _runtimeController = runtimeController ?? new AutoBattleRuntimeController();
    }

    public bool TryResolveActiveBattle(out WorldSiteAutoBattleResolveResult result)
    {
        if (!_runtimeController.StartActiveBattle(out string failureReason))
        {
            result = new WorldSiteAutoBattleResolveResult
            {
                Success = false,
                FailureReason = failureReason ?? "",
                RuntimeController = _runtimeController
            };
            GameLog.Warn(nameof(WorldSiteAutoBattleAdapter), $"AutoBattleResolveFailed reason={result.FailureReason}");
            return false;
        }

        if (!BattleSessionHandoff.TryConsumeLastBattleResult(
                out BattleStartRequest request,
                out BattleResult battleResult) ||
            request == null ||
            battleResult == null ||
            _runtimeController.Report == null)
        {
            result = new WorldSiteAutoBattleResolveResult
            {
                Success = false,
                FailureReason = "auto_battle_result_missing",
                RuntimeController = _runtimeController
            };
            GameLog.Warn(nameof(WorldSiteAutoBattleAdapter), "AutoBattleResolveFailed reason=auto_battle_result_missing");
            return false;
        }

        result = new WorldSiteAutoBattleResolveResult
        {
            Success = true,
            Request = request,
            BattleResult = battleResult,
            Report = _runtimeController.Report,
            RuntimeController = _runtimeController
        };
        GameLog.Info(
            nameof(WorldSiteAutoBattleAdapter),
            $"AutoBattleResolved request={request.RequestId} outcome={battleResult.Outcome} forces={battleResult.ForceResults.Count} reportEvents={_runtimeController.Report.EventFeed.Count}");
        return true;
    }
}

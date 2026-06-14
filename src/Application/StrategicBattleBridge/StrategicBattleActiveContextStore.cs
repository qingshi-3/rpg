using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicBattleBridge;

public static class StrategicBattleActiveContextStore
{
    private static StrategicBattleActiveContext _activeContext;

    public static bool HasActiveContext => _activeContext != null;

    public static void Begin(StrategicBattleActiveContext context)
    {
        _activeContext = context;
        GameLog.Info(
            nameof(StrategicBattleActiveContextStore),
            $"StrategicBattleActiveContextBegin context={context?.ContextId ?? ""} expedition={context?.Session?.ExpeditionId ?? ""} target={context?.Session?.TargetLocationId ?? ""}");
    }

    public static bool TryPeek(out StrategicBattleActiveContext context)
    {
        context = _activeContext;
        return context != null;
    }

    public static void Clear(string reason = "")
    {
        if (_activeContext != null)
        {
            GameLog.Info(
                nameof(StrategicBattleActiveContextStore),
                $"StrategicBattleActiveContextClear context={_activeContext.ContextId} reason={reason ?? ""}");
        }

        _activeContext = null;
    }
}

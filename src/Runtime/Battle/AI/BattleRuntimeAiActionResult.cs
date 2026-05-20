namespace Rpg.Runtime.Battle.AI;

public sealed class BattleRuntimeAiActionResult
{
    private BattleRuntimeAiActionResult(
        BattleRuntimeAiActionRequest request,
        bool success,
        string status,
        string failureReason)
    {
        Request = request;
        Success = success;
        Status = status ?? "";
        FailureReason = failureReason ?? "";
    }

    public BattleRuntimeAiActionRequest Request { get; }
    public bool Success { get; }
    public string Status { get; }
    public string FailureReason { get; }

    public static BattleRuntimeAiActionResult Succeeded(BattleRuntimeAiActionRequest request, string status)
    {
        return new BattleRuntimeAiActionResult(request, true, status, "");
    }

    public static BattleRuntimeAiActionResult Failed(BattleRuntimeAiActionRequest request, string failureReason)
    {
        return new BattleRuntimeAiActionResult(request, false, "failed", failureReason);
    }
}

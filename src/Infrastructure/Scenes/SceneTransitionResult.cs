using Godot;

namespace Rpg.Infrastructure.Scenes;

public sealed class SceneTransitionResult
{
    private SceneTransitionResult(bool success, string failureReason, Error error)
    {
        Success = success;
        FailureReason = failureReason ?? "";
        Error = error;
    }

    public bool Success { get; }
    public string FailureReason { get; }
    public Error Error { get; }

    public static SceneTransitionResult Ok()
    {
        return new SceneTransitionResult(true, "", Error.Ok);
    }

    public static SceneTransitionResult Fail(string failureReason, Error error = Error.Failed)
    {
        return new SceneTransitionResult(false, failureReason, error);
    }
}

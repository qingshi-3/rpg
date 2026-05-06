namespace Rpg.Application.Emotion;

public sealed class EmotionOperationResult<T>
{
    private EmotionOperationResult(bool success, T value, string error)
    {
        Success = success;
        Value = value;
        Error = error ?? "";
    }

    public bool Success { get; }
    public T Value { get; }
    public string Error { get; }

    public static EmotionOperationResult<T> Ok(T value)
    {
        return new EmotionOperationResult<T>(true, value, "");
    }

    public static EmotionOperationResult<T> Fail(string error)
    {
        return new EmotionOperationResult<T>(false, default, error);
    }
}

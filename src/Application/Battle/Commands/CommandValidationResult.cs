namespace Rpg.Application.Battle.Commands;

public enum CommandRejectionStage
{
    None = 0,
    UiHint = 1,
    Application = 2,
    Runtime = 3
}

public sealed class CommandValidationResult
{
    public bool Accepted { get; init; }
    public CommandRejectionStage RejectionStage { get; init; }
    public string ReasonCode { get; init; } = "";

    public static CommandValidationResult Accept()
    {
        return new CommandValidationResult { Accepted = true };
    }

    public static CommandValidationResult Reject(CommandRejectionStage stage, string reasonCode)
    {
        return new CommandValidationResult
        {
            Accepted = false,
            RejectionStage = stage,
            ReasonCode = reasonCode ?? ""
        };
    }
}

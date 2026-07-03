namespace DisplayDeck.Core.Models;

public enum ChangeStatus
{
    Success,
    NeedsRestart,
    BadMode,
    Failed,
    InvalidParameters,
}

/// <summary>Outcome of an attempt to change display settings.</summary>
public sealed record ChangeResult(ChangeStatus Status, string Message)
{
    public bool IsSuccess => Status is ChangeStatus.Success or ChangeStatus.NeedsRestart;

    public static ChangeResult Success { get; } = new(ChangeStatus.Success, "Applied successfully.");
}

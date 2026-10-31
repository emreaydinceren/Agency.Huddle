namespace Agency.Huddle.App.Tasks;

/// <summary>What an Ask on a Proof item did.</summary>
/// <param name="Succeeded">Whether the text was posted.</param>
/// <param name="RoomName">The display name of the Room it was posted in, or <see langword="null"/> on failure.</param>
internal sealed record AskOutcome(bool Succeeded, string? RoomName)
{
    /// <summary>The outcome of an Ask that posted nothing.</summary>
    public static AskOutcome Failure { get; } = new(false, null);
}

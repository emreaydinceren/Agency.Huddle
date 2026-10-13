namespace Agency.Huddle.Contracts;

/// <summary>A Message in a Room's transcript and on the wire.</summary>
public sealed record ChatMessage(
    string Id, DateTimeOffset Timestamp, string SenderId, string SenderName, string Text)
{
    /// <summary>The Message's references at send time (spec §8), or null when it had none.</summary>
    public IReadOnlyList<MessageReference>? References { get; init; }
}

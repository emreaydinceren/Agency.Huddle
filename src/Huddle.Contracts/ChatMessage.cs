namespace Agency.Huddle.Contracts;

public sealed record ChatMessage(
    string Id, DateTimeOffset Timestamp, string SenderId, string SenderName, string Text);
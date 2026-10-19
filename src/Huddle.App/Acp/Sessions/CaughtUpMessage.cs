namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>One earlier Message the Agent was not Mentioned in, carried as catch-up context only.</summary>
/// <param name="SenderName">Who sent it.</param>
/// <param name="Text">What it said.</param>
internal sealed record CaughtUpMessage(string SenderName, string Text);

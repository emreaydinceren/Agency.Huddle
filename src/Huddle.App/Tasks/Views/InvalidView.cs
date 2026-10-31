namespace Agency.Huddle.App.Tasks.Views;

/// <summary>A View entry that failed to load, kept so a hand edit doesn't make it disappear silently.</summary>
public sealed record InvalidView(string? Id, string? Name, IReadOnlyList<string> Problems);

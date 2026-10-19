namespace Agency.Huddle.App.Tasks.Views;

/// <summary>A Team, and optionally a Project within it. A null Project matches Tasks directly under the Team ("No project").</summary>
public sealed record ProjectRef(string Team, string? Project);

namespace Agency.Huddle.App.Tasks;

/// <summary>Where a Task lives. Project is null for a Task directly under its Team.</summary>
public sealed record TaskLocation(string Team, string? Project, bool Closed);

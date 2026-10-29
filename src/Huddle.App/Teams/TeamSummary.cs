namespace Agency.Huddle.App.Teams;

/// <summary>One Team as the pages see it. Names compare ignoring case; Name is the display spelling.</summary>
public sealed record TeamSummary(
    string Name,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Members,
    bool HasFolder)
{
    /// <summary>True when at least one Persona carries this Team's label.</summary>
    public bool HasMembers => this.Members.Count > 0;

    /// <summary>The team type and version from <c>_team.md</c>, or <see langword="null"/> when the Team has no team file.</summary>
    public VersionedRef? TeamType { get; init; }

    /// <summary>The manager Agent named in <c>_team.md</c>, or <see langword="null"/>.</summary>
    public string? Manager { get; init; }

    /// <summary>The workspace folder named in <c>_team.md</c>, or <see langword="null"/>.</summary>
    public string? Workspace { get; init; }

    /// <summary>The one-line statement of what the Team owns, from <c>_team.md</c>, or <see langword="null"/>.</summary>
    public string? Owns { get; init; }
}

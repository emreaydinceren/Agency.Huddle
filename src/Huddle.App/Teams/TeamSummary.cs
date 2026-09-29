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
}

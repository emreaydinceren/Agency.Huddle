namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Teams</c>, per Spec §7. Shared with the Tasks feature: <see cref="Dir"/> is also the Tasks scan root (Library Task G1.2).</summary>
public sealed class TeamsOptions
{
    /// <summary>Team folders, relative to <c>DataDir</c>. Also the Tasks scan root, replacing the retired <c>Tasks:Dir</c>.</summary>
    public string Dir { get; set; } = "Teams";

    /// <summary>The most Team Memory lines in one session's system prompt, across all of a Persona's Teams and Projects; the rest are counted. Zero or less lists none.</summary>
    public int MaxMemoryEntries { get; set; } = 50;
}

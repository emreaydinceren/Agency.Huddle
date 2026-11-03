namespace Agency.Huddle.Seeder.Model;

/// <summary>A Team folder and the Project folders directly inside it. Seeder spec §5; a typed Team follows SEED-HOME-8.</summary>
/// <param name="Name">The Team name.</param>
/// <param name="Projects">The Project names; empty for a Team with no Projects.</param>
internal sealed record SeedTeam(string Name, IReadOnlyList<string> Projects)
{
    /// <summary>
    /// The built-in team type the Team was "hired" from, written <c>id@version</c> such as <c>household@1.0.0</c>, or
    /// <see langword="null"/> for a blank Team. A typed Team also gets the type's default process as <c>AGENTS.md</c> and <c>_team.md</c>.
    /// </summary>
    public string? TeamType { get; init; }

    /// <summary>The manager Agent's Name, or <see langword="null"/>; only a typed Team has one.</summary>
    public string? Manager { get; init; }

    /// <summary>The one-line "What it's for" shown on the Team page's Details tab, or <see langword="null"/>.</summary>
    public string? Owns { get; init; }
}

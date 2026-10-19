namespace Agency.Huddle.Seeder.Model;

/// <summary>A Team folder and the Project folders directly inside it.</summary>
/// <param name="Name">The Team name.</param>
/// <param name="Projects">The Project names; empty for a Team with no Projects.</param>
internal sealed record SeedTeam(string Name, IReadOnlyList<string> Projects);

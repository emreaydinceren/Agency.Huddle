namespace Agency.Huddle.Seeder.Model;

/// <summary>Where a Task lives: a Team, and optionally one of its Projects.</summary>
/// <param name="Team">The Team name.</param>
/// <param name="Project">The Project name, or <see langword="null"/> for the Team root.</param>
internal sealed record SeedLocation(string Team, string? Project)
{
    /// <summary>The display path, <c>Team</c> or <c>Team/Project</c>.</summary>
    internal string Path => this.Project is null ? this.Team : $"{this.Team}/{this.Project}";
}

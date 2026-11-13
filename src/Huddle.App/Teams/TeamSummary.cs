using Agency.Huddle.App.Avatars;

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

    /// <summary>The Team's avatar from <c>_team.md</c>, or <see cref="Avatar.None"/> when it has none.</summary>
    public Avatar Avatar { get; init; } = Avatar.None;

    /// <summary>
    /// The avatars of the Projects that have one, by Project name compared ignoring case, from each Project folder's <c>_project.md</c>
    /// (ADR-0083). A Project with no entry draws the initials of its name; read through <see cref="ProjectAvatar"/>.
    /// </summary>
    public IReadOnlyDictionary<string, Avatar> ProjectAvatars { get; init; } = new Dictionary<string, Avatar>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The avatar of <paramref name="project"/>, or <see cref="Avatar.None"/> when it has none.</summary>
    /// <param name="project">The Project's name, matched ignoring case.</param>
    /// <returns>The Project's avatar.</returns>
    public Avatar ProjectAvatar(string project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return this.ProjectAvatars.GetValueOrDefault(project) ?? Avatar.None;
    }
}

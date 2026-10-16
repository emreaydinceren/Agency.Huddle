using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Components;

/// <summary>
/// Everything <see cref="Shared.ProfileCard"/> draws about one Teammate, independent of where it came from:
/// Build a team maps its private draft to this, and the avatar profile popover maps a saved Teammate.
/// </summary>
/// <param name="Name">The Teammate's name.</param>
/// <param name="Title">The Teammate's title.</param>
/// <param name="Description">What the Teammate does.</param>
/// <param name="Avatar">The Teammate's avatar.</param>
public sealed record TeammateProfile(string Name, string Title, string Description, Avatar Avatar)
{
    /// <summary>The Persona's state for the status dot, or <see langword="null"/> to draw no dot.</summary>
    public PersonaState? State { get; init; }

    /// <summary>The model tier for the tier chip, or <see langword="null"/> to draw no chip.</summary>
    public ModelTier? Tier { get; init; }

    /// <summary>The names of the Teammate's skills; drives the skills chip and its hover list.</summary>
    public IReadOnlyList<string> SkillNames { get; init; } = [];

    /// <summary>Why the Teammate cannot work yet; drives the "Needs setup" line.</summary>
    public IReadOnlyList<string> SetupReasons { get; init; } = [];

    /// <summary>An unsaved avatar image, passed through to the avatar, or <see langword="null"/>.</summary>
    public byte[]? PendingImage { get; init; }

    /// <summary>The file extension of <see cref="PendingImage"/>, or <see langword="null"/>.</summary>
    public string? PendingImageExtension { get; init; }
}

using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.App.Teams;

/// <summary>One Agent Build a team has drafted: written as is, not resolved from a team type's candidate file.</summary>
/// <param name="Name">The Agent's name.</param>
/// <param name="Title">The job title.</param>
/// <param name="Text">The persona text, which becomes the Agent's system prompt.</param>
/// <param name="Role">The role of the team type this Agent fills, or <see langword="null"/> for an Agent outside the type's roles.</param>
/// <param name="Avatar">The avatar to save (its colour and label); an <paramref name="Image"/> is saved with it.</param>
/// <param name="Image">The bytes of the avatar image, or <see langword="null"/>.</param>
/// <param name="ImageExtension">The extension, with its dot, of <paramref name="Image"/>.</param>
/// <param name="Adapter">The adapter the Agent runs on, or <see langword="null"/> for the default.</param>
/// <param name="Model">The model, or <see langword="null"/> for the adapter's default.</param>
/// <param name="Effort">The effort, or <see langword="null"/> for the default.</param>
/// <param name="Skills">The names of the Skills the Agent carries; they must be in <c>Skills/</c> (see <see cref="HireService.EnsureSkillsAsync"/>).</param>
/// <param name="Packs">The skill packs the Agent was drawn from.</param>
internal sealed record DraftHire(
    string Name,
    string Title,
    string Text,
    RoleRef? Role,
    Avatar Avatar,
    byte[]? Image,
    string? ImageExtension,
    string? Adapter = null,
    string? Model = null,
    string? Effort = null,
    IReadOnlyList<string>? Skills = null,
    IReadOnlyList<VersionedRef>? Packs = null)
{
    /// <summary>The names of the Skills the Agent carries; empty when omitted.</summary>
    public IReadOnlyList<string> Skills { get; init; } = Skills ?? [];

    /// <summary>The skill packs the Agent was drawn from; empty when omitted.</summary>
    public IReadOnlyList<VersionedRef> Packs { get; init; } = Packs ?? [];
}

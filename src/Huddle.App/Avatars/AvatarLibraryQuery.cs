namespace Agency.Huddle.App.Avatars;

/// <summary>
/// The picker's current filter state. An avatar passes only when it satisfies every control, as
/// <c>AvatarCatalog.Search</c> applies them.
/// </summary>
/// <param name="Skin">The skin group to keep, or null for All.</param>
/// <param name="Text">The free-text box. Each whitespace-separated word must prefix a token of the profession or a tag; empty passes everyone.</param>
/// <param name="Professions">The professions to keep (OR), compared exactly and ignoring case. Empty means every profession.</param>
/// <param name="Tags">The tags an avatar must all carry (AND), compared exactly and ignoring case.</param>
internal sealed record AvatarLibraryQuery(SkinGroup? Skin, string Text, IReadOnlySet<string> Professions, IReadOnlySet<string> Tags)
{
    /// <summary>The query that matches every avatar: All skin groups, empty text, and no profession or tag selected.</summary>
    internal static AvatarLibraryQuery Empty { get; } = new(
        null,
        string.Empty,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

namespace Agency.Huddle.App.Library;

/// <summary>Per-kind facts about a Library root.</summary>
internal static class LibraryRoots
{
    /// <summary>The document a root opens on when there is no remembered selection: <c>SKILLS.md</c> for the Skill Sources
    /// root (Skill Packs spec §6.11.4), otherwise none, which keeps the explorer's own behaviour.</summary>
    public static string? DefaultDocument(LibraryRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root.Kind == LibraryRootKind.SkillSources ? "SKILLS.md" : null;
    }
}
namespace Agency.Huddle.App.Library;

/// <summary>The kind of Library Root: a Team folder collection, Agent folder collection, or pinned root.</summary>
public enum LibraryRootKind
{
    /// <summary>Team folders.</summary>
    Teams,

    /// <summary>Agent folders.</summary>
    Agents,

    /// <summary>Pinned root.</summary>
    Pinned,

    /// <summary>The Uploads root, where chat uploads are saved; a plain folder tree that behaves like a pinned root for protection.</summary>
    Uploads,

    /// <summary>The Skill Sources folder: SKILLS.md, Pack and catalog files and Source notes, read only (Skill Packs spec §6.11).</summary>
    SkillSources,
}

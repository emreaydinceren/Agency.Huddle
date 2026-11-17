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

    /// <summary>The Raw root, where files brought into the Team are kept as they arrived; a plain folder tree that behaves like a pinned root for protection.</summary>
    Raw,

    /// <summary>The Skill Sources folder: SKILLS.md, Pack and catalog files and Source notes, read only (Skill Packs spec §6.11).</summary>
    SkillSources,

    /// <summary>The Archive root, where put-away Teams and Projects are kept; read only.</summary>
    Archive,

    /// <summary>The Prompt override folder, where the Human's Prompt overrides are kept.</summary>
    Prompts,

    /// <summary>A Team's workspace folder: writable and derived from the Team, so it is never saved as a pinned root and is not offered for removal.</summary>
    Workspace,

    /// <summary>The Memory backups root, where each Memory Tidy pass keeps the files it changed and its journal; read only and left out of every search.</summary>
    MemoryBackups,
}

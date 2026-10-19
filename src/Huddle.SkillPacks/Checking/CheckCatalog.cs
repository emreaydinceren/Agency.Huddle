namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>Every check code in use, in the order of the table in §6.3. UI text, not model-facing text.</summary>
public static class CheckCatalog
{
    private static readonly CheckInfo[] Entries =
    [
        new("R001", Severity.Error, "SKILLS.md exists and has a Packs table, a Catalogs table, or both, each under its own level-2 heading", "SKILLS.md is missing or has no Packs or Catalogs table. Ask your AI tool to follow InstallSkillPack.md and recreate it, or restore it from a backup."),
        new("R002", Severity.Error, "Each table has a Local path column", "Add a column headed Local path to that table, or ask your AI tool to repair it."),
        new("R003", Severity.Error, "The row's local path is not empty and, resolved for this computer, leads to a file that exists", "The file is not at that path. If you moved the clone, ask your AI tool to correct the row; if you deleted it, remove the row."),
        new("R004", Severity.Error, "No two rows resolve to the same file", "Two rows point at the same file. Remove the later one."),
        new("R005", Severity.Error, "A Packs row points at *.skillpack.json, a Catalogs row at *.skillcatalog.json", "This row is in the wrong table or points at the wrong kind of file. Move it, or fix its path."),
        new("R006", Severity.Warning, "Last updated is empty or a yyyy-MM-dd date", "Write the date as yyyy-MM-dd, for example 2026-10-17."),
        new("R007", Severity.Warning, "Origin URL is empty or an absolute https, ssh or git URL", "Give the full address, starting with https://. Huddle never opens it."),
        new("R008", Severity.Warning, "Every Pack id a role's suggestedPacks names is provided by some Packs row", "The hiring pool suggests a Pack that is not here. Ask your AI tool to add it, or ignore this."),
        new("R009", Severity.Warning, "A per-computer path has an entry for this computer or a * entry", "This file is not on this computer yet. Copy the fix prompt; your AI tool will fetch it here and add an entry for this computer."),
        new("R010", Severity.Error, "A per-computer path names each computer once", "Two entries name the same computer. Keep one."),
        new("R011", Severity.Error, "SKILLS.md is at most 1 MB", "SKILLS.md is far larger than a list of files should be. Restore it from a backup; it should hold one row per file, not per Skill."),
        new("R012", Severity.Warning, "Only one Packs and one Catalogs heading", "Merge the second table into the first, or rename its heading."),
        new("R013", Severity.Error, "A path holds at most one plain entry", "Two entries apply to every computer. Keep one, or give each its computer's name."),
        new("P001", Severity.Error, "The Pack file is JSON, an object, with a schemaVersion this Huddle knows", "The Pack file is not valid JSON, or was written for a newer Huddle. Ask your AI tool to rewrite it with `huddle-packs schema pack`."),
        new("P002", Severity.Error, "id, version, title, source and skills are present and of the right type", "A required field is missing. Run `huddle-packs schema pack` to see which."),
        new("P003", Severity.Error, "id is lowercase letters, digits and single hyphens, and the file is <id>.skillpack.json", "Rename the file to match the id, or fix the id."),
        new("P004", Severity.Error, "No earlier Packs row has the same id", "Another Pack already uses this id. Give one of them a new id."),
        new("P005", Severity.Error, "Every appliesTo entry is a role in this Huddle", "One of the roles is not in this Huddle. Run `huddle-packs roles` for the list, or add the role first."),
        new("P006", Severity.Warning, "appliesTo is not empty", "No Teammate card will offer this Pack. List the roles it is for."),
        new("P007", Severity.Warning, "consultWhenAdds is not empty and is at most 200 characters", "Say in one short phrase when a Teammate should reach for this Pack."),
        new("P008", Severity.Error, "No Skill name appears twice in one Pack", "A Skill is listed twice. Remove one entry."),
        new("P009", Severity.Error, "Each Pack Skill gives exactly one of path and catalog, and a catalog lists a Skill of that name", "A Skill in this Pack gives both a path and a catalog, neither, or names a catalog entry that does not exist. Give one, and check the catalog id and Skill name."),
        new("D001", Severity.Error, "The catalog file is JSON with a known schemaVersion, an id matching <id>.skillcatalog.json, a source and a skills list", "The catalog file is invalid or its id does not match its file name. Run `huddle-packs schema catalog`."),
        new("D002", Severity.Error, "Each entry has name, description and origin; name is a valid Skill name used once in this catalog", "A catalog entry is incomplete or repeated. Fix the entry named in the message."),
        new("D003", Severity.Error, "No two Packs or catalogs give one Skill name different origin strings", "Two Sources offer a Skill with this name but from different places. Rename one, or drop it from one Source."),
        new("D004", Severity.Warning, "origin names a commit, not a branch", "The origin is not pinned to a commit, so the Skill can change under you. Ask your AI tool to pin it."),
        new("D005", Severity.Error, "No earlier Catalogs row has the same catalog id", "Another catalog already uses this id. Give one of them a new id."),
        new("S001", Severity.Warning, "A path is filled in and leads to a folder; a Pack's Skill has a path", "This Skill's folder is not on this computer. Copy the prompt; your AI tool will fetch it."),
        new("S002", Severity.Error, "The folder has a SKILL.md whose name equals the folder name and whose description is at most 500 characters", "SKILL.md is missing, or its name or description is wrong. The Skill's name must equal its folder name."),
        new("S003", Severity.Error, "At most 200 files, and 5 MB in total", "The folder is too big for a Skill. Point at a smaller folder."),
        new("S004", Severity.Error, "No symbolic link or junction, no name Windows cannot store, no two names that differ only in case", "The folder holds a link or a file name Windows cannot keep. Remove it from the Skill."),
        new("S005", Severity.Error, "The name is not one of Huddle's built-in Skills", "This name belongs to a Skill that ships with Huddle. Rename the Skill."),
        new("S006", Severity.Error, "No Skill of this name in Skills/ came from a different origin", "A Skill with this name is already installed from somewhere else. Rename this one, or remove the installed one."),
        new("S007", Severity.Warning, "No .md file directly in the folder is over 64 KB", "Teammates skip files over 64 KB. Split the file."),
        new("S008", Severity.Warning, "The folder holds no files in sub-folders or not ending in .md", "Teammates read only the .md files directly in the Skill's folder; the rest is copied but never read."),
        new("S009", Severity.Warning, "A licence is declared, is not proprietary, and equals the Source's", "The licence is missing, proprietary or differs from the Source's. Read it before you share the folder."),
        new("S010", Severity.Warning, "No scripts, shell commands, network calls or paths outside the Work Dir", "The Skill ships scripts or commands. Huddle never runs them, and Teammates cannot read them."),
        new("S011", Severity.Error, "tools: names no App Tool", "Only Huddle's built-in Skills can grant App Tools. Remove the tool from the `tools:` line."),
        new("S012", Severity.Warning, "description is at most 300 characters", "A long description crowds a Teammate's Skill list. Shorten it."),
        new("O001", Severity.Error, "sources/<source>/source.json exists, matches the schema and has a known kind", "This Source has no valid source.json. Ask your AI tool to write it; `huddle-packs schema source` shows the format."),
        new("O002", Severity.Warning, "A github or git Source whose entries are not all pinned has a commit", "The Source is not pinned to a commit. Ask your AI tool to record it."),
        new("O003", Severity.Error, "A deprecated, withdrawn or moved status has statusReason, and moved has movedTo", "The Source's status needs a reason (and, if moved, where to)."),
        new("O004", Severity.Warning, "NOTES.md exists and is at most 16 KB", "The Source has no notes, or they are too long to read. Ask your AI tool to write or shorten them."),
        new("O005", Severity.Warning, "NOTES.md opens with front matter naming its layout", "Add `layout:` to the notes' front matter so the next visit starts from it."),
        new("X001", Severity.Error, "No file holds what looks like a secret", "A file seems to hold a password or token. Remove it, and change the secret: it may already be copied."),
        new("X002", Severity.Warning, "huddle-packs and this Huddle have the same version", "huddle-packs and this Huddle differ in version. Use the command line from InstallSkillPack.md."),
    ];

    private static readonly Dictionary<string, CheckInfo> ByCode = Entries.ToDictionary(static entry => entry.Code, StringComparer.Ordinal);

    /// <summary>Every code in use, in table order.</summary>
    public static IReadOnlyList<CheckInfo> All { get; } = Entries;

    /// <summary>Codes that were once used and must never come back.</summary>
    public static IReadOnlyList<string> Retired { get; } = [];

    /// <summary>The entry for a code, or null for an unknown one.</summary>
    public static CheckInfo? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return ByCode.GetValueOrDefault(code);
    }

    /// <summary>The fix hint for <paramref name="code"/>, or an empty string for an unknown one.</summary>
    public static string HintFor(string code)
    {
        return Find(code)?.FixHint ?? string.Empty;
    }
}

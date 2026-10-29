namespace Agency.Huddle.App.Skills.Ui;

/// <summary>The words of the remove confirmations (Skill Packs spec §6.12.2, §6.12.3, §6.12.5). UI text.</summary>
internal static class RemovalText
{
    /// <summary>The title of the Remove Pack confirm and of <c>RemovePackDialog</c>.</summary>
    public static string PackTitle(string title) => $"Remove \"{title}\"?";

    /// <summary>The message of the Remove Pack confirm when no Agent holds it.</summary>
    /// <param name="fileInFolder">The file's path relative to the Skill Sources folder, Windows separators, when it is inside; else null.</param>
    /// <param name="fileOutside">The file's full path when it is outside the folder; else null.</param>
    public static string PackMessage(string? fileInFolder, string? fileOutside)
    {
        string where = fileInFolder is not null
            ? $"This removes its row from SKILLS.md and deletes {fileInFolder} from the Skill Sources folder."
            : $"This removes its row from SKILLS.md. {Outside(fileOutside)}";
        return $"{where} No Agent has this Pack. Your AI tool's clone is not touched.";
    }

    /// <summary>The sentence for a file outside the folder.</summary>
    public static string Outside(string? file) =>
        file is null ? string.Empty : $"The file {file} is outside the Skill Sources folder, so Huddle leaves it where it is.";

    /// <summary>The title of the Remove from Agents confirm.</summary>
    public static string UnholdTitle(int count)
    {
        string agents = Agency.Huddle.App.Components.Shared.Plural.Of(count, "Agent", "Agents");
        return $"Remove from {agents}?";
    }

    /// <summary>The message of the Remove from Agents confirm.</summary>
    public static string UnholdMessage(IReadOnlyList<string> holders) =>
        $"{SkillStateText.AndList(holders)} will lose the Skills no one else gave them, and the Pack's \"consult when\" phrase. They restart. The Pack's row and file stay.";

    /// <summary>The title of the Remove catalog confirm.</summary>
    public static string CatalogTitle(string id) => $"Remove catalog \"{id}\"?";

    /// <summary>The message of the Remove catalog confirm.</summary>
    /// <param name="fileInFolder">The file relative to the folder, when inside.</param>
    /// <param name="fileOutside">The full path, when outside.</param>
    /// <param name="skills">How many Skills it lists.</param>
    /// <param name="packsUsingIt">The titles of the Packs that name its Skills.</param>
    public static string CatalogMessage(string? fileInFolder, string? fileOutside, int skills, IReadOnlyList<string> packsUsingIt)
    {
        ArgumentNullException.ThrowIfNull(packsUsingIt);
        string row = fileInFolder is not null
            ? $"This removes its row from SKILLS.md and deletes {fileInFolder} from the Skill Sources folder."
            : "This removes its row from SKILLS.md.";
        string listed = $"Its {Agency.Huddle.App.Components.Shared.Plural.Of(skills, "Skill", "Skills")} stop being listed. Agents keep their copies in Skills/.";
        string packs = packsUsingIt.Count == 0 ? string.Empty : $" These Packs name Skills in it and will show Broken until your AI tool fixes them: {SkillStateText.AndList(packsUsingIt)}.";
        string outside = fileOutside is null ? string.Empty : $" {Outside(fileOutside)}";
        return $"{row} {listed}{packs}{outside}";
    }

    /// <summary>The message of the Remove Source confirm.</summary>
    /// <param name="sourceId">The Source id.</param>
    /// <param name="rowTitles">The titles of the rows that name it.</param>
    /// <param name="clone">The clone's path from <c>source.json</c>, or null when it is unreadable.</param>
    public static string SourceMessage(string sourceId, IReadOnlyList<string> rowTitles, string? clone)
    {
        ArgumentNullException.ThrowIfNull(rowTitles);
        List<string> parts = [$"This permanently deletes the notes kept for {sourceId}: NOTES.md, source.json and log.jsonl."];
        if (rowTitles.Count > 0)
        {
            parts.Add($"It also removes {Agency.Huddle.App.Components.Shared.Plural.Of(rowTitles.Count, "row", "rows")} from SKILLS.md ({string.Join(", ", rowTitles)}) and deletes their files from the Skill Sources folder.");
        }

        parts.Add(clone is null ? "Agents keep their copies in Skills/." : $"Agents keep their copies in Skills/. Your AI tool's clone at {clone} is not touched.");
        parts.Add("If you share this folder with another Huddle, it loses the notes too.");
        return string.Join(' ', parts);
    }
}

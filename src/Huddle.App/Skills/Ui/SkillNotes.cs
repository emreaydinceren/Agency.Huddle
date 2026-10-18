namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Reads the first lines of a Source's <c>NOTES.md</c> for the Details dialog (Skill Packs spec §6.9.10).</summary>
internal static class SkillNotes
{
    /// <summary>What the dialog shows when the file cannot be read.</summary>
    public const string Unreadable = "The notes could not be read.";

    /// <summary>The first <paramref name="count"/> lines, joined by line feeds, or <see cref="Unreadable"/>.</summary>
    public static string FirstLines(string path, int count)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            return string.Join("\n", File.ReadLines(path).Take(count));
        }
        catch (IOException)
        {
            return Unreadable;
        }
        catch (UnauthorizedAccessException)
        {
            return Unreadable;
        }
    }
}

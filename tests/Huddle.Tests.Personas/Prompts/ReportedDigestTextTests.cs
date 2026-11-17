namespace Agency.Huddle.Tests.Prompts;

/// <summary>
/// Pins Memory Tidy spec §6.4's text for the Chief of Staff: before it summarises a manager's digest it reads that manager's
/// <c>reported.md</c> through <c>agent:</c>, in its built-in Persona text and in the <c>team-rollup</c> Skill alike.
/// </summary>
public sealed class ReportedDigestTextTests
{
    private const string Paragraph =
        "Before you summarise a manager's digest, read what that manager has already told the Human directly: call the " +
        "memory tool with command read, folder agent: followed by the manager's Name, and file reported.md. Leave out of " +
        "your summary anything listed there, unless the digest shows it has changed. If the tool says the manager has told " +
        "the Human nothing, or that no manager has that Name, summarise the whole digest. Your own reported.md works as the " +
        "Memory rules say.";

    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n'];

    /// <summary>Each file the Chief of Staff reads before a digest summary, as path segments under <c>src/Huddle.App</c>.</summary>
    public static TheoryData<string> Files() => new()
    {
        "Builtin/chief-of-staff.md",
        "Skills/Defaults/team-rollup/SKILL.md",
    };

    /// <summary>The file holds §6.4's paragraph word for word, however it is wrapped.</summary>
    /// <param name="file">The file's path under <c>src/Huddle.App</c>, with <c>/</c> separators.</param>
    [Theory]
    [MemberData(nameof(Files))]
    public void File_HoldsTheReportedParagraph(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string path = RepoPaths.App(file.Split('/'));

        string text = Flatten(File.ReadAllText(path));

        Assert.Contains(Flatten(Paragraph), text, StringComparison.Ordinal); // contains-ok: one paragraph inside a whole Persona or Skill file
    }

    /// <summary>Collapses every run of whitespace to one space so a wrapped line compares equal to its one-line form.</summary>
    /// <param name="text">The text.</param>
    private static string Flatten(string text) =>
        string.Join(' ', text.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries));
}

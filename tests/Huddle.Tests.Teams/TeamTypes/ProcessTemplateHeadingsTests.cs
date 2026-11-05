namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Pins D-35: every shipped built-in process template opens with an <c>## Identity</c> heading and carries the nine house headings, so each Team's AGENTS.md has the same sections whatever type it came from.</summary>
public sealed class ProcessTemplateHeadingsTests
{
    private static readonly string[] HouseHeadings =
    [
        "When it applies", "Who does what", "Stages", "Gates", "Escalation", "If a role is not hired", "Cadence", "Onboarding", "Definition of Done",
    ];

    /// <summary>Every built-in process template, as a path relative to the Builtin folder, in ordinal order, so each template is its own row.</summary>
    public static TheoryData<string> ProcessTemplatePaths
    {
        get
        {
            TheoryData<string> rows = new();
            foreach (string path in EnumerateProcessTemplates())
            {
                rows.Add(path);
            }

            return rows;
        }
    }

    /// <summary>Each process template's first <c>##</c> heading is exactly <c>Identity</c>.</summary>
    /// <param name="relativePath">The template path under the Builtin folder, such as <c>household/processes/household-basics.md</c>.</param>
    [Theory]
    [MemberData(nameof(ProcessTemplatePaths))]
    public void ProcessTemplate_FirstHeadingIsIdentity(string relativePath)
    {
        string[] headings = ReadHeadings(relativePath);

        Assert.NotEmpty(headings);
        Assert.Equal("Identity", headings[0]);
    }

    /// <summary>Each process template carries all nine house headings, and the list names any that are missing.</summary>
    /// <param name="relativePath">The template path under the Builtin folder, such as <c>household/processes/household-basics.md</c>.</param>
    [Theory]
    [MemberData(nameof(ProcessTemplatePaths))]
    public void ProcessTemplate_HasEveryHouseHeading(string relativePath)
    {
        string[] headings = ReadHeadings(relativePath);

        Assert.Empty(HouseHeadings.Except(headings, StringComparer.Ordinal));
    }

    /// <summary>No process template's <c>## Onboarding</c> section says "Propose a starter Task": the word reads as "create", and onboarding only suggests (#347).</summary>
    /// <param name="relativePath">The template path under the Builtin folder, such as <c>household/processes/household-basics.md</c>.</param>
    [Theory]
    [MemberData(nameof(ProcessTemplatePaths))]
    public void ProcessTemplate_OnboardingNeverSaysProposeAStarterTask(string relativePath)
    {
        string onboarding = ReadOnboarding(relativePath);

        Assert.NotEmpty(onboarding);
        Assert.DoesNotContain("Propose a starter Task", onboarding, StringComparison.Ordinal);
    }

    /// <summary>Reads the lines of the <c>## Onboarding</c> section, up to the next level-two heading.</summary>
    /// <param name="relativePath">The template path under the Builtin folder.</param>
    /// <returns>The section's lines joined by a newline; empty when the template has no such section.</returns>
    private static string ReadOnboarding(string relativePath)
    {
        string path = Path.Combine(BuiltinDirectory(), relativePath);
        string[] lines = File.ReadAllLines(path);
        return string.Join('\n', lines
            .SkipWhile(static line => !string.Equals(line.TrimEnd(), "## Onboarding", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(static line => !line.StartsWith("## ", StringComparison.Ordinal)));
    }

    /// <summary>Lists the process templates in the source folder: a <c>processes</c> folder under a type folder, holding Markdown files.</summary>
    /// <returns>The relative paths in ordinal order.</returns>
    private static IEnumerable<string> EnumerateProcessTemplates()
    {
        string builtin = BuiltinDirectory();
        return Directory.EnumerateFiles(builtin, "*.md", SearchOption.AllDirectories)
            .Where(static file => string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), "processes", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(builtin, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
    }

    /// <summary>Reads the level-two headings of one process template, in file order.</summary>
    /// <param name="relativePath">The template path under the Builtin folder.</param>
    /// <returns>The text of each <c>## </c> line, without the marker.</returns>
    private static string[] ReadHeadings(string relativePath)
    {
        string path = Path.Combine(BuiltinDirectory(), relativePath);
        return [.. File.ReadAllLines(path)
            .Where(static line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(static line => line[3..].TrimEnd())];
    }

    /// <summary>Walks up from the test binary until the directory holding <c>Huddle.slnx</c> is found, then returns the Builtin type folder beneath it.</summary>
    /// <returns>The <c>src/Huddle.App/TeamTypes/Builtin</c> directory.</returns>
    private static string BuiltinDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Huddle.App", "TeamTypes", "Builtin");
    }
}

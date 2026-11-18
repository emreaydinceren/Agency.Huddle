using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins #669 (spec V6a): the shipped processes, team types and hiring-pool personas ask for a review by creating a
/// review Task and never describe a Review stage or state, because Tasks have no Review state.
/// </summary>
public sealed partial class BuiltInProcessWordingTests
{
    private static readonly string[] DevelopmentProcesses = ["lightweight-flow", "maintenance", "sprints", "spec-driven"];

    /// <summary>No built-in process, team-type.json, persona or hiringpool.json text names a Review stage or state.</summary>
    [Fact]
    public void BuiltInContent_NamesNoReviewStageOrState()
    {
        string root = RepoRoot();
        List<string> offenders = [];
        foreach (string file in ContentFiles(root))
        {
            string text = File.ReadAllText(file);
            foreach (Match match in ReviewStateWording().Matches(text))
            {
                offenders.Add($"{Path.GetRelativePath(root, file)}: {match.Value}");
            }
        }

        Assert.Equal(string.Empty, string.Join('\n', offenders));
    }

    /// <summary>The content scan reads processes, team types, personas and the pool, so it cannot pass by finding nothing.</summary>
    [Fact]
    public void ContentFiles_CoverProcessesTeamTypesPersonasAndPool()
    {
        List<string> files = ContentFiles(RepoRoot());

        Assert.True(files.Count(static f => f.EndsWith(".md", StringComparison.Ordinal) && f.Contains("processes", StringComparison.Ordinal)) >= 20);
        Assert.True(files.Count(static f => f.EndsWith("team-type.json", StringComparison.Ordinal)) >= 20);
        Assert.True(files.Count(static f => f.Contains("personas", StringComparison.Ordinal)) >= 50);
        Assert.Contains(files, static f => f.EndsWith("hiringpool.json", StringComparison.Ordinal)); // contains-ok: path filter over file names
    }

    /// <summary>Each development process tells the owner to create a review Task.</summary>
    /// <param name="process">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("maintenance")]
    [InlineData("sprints")]
    [InlineData("spec-driven")]
    public void DevelopmentProcess_TellsTheOwnerToCreateAReviewTask(string process)
    {
        string text = File.ReadAllText(ProcessPath(RepoRoot(), process));

        Assert.Contains("create a review Task", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }

    /// <summary>The spec-driven process has no <c>Spec reviewed:</c> line and its Definition of Done uses the two tags.</summary>
    [Fact]
    public void SpecDriven_DefinitionOfDone_UsesTaskTagsNotSpecReviewed()
    {
        string text = File.ReadAllText(ProcessPath(RepoRoot(), "spec-driven"));

        Assert.DoesNotContain("Spec reviewed:", text, StringComparison.Ordinal);
        Assert.Contains("- Review: task tagged review", text, StringComparison.Ordinal); // contains-ok: pins a Definition of Done line
        Assert.Contains("- QA evidence: task tagged verify", text, StringComparison.Ordinal); // contains-ok: pins a Definition of Done line
    }

    /// <summary>The other development processes carry the same two Definition of Done lines in place of the old evidence lines.</summary>
    /// <param name="process">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("maintenance")]
    [InlineData("sprints")]
    public void OtherDevelopmentProcesses_DefinitionOfDone_UsesTaskTags(string process)
    {
        string text = File.ReadAllText(ProcessPath(RepoRoot(), process));

        Assert.Contains("- Review: task tagged review", text, StringComparison.Ordinal); // contains-ok: pins a Definition of Done line
        Assert.Contains("- QA evidence: task tagged verify", text, StringComparison.Ordinal); // contains-ok: pins a Definition of Done line
        Assert.DoesNotContain("evidence from challenge", text, StringComparison.Ordinal);
    }

    /// <summary>The blockers gate lists In Progress, Needs You or Done in every development process.</summary>
    /// <param name="process">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("maintenance")]
    [InlineData("sprints")]
    [InlineData("spec-driven")]
    public void BlockersGate_ListsInProgressNeedsYouOrDone(string process)
    {
        string text = File.ReadAllText(ProcessPath(RepoRoot(), process));

        Assert.Contains("move a Task to In Progress, Needs You or Done while", text, StringComparison.Ordinal); // contains-ok: pins the gate text
    }

    /// <summary>The reviewer and QA personas are consulted when a review or verify Task is assigned to them.</summary>
    [Fact]
    public void ReviewerAndQa_ConsultWhen_NameTheirTask()
    {
        string root = RepoRoot();

        Assert.Contains("consult_when: 'A review Task is assigned to you'", PersonaText(root, "reviewer"), StringComparison.Ordinal); // contains-ok: pins front matter
        Assert.Contains("consult_when: 'A verify Task is assigned to you'", PersonaText(root, "qa-engineer"), StringComparison.Ordinal); // contains-ok: pins front matter
    }

    /// <summary>The developer persona creates a review Task and the reviewer leaves it open with findings.</summary>
    [Fact]
    public void DeveloperAndReviewer_DescribeTheReviewTask()
    {
        string root = RepoRoot();

        Assert.Contains("create a review Task", PersonaText(root, "developer"), StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("leave the review Task open", PersonaText(root, "reviewer"), StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }

    /// <summary>Each development process, and the developer persona, tell the owner to answer findings on the same review Task and never create a second one for the same work.</summary>
    /// <param name="process">The process id, or <c>developer</c> for the persona.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("maintenance")]
    [InlineData("sprints")]
    [InlineData("spec-driven")]
    [InlineData("developer")]
    public void OwnerWording_AnswersFindingsOnTheSameReviewTask(string process)
    {
        string root = RepoRoot();
        string text = process == "developer" ? PersonaText(root, process) : File.ReadAllText(ProcessPath(root, process));

        Assert.Contains("on that same review Task that it is ready", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("never create a second review Task for the same work", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }

    /// <summary>Each development process tells the owner to answer QA findings on the same QA Task and never create a second one.</summary>
    /// <param name="process">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("maintenance")]
    [InlineData("sprints")]
    [InlineData("spec-driven")]
    public void DevelopmentProcess_AnswersQaFindingsOnTheSameQaTask(string process)
    {
        string text = File.ReadAllText(ProcessPath(RepoRoot(), process));

        Assert.Contains("on that same QA Task that it is ready", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("never create a second QA Task for the same work", text, StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }

    /// <summary>The reviewer and QA personas review again on the same Task when the owner comments that it is ready.</summary>
    [Fact]
    public void ReviewerAndQa_ReviewAgainOnTheSameTask()
    {
        string root = RepoRoot();

        Assert.Contains("you review again on the same review Task", PersonaText(root, "reviewer"), StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
        Assert.Contains("check again on the same verify Task", PersonaText(root, "qa-engineer"), StringComparison.Ordinal); // contains-ok: pins the wording an Agent reads
    }

    /// <summary>Lists every shipped process, team type, persona and the pool file.</summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The file paths.</returns>
    private static List<string> ContentFiles(string root)
    {
        string app = Path.Combine(root, "src", "Huddle.App");
        List<string> files = [];
        files.AddRange(Directory.EnumerateFiles(Path.Combine(app, "TeamTypes", "Builtin"), "*.md", SearchOption.AllDirectories)
            .Where(static f => f.Replace('\\', '/').Contains("/processes/", StringComparison.Ordinal)));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(app, "TeamTypes", "Builtin"), "team-type.json", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(app, "HiringPool", "personas"), "*.md"));
        files.Add(Path.Combine(app, "HiringPool", "hiringpool.json"));
        return files;
    }

    /// <summary>Resolves a development process file.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="process">The process id.</param>
    /// <returns>The file path.</returns>
    private static string ProcessPath(string root, string process)
    {
        Assert.Contains(process, DevelopmentProcesses);
        return Path.Combine(root, "src", "Huddle.App", "TeamTypes", "Builtin", "development", "processes", process + ".md");
    }

    /// <summary>Reads one hiring-pool persona.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="persona">The persona id.</param>
    /// <returns>The file text.</returns>
    private static string PersonaText(string root, string persona) =>
        File.ReadAllText(Path.Combine(root, "src", "Huddle.App", "HiringPool", "personas", persona + ".md"));

    /// <summary>Finds the repository root above the test folder.</summary>
    /// <returns>The repository root.</returns>
    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Huddle.slnx was not found above the test folder.");
    }

    /// <summary>Matches wording that names a Review stage or state, in either the old stage form or a sentence.</summary>
    /// <returns>The generated regex.</returns>
    [GeneratedRegex(
        @"\*\*Review\b|\b(?:in|to|from|into) Review\b|\bReview (?:state|stage|column|status)\b|Progress, Review|status: Review|""Review""|consult_when: '[^']*\bin Review\b",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex ReviewStateWording();
}

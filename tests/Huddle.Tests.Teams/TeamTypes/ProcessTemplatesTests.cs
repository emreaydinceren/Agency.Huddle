using System.Reflection;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the four shipped Development process templates (PR-1, PR-6): each has the PR-6 outline with the Onboarding section, a template id at version 1.0.0, known gates, and role ids that exist in the manifest.</summary>
public sealed partial class ProcessTemplatesTests
{
    private const string TypeFolder = "TeamTypes/Builtin/development/";

    private static readonly string[] OutlineHeadings =
    [
        "Identity",
        "When it applies",
        "Stages",
        "Who does what",
        "If a role is not hired",
        "Gates",
        "Definition of Done",
        "Cadence",
        "Escalation",
        "Onboarding",
    ];

    private static readonly string[] KnownGates =
    [
        "qa-evidence-before-done",
        "checks-before-merge",
        "accept-before-merge",
        "spec-approved-before-build",
        "failing-test-before-fix",
        "blockers-done-before-start",
    ];

    /// <summary>A process file split into the lines between its frontmatter fences and the lines of its body.</summary>
    /// <param name="Frontmatter">The lines between the two <c>---</c> fences, without the fences.</param>
    /// <param name="Body">The lines after the closing fence.</param>
    private sealed record ProcessFile(IReadOnlyList<string> Frontmatter, IReadOnlyList<string> Body);

    [GeneratedRegex("`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex BacktickSpan();

    /// <summary>Reads an embedded process file and splits it into frontmatter and body, so the tests see the files that ship.</summary>
    /// <param name="processId">The process id, which is also the file name without <c>.md</c>.</param>
    /// <returns>The parsed frontmatter and body lines.</returns>
    private static ProcessFile LoadProcess(string processId)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string target = TypeFolder + "processes/" + processId + ".md";
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == target);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        string[] lines = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal("---", lines[0]);
        int closing = Array.IndexOf(lines, "---", 1);
        Assert.True(closing > 0);
        return new ProcessFile(lines[1..closing], lines[(closing + 1)..]);
    }

    /// <summary>Returns the value of one frontmatter key with its quotes removed, and fails the test when the key is absent.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <param name="key">The frontmatter key, such as <c>template</c>.</param>
    /// <returns>The scalar value.</returns>
    private static string Scalar(ProcessFile file, string key)
    {
        string prefix = key + ":";
        string? line = file.Frontmatter.FirstOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
        Assert.NotNull(line);
        return line[prefix.Length..].Trim().Trim('\'');
    }

    /// <summary>Returns the items of a frontmatter flow list such as <c>gates: ['a', 'b']</c>.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <param name="key">The frontmatter key holding the list.</param>
    /// <returns>The list items in file order, without quotes.</returns>
    private static string[] ListValue(ProcessFile file, string key)
    {
        string raw = Scalar(file, key);
        return raw.Trim('[', ']')
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(static item => item.Trim('\''))
            .ToArray();
    }

    /// <summary>Returns the level-two headings of a process body, in document order.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <returns>The heading texts without the <c>## </c> marker.</returns>
    private static string[] Headings(ProcessFile file)
    {
        return file.Body
            .Where(static l => l.StartsWith("## ", StringComparison.Ordinal))
            .Select(static l => l[3..])
            .ToArray();
    }

    /// <summary>Returns the body lines under one level-two heading, up to the next heading.</summary>
    /// <param name="file">The parsed process file.</param>
    /// <param name="heading">The heading text, without the <c>## </c> marker.</param>
    /// <returns>The lines of that section.</returns>
    private static List<string> Section(ProcessFile file, string heading)
    {
        List<string> lines = [];
        bool inside = false;
        foreach (string line in file.Body)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line[3..], heading, StringComparison.Ordinal);
                continue;
            }

            if (inside)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>Extracts the text of every backtick span in the given lines.</summary>
    /// <param name="lines">The lines to scan.</param>
    /// <returns>Each span's text, in order.</returns>
    private static IEnumerable<string> BacktickSpans(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            foreach (Match match in BacktickSpan().Matches(line))
            {
                yield return match.Groups[1].Value;
            }
        }
    }

    /// <summary>Reads the embedded Development manifest, so role ids are checked against the real roles.</summary>
    /// <returns>The parsed manifest.</returns>
    private static TeamTypeManifest LoadManifest()
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(static n => n.Replace('\\', '/') == TypeFolder + "team-type.json");
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        TeamTypeManifest? manifest = System.Text.Json.JsonSerializer.Deserialize<TeamTypeManifest>(reader.ReadToEnd(), TeamTypeJson.Options);
        Assert.NotNull(manifest);
        return manifest;
    }

    /// <summary>Each process has the PR-6 outline in order, followed by the Onboarding section.</summary>
    /// <param name="processId">The process file to check.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_Outline_IsPR6HeadingsInOrderThenOnboarding(string processId)
    {
        ProcessFile file = LoadProcess(processId);

        Assert.Equal(OutlineHeadings, Headings(file));
    }

    /// <summary>Each process has a title and a template id that is its file name at version 1.0.0.</summary>
    /// <param name="processId">The process file to check.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_Frontmatter_TemplateIsFileNameAtVersion(string processId)
    {
        ProcessFile file = LoadProcess(processId);

        Assert.NotEmpty(Scalar(file, "title"));
        Assert.Equal(processId + "@1.0.0", Scalar(file, "template"));
    }

    /// <summary>Each process declares exactly the gates the plan gives it, and every gate is one of the six known ids.</summary>
    /// <param name="processId">The process file to check.</param>
    /// <param name="expectedGates">The expected gate ids, in file order.</param>
    [Theory]
    [InlineData("lightweight-flow", new[] { "qa-evidence-before-done", "checks-before-merge", "accept-before-merge", "blockers-done-before-start" })]
    [InlineData("spec-driven", new[] { "qa-evidence-before-done", "checks-before-merge", "spec-approved-before-build", "accept-before-merge", "blockers-done-before-start" })]
    [InlineData("sprints", new[] { "qa-evidence-before-done", "checks-before-merge", "accept-before-merge", "blockers-done-before-start" })]
    [InlineData("maintenance", new[] { "qa-evidence-before-done", "checks-before-merge", "failing-test-before-fix", "accept-before-merge", "blockers-done-before-start" })]
    public void Process_Gates_AreExpectedKnownIds(string processId, string[] expectedGates)
    {
        ProcessFile file = LoadProcess(processId);

        string[] gates = ListValue(file, "gates");
        Assert.All(gates, gate => Assert.Contains(gate, KnownGates));
        Assert.Equal(expectedGates, gates);
    }

    /// <summary>Every role id named in a process's Who does what section exists in the Development manifest.</summary>
    /// <param name="processId">The process file to check.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_WhoDoesWhat_NamesOnlyManifestRoles(string processId)
    {
        ProcessFile file = LoadProcess(processId);
        string[] roles = BacktickSpans(Section(file, "Who does what")).ToArray();
        string[] manifestRoles = LoadManifest().Roles.Select(static r => r.Id).ToArray();

        Assert.NotEmpty(roles);
        Assert.All(roles, role => Assert.Contains(role, manifestRoles));
    }

    /// <summary>#538: every Development process lists the blocker gate under Gates and tells the stage before the work starts to wait for blockers and the manager's go-ahead.</summary>
    /// <param name="processId">The process id.</param>
    /// <param name="stage">The stage line that carries the sentence: the first, or Build in spec-driven, whose first stage is the spec.</param>
    [Theory]
    [InlineData("lightweight-flow", "1. ")]
    [InlineData("spec-driven", "4. ")]
    [InlineData("sprints", "1. ")]
    [InlineData("maintenance", "1. ")]
    public void Process_Blockers_GatesAndStageSayTheTaskWaits(string processId, string stage)
    {
        ProcessFile file = LoadProcess(processId);
        string firstStage = Section(file, "Stages").First(line => line.StartsWith(stage, StringComparison.Ordinal));

        Assert.Contains("- `blockers-done-before-start`: Huddle refuses to move a Task to In Progress, Review, Needs You or Done while a Task in its `blocked_by` is still open (not Done, Cancelled, Duplicate or Rejected), and names the open blocker. Only the manager or the Human removes a blocker.", Section(file, "Gates")); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("A Task with blockers waits here until every blocker is finished and the manager gives the go-ahead; do no work on it before then.", firstStage, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>#536: every Development process tells QA to drive a UI acceptance in a browser, and tells the manager to do the same when no QA Engineer is hired.</summary>
    /// <param name="processId">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_Verify_DrivesAWebUiInABrowser(string processId)
    {
        ProcessFile file = LoadProcess(processId);
        string text = string.Join(' ', file.Body);

        Assert.Contains("drives it in a browser (the browser-check Skill)", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("When the acceptance names a web page or a UI, the manager drives it in a browser too. The Done gate still needs that Evidence line.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>#536: the QA Engineer and Development Manager personas carry the browser-check sentence, so a hired Agent is told to read the Skill.</summary>
    [Fact]
    public void Personas_QaAndManager_PointAtTheBrowserCheckSkill()
    {
        string qa = ReadPersona("qa-engineer");
        string manager = ReadPersona("development-manager");

        Assert.Contains("When the acceptance names a web page or a UI, drive it in a browser and record what you did as Evidence: read your browser-check Skill and follow it. If you cannot, say in the Evidence line that the verify is partial.", qa, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("When no QA Engineer is hired, you verify each acceptance line yourself. When it names a web page or a UI, drive it in a browser with your browser-check Skill and record what you did as Evidence.", manager, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>#537: every Development process starts its Definition of Done with a Review item that needs Evidence from challenge, before QA evidence.</summary>
    /// <param name="processId">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("spec-driven")]
    [InlineData("sprints")]
    [InlineData("maintenance")]
    public void Process_DefinitionOfDone_HasReviewEvidenceBeforeQa(string processId)
    {
        string text = ReadResourceText("TeamTypes/Builtin/development/processes/" + processId + ".md");
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.NotNull(document);

        int review = IndexOfLabel(document.DefinitionOfDone, "Review");
        int qa = IndexOfLabel(document.DefinitionOfDone, "QA evidence");

        Assert.True(review >= 0, "The Definition of Done has no Review item.");
        Assert.True(review < qa, "Review must come before QA evidence.");
        DoneRule.EvidenceFrom rule = Assert.IsType<DoneRule.EvidenceFrom>(document.DefinitionOfDone[review].Rule);
        Assert.Equal("challenge", rule.Step);
    }

    /// <summary>#537: the lightweight flow and sprints stages say QA starts only after the review, and what the reviewer records.</summary>
    /// <param name="processId">The process id.</param>
    [Theory]
    [InlineData("lightweight-flow")]
    [InlineData("sprints")]
    public void Process_Stages_QaStartsAfterReview(string processId)
    {
        ProcessFile file = LoadProcess(processId);
        string text = string.Join(' ', file.Body);

        Assert.Contains("QA starts only after the review has no open finding.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("If the change gets a new commit after the review, QA runs the verify again on that commit.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("runs the Team's check command on it and records the outcome on the Task as an Evidence line with that command", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>#537: the QA Engineer waits for the review, and the Reviewer records every review as an Evidence line with a command.</summary>
    [Fact]
    public void Personas_QaAndReviewer_OrderReviewBeforeVerify()
    {
        string qa = ReadPersona("qa-engineer");
        string reviewer = ReadPersona("reviewer");

        Assert.Contains("Start the verify only after the Task's review has no open finding: the reviewer's Evidence line is on the Task and nothing it raised is still open.", qa, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("If the change gets a new commit after the review, run the checks again on that commit and add a new Evidence line.", qa, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("Record every review on the Task, approval or bounce, as one Evidence line with the check command you ran, its exit code and your verdict in the note. A line with only a note does not count as a review in the Task's Proof.", reviewer, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.DoesNotContain("the evidence shows the checks passing", reviewer, StringComparison.Ordinal);
    }

    /// <summary>#550: every built-in process whose Definition of Done has a document-reviewed item says, in its Stages section, that the draft is a file attached to the Task and who logs the review.</summary>
    [Fact]
    public void Process_DocumentReviewedItem_StagesSayTheDraftIsAFile()
    {
        const string paragraph = "A draft that the Definition of Done reviews is a file, not text in the Task: its author saves it as a Markdown file in the Team's `outputs/` folder and attaches it to the Task by putting its full path, in backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs the review on it with `log_change` and the kind `reviewed`, and only then posts the Evidence line. A draft written only in the Task's description does not meet the review item.";
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Select(static n => n.Replace('\\', '/'))
            .Where(static n => n.Contains("TeamTypes/Builtin/", StringComparison.Ordinal) && n.Contains("/processes/", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal)) // contains-ok: resource path filter
            .ToArray();
        int checkedCount = 0;
        List<string> failures = [];
        foreach (string resource in resources)
        {
            string text = ReadResourceText(resource);
            Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document), resource);
            Assert.NotNull(document);
            if (!document.DefinitionOfDone.Any(static item => item.Rule is DoneRule.DocumentReviewedBy))
            {
                continue;
            }

            checkedCount++;
            string[] stages = StagesLines(text);
            if (!stages.Contains(paragraph, StringComparer.Ordinal))
            {
                failures.Add(resource);
            }
        }

        Assert.NotEqual(0, checkedCount);
        Assert.True(failures.Count == 0, "Stages lack the attached-file paragraph in: " + string.Join(", ", failures));
    }

    /// <summary>#547: the household process's Gates section says a gated Task waits in Review until its Proof is complete, and only then in Needs You.</summary>
    [Fact]
    public void Process_HouseholdBasicsGates_SayTheTaskWaitsInReviewUntilItsProofIsComplete()
    {
        string text = ReadResourceText("TeamTypes/Builtin/household/processes/household-basics.md");
        string gates = string.Join('\n', GatesLines(text));

        Assert.Contains("That Task waits in Review until its Proof is complete, then in Needs You until a parent accepts it", gates, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>#547: no built-in process claims a Task waits in Needs You without the Review and Proof step first.</summary>
    [Fact]
    public void Process_EveryBuiltIn_NeverSaysTheTaskWaitsInNeedsYouUntil()
    {
        string[] resources = typeof(TeamTypeCatalog).Assembly.GetManifestResourceNames()
            .Select(static n => n.Replace('\\', '/'))
            .Where(static n => n.Contains("TeamTypes/Builtin/", StringComparison.Ordinal) && n.Contains("/processes/", StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal)) // contains-ok: resource path filter
            .ToArray();

        Assert.NotEmpty(resources);
        List<string> failures = [.. resources.Where(static resource => ReadResourceText(resource).Contains("waits in Needs You until", StringComparison.Ordinal))]; // contains-ok: scanning shipped text for a banned claim
        Assert.True(failures.Count == 0, "These processes say a Task waits in Needs You with no Review step first: " + string.Join(", ", failures));
    }

    /// <summary>Returns the trimmed lines of the Gates section of a raw process file.</summary>
    /// <param name="text">The raw file text.</param>
    /// <returns>The lines between <c>## Gates</c> and the next level-two heading.</returns>
    private static string[] GatesLines(string text)
    {
        List<string> lines = [];
        bool inside = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line, "## Gates", StringComparison.Ordinal);
                continue;
            }

            if (inside)
            {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    /// <summary>Returns the trimmed lines of the Stages section of a raw process file.</summary>
    /// <param name="text">The raw file text.</param>
    /// <returns>The lines between <c>## Stages</c> and the next level-two heading.</returns>
    private static string[] StagesLines(string text)
    {
        List<string> lines = [];
        bool inside = false;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line, "## Stages", StringComparison.Ordinal);
                continue;
            }

            if (inside)
            {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    /// <summary>Returns the position of the Definition of Done item with a label, or -1.</summary>
    /// <param name="items">The items in written order.</param>
    /// <param name="label">The label to find.</param>
    /// <returns>The index, or -1 when absent.</returns>
    private static int IndexOfLabel(IReadOnlyList<DoneItem> items, string label)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].Label, label, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Reads any embedded resource as text, so the test sees the file that ships.</summary>
    /// <param name="target">The resource name with forward slashes.</param>
    /// <returns>The raw file text.</returns>
    private static string ReadResourceText(string target)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == target);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Reads an embedded hiring-pool persona file, so the test sees the text that ships.</summary>
    /// <param name="personaId">The persona id, which is the file name without <c>.md</c>.</param>
    /// <returns>The raw file text.</returns>
    private static string ReadPersona(string personaId)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string target = "HiringPool/personas/" + personaId + ".md";
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == target);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

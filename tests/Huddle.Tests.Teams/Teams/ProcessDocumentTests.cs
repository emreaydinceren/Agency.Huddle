using System.Reflection;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="ProcessDocument"/>: the Process.md frontmatter and outline reader (PR-6, PR-7).</summary>
public sealed class ProcessDocumentTests
{
    private const string FullOutline = """
        # Lightweight flow
        ## When it applies
        ## Stages
        ## Who does what
        ## If a role is not hired
        ## Gates
        ## Cadence
        ## Escalation
        """;

    private static string Compose(string frontmatter, string body = FullOutline) =>
        "---\n" + frontmatter + "\n---\n" + body.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    /// <summary>Reads a template or Process text into the settings the production <c>_team.md</c> reader holds for it.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The <see cref="TeamFile.Process"/> of the parsed text; empty when there is no closed frontmatter block.</returns>
    private static ProcessSettings Settings(string text) =>
        TeamFile.TryParse(text, out TeamFile? file) ? file.Process : ProcessSettings.Empty;

    private static ProcessDocument Parse(string text)
    {
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.NotNull(document);
        return document;
    }

    /// <summary>The Spec example parses into title, template, gates and retro cadence, and reports no missing section.</summary>
    [Fact]
    public void TryParse_SpecExample_ReadsEveryField()
    {
        string text = Compose("title: 'Lightweight flow'\ntemplate: 'lightweight-flow@1.0.0'\ngates: ['qa-evidence-before-done', 'checks-before-merge']\nretro: { every: 10, unit: 'done-tasks' }");

        ProcessDocument document = Parse(text);

        Assert.Equal("Lightweight flow", document.Title);
        ProcessSettings settings = Settings(text);
        Assert.Equal("lightweight-flow@1.0.0", settings.Template);
        Assert.Equal([GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge], settings.Gates);
        Assert.Equal(new RetroCadence(10, CadenceUnit.DoneTasks), settings.RetroSchedule());
        Assert.Null(settings.DigestSchedule());
        Assert.Null(settings.Wip);
        Assert.Null(settings.CheckCommand);
        Assert.Empty(document.MissingSections);
        Assert.Equal(text, document.Text);
    }

    /// <summary>All four gate ids parse, in file order, and round-trip through ToId and TryParseGate.</summary>
    [Fact]
    public void TryParse_AllFourGateIds_ParseAndRoundTrip()
    {
        ProcessSettings settings = Settings(Compose("title: 'T'\ngates: ['qa-evidence-before-done', 'checks-before-merge', 'spec-approved-before-build', 'failing-test-before-fix']"));

        Assert.Equal(
            [GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge, GateId.SpecApprovedBeforeBuild, GateId.FailingTestBeforeFix],
            settings.Gates);
        Assert.Equal(
            ["qa-evidence-before-done", "checks-before-merge", "spec-approved-before-build", "failing-test-before-fix"],
            settings.Gates.Select(ProcessDocument.ToId).ToArray());
        Assert.All(settings.Gates, gate =>
        {
            Assert.True(ProcessDocument.TryParseGate(ProcessDocument.ToId(gate), out GateId parsed));
            Assert.Equal(gate, parsed);
        });
    }

    /// <summary>TryParseGate rejects an unknown or blank id.</summary>
    [Fact]
    public void TryParseGate_UnknownId_ReturnsFalse()
    {
        Assert.False(ProcessDocument.TryParseGate("deploy-on-friday", out _));
        Assert.False(ProcessDocument.TryParseGate("", out _));
    }

    /// <summary>An unknown gate id is dropped from Gates and listed in UnknownGates; a duplicate known id is kept once.</summary>
    [Fact]
    public void TryParse_UnknownGate_IsListedNotKept()
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase) { ["gates"] = "['checks-before-merge', 'deploy-on-friday', 'checks-before-merge']" };

        (List<GateId> gates, List<string> unknown) = ProcessDocument.ReadGates(values);

        Assert.Equal([GateId.ChecksBeforeMerge], gates);
        Assert.Equal(["deploy-on-friday"], unknown);
    }

    /// <summary>Retro and digest cadences read both units; wip and checkCommand are read too.</summary>
    [Fact]
    public void TryParse_CadencesWipAndCheckCommand_AreRead()
    {
        ProcessSettings settings = Settings(Compose("title: 'T'\nretro: { every: 3, unit: 'days' }\ndigest: { every: 1, unit: 'days' }\nwip: 3\ncheckCommand: 'dotnet test'"));

        Assert.Equal(new RetroCadence(3, CadenceUnit.Days), settings.RetroSchedule());
        Assert.Equal(new DigestCadence(1, CadenceUnit.Days), settings.DigestSchedule());
        Assert.Equal(3, settings.Wip);
        Assert.Equal("dotnet test", settings.CheckCommand);
    }

    /// <summary>A retro with the done-tasks unit parses to DoneTasks.</summary>
    [Fact]
    public void TryParse_RetroDoneTasks_ParsesUnit()
    {
        ProcessSettings settings = Settings(Compose("title: 'T'\nretro: { every: 10, unit: 'done-tasks' }"));

        Assert.Equal(new RetroCadence(10, CadenceUnit.DoneTasks), settings.RetroSchedule());
    }

    /// <summary>A malformed cadence, an unknown unit, a non-positive count and a non-numeric wip all read as absent.</summary>
    [Theory]
    [InlineData("retro: { every: 10, unit: 'weeks' }")]
    [InlineData("retro: { every: 0, unit: 'days' }")]
    [InlineData("retro: { every: ten, unit: 'days' }")]
    [InlineData("retro: 10")]
    public void TryParse_MalformedRetro_IsNull(string line)
    {
        Assert.Null(Settings(Compose("title: 'T'\n" + line)).RetroSchedule());
    }

    /// <summary>A template id the frontmatter leaves blank, or writes no value for, reads as null; a written one is kept.</summary>
    [Theory]
    [InlineData("template: ''", null)]
    [InlineData("template:", null)]
    [InlineData("title: 'T'", null)]
    [InlineData("template: 'lightweight-flow@1.0.0'", "lightweight-flow@1.0.0")]
    public void TryParse_MalformedTemplate_ReadsAsNull(string line, string? expected)
    {
        Assert.Equal(expected, Settings(Compose("title: 'T'\n" + line)).Template);
    }

    /// <summary>A wip that is not a positive integer reads as null.</summary>
    [Theory]
    [InlineData("wip: many")]
    [InlineData("wip: 0")]
    public void TryParse_BadWip_IsNull(string line)
    {
        Assert.Null(Settings(Compose("title: 'T'\n" + line)).Wip);
    }

    /// <summary>Headings of the PR-6 outline absent from the body are reported in outline order; the extra Onboarding section is not required.</summary>
    [Fact]
    public void TryParse_MissingSections_AreReported()
    {
        ProcessDocument document = Parse(Compose("title: 'T'", "# T\n## When it applies\n## Gates\n## Onboarding"));

        Assert.Equal(["Stages", "Who does what", "If a role is not hired", "Cadence", "Escalation"], document.MissingSections);
    }

    /// <summary>CRLF text parses the same as LF text.</summary>
    [Fact]
    public void TryParse_CrLf_ParsesLikeLf()
    {
        string text = Compose("title: 'T'\ngates: ['checks-before-merge']").Replace("\n", "\r\n", StringComparison.Ordinal);

        ProcessDocument document = Parse(text);

        Assert.Equal("Lightweight flow", document.Title);
        Assert.Equal([GateId.ChecksBeforeMerge], Settings(text).Gates);
        Assert.Empty(document.MissingSections);
    }

    /// <summary>Empty text is rejected; a file with no frontmatter is prose and parses (Spec 6.2, D-10).</summary>
    [Fact]
    public void TryParse_EmptyText_ReturnsFalse()
    {
        Assert.False(ProcessDocument.TryParse(string.Empty, out ProcessDocument? document));
        Assert.Null(document);
    }

    /// <summary>A heading-only file with no frontmatter parses, with its title and no gates.</summary>
    [Fact]
    public void TryParse_NoFrontmatter_ParsesAsProse()
    {
        ProcessDocument document = Parse("# Just a heading\n## Stages\n");

        Assert.Equal("Just a heading", document.Title);
        Assert.Empty(Settings("# Just a heading\n## Stages\n").Gates);
    }

    /// <summary>Without a frontmatter title the first level-one heading is the title.</summary>
    [Fact]
    public void TryParse_NoTitleKey_FallsBackToHeading()
    {
        ProcessDocument document = Parse(Compose("template: 'no-version'"));

        Assert.Equal("Lightweight flow", document.Title);
    }

    /// <summary>All four shipped Development templates parse with the production parser, with their gates, cadences and the full outline.</summary>
    /// <param name="processId">The template file name without extension.</param>
    /// <param name="gateCount">The number of gates the template declares.</param>
    /// <param name="hasDigest">Whether the template declares a digest cadence.</param>
    /// <param name="wip">The expected WIP limit, or zero for none.</param>
    [Theory]
    [InlineData("lightweight-flow", 2, false, 0)]
    [InlineData("spec-driven", 3, false, 0)]
    [InlineData("sprints", 2, true, 0)]
    [InlineData("maintenance", 3, false, 2)]
    public void TryParse_ShippedTemplates_ParseCompletely(string processId, int gateCount, bool hasDigest, int wip)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string target = "TeamTypes/Builtin/development/processes/" + processId + ".md";
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == target);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);

        string text = reader.ReadToEnd();
        ProcessDocument document = Parse(text);
        ProcessSettings settings = Settings(text);

        Assert.Equal(processId + "@1.0.0", settings.Template);
        Assert.NotEmpty(document.Title);
        Assert.Equal(gateCount, settings.Gates.Count);
        Assert.Empty(document.MissingSections);
        Assert.Equal(new RetroCadence(10, CadenceUnit.DoneTasks), settings.RetroSchedule());
        Assert.Equal(hasDigest, settings.DigestSchedule() is not null);
        Assert.Equal(wip == 0 ? null : wip, settings.Wip);
        Assert.Equal("dotnet test", settings.CheckCommand);
    }

    /// <summary>Each gate reads as its sentence-case display name in the Process tab and the override prompt.</summary>
    [Fact]
    public void DisplayName_EachGate_ReadsAsItsSentenceCaseName()
    {
        Assert.Equal("QA evidence before done", ProcessDocument.DisplayName(GateId.QaEvidenceBeforeDone));
        Assert.Equal("Checks before merge", ProcessDocument.DisplayName(GateId.ChecksBeforeMerge));
        Assert.Equal("Spec approved before build", ProcessDocument.DisplayName(GateId.SpecApprovedBeforeBuild));
        Assert.Equal("Failing test before fix", ProcessDocument.DisplayName(GateId.FailingTestBeforeFix));
    }

    /// <summary>The lenient fence rule: a BOM, spaces around a fence line and a <c>...</c> closer all still give a document, and a fence line in the body does not end anything.</summary>
    /// <param name="opening">The first line.</param>
    /// <param name="closing">The closing line.</param>
    [Theory]
    [InlineData("---", "---")]
    [InlineData("\uFEFF---", "---")]
    [InlineData("---", "...")]
    [InlineData("  ---  ", "---")]
    [InlineData("---", "  ...  ")]
    [InlineData("\uFEFF  ---  ", "  ...  ")]
    public void TryParse_LenientFences_ReadTheFrontmatter(string opening, string closing)
    {
        string text = opening + "\ntitle: 'Flow'\nwip: 3\n" + closing + "\n# Flow\n## Stages\n---\nwip: 9\n...\n";

        ProcessDocument document = Parse(text);

        Assert.Equal("Flow", document.Title);
        Assert.Equal(3, Settings(text).Wip);
        Assert.DoesNotContain("Stages", document.MissingSections);
        Assert.Equal(text, document.Text);
    }

    /// <summary>A block that never closes is not frontmatter, whatever the opening line looks like; a near miss is not a fence. The text is prose and its settings are not read.</summary>
    /// <param name="text">The Process file.</param>
    [Theory]
    [InlineData("\uFEFF---\ntitle: 'Flow'\n")]
    [InlineData("  ---  \ntitle: 'Flow'\n")]
    [InlineData("---\ntitle: 'Flow'\n--- x\n")]
    [InlineData("----\ntitle: 'Flow'\n---\n")]
    [InlineData("---\ntitle: 'Flow'\n....\n")]
    public void TryParse_UnclosedOrNearMiss_IsProseWithoutSettings(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ProcessDocument document = Parse(text);

        Assert.Equal(string.Empty, document.Title);
        Assert.False(TeamFile.TryParse(text, out _));
        Assert.Equal(ProcessSettings.Empty, Settings(text));
    }
}

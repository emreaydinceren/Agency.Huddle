using System.Text.Json;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// The Team Memory Helper's judgement and its apply step (Team Memory Helper spec §6.4 to §6.6): the prompt values, the Verdict's
/// shape, and what Huddle writes, refuses or drops for each answer. Applied directly, without the dispatcher, except where a stale
/// Verdict queues its second judgement.
/// </summary>
public sealed class TeamMemoryHelperTests
{
    private const string Description = "Interview recording consent rules; read before recording an interview";

    /// <summary>The prompt values carry the proposal, the folders, the index and each candidate in full; blanks read as words, not gaps.</summary>
    [Fact]
    public void PromptValues_CarryTheProposalIndexAndCandidates()
    {
        TeamMemoryInput input = new(
            TeamMemoryHarness.Proposal(why: null),
            ["Research", "Research/Atlas"],
            ["Research/consent.md — Consent rules"],
            [new TeamMemoryCandidate("Research", "consent.md", "abc", "---\ndescription: Consent rules\n---\n\nWritten.\n")]);

        Dictionary<string, string> values = TeamMemoryHelper.ValuesFor(input);

        Assert.Equal("Research", values["{{team}}"]);
        Assert.Equal("Leo", values["{{proposer}}"]);
        Assert.Equal("Interviews are recorded only with written consent.", values["{{fact}}"]);
        Assert.Equal("not given", values["{{why}}"]);
        Assert.Equal("Research, Research/Atlas", values["{{folders}}"]);
        Assert.Equal("Research/consent.md — Consent rules", values["{{index}}"]);
        Assert.Equal("<file folder=\"Research\" name=\"consent.md\">\n---\ndescription: Consent rules\n---\n\nWritten.\n</file>", values["{{candidates}}"]);
        Assert.DoesNotContain("abc", values["{{candidates}}"], StringComparison.Ordinal);
    }

    /// <summary>An empty Memory reads as <c>(empty)</c> and no candidates as <c>none</c>.</summary>
    [Fact]
    public void PromptValues_EmptyMemory_SaysSo()
    {
        Dictionary<string, string> values = TeamMemoryHelper.ValuesFor(new TeamMemoryInput(TeamMemoryHarness.Proposal(), ["Research"], [], []));

        Assert.Equal("(empty)", values["{{index}}"]);
        Assert.Equal("none", values["{{candidates}}"]);
    }

    /// <summary>The fingerprint changes when the index changes, so a repeated proposal after a write is judged again.</summary>
    [Fact]
    public void Fingerprint_ChangesWithTheIndex()
    {
        using TeamMemoryHarness harness = new();
        TeamMemoryProposal proposal = TeamMemoryHarness.Proposal();

        string before = harness.Helper.Fingerprint(new TeamMemoryInput(proposal, ["Research"], [], []));
        string after = harness.Helper.Fingerprint(new TeamMemoryInput(proposal, ["Research"], ["Research/consent.md — Consent"], []));

        Assert.NotEqual(before, after);
        Assert.Equal(before, harness.Helper.Fingerprint(new TeamMemoryInput(proposal with { Id = Guid.NewGuid() }, ["Research"], [], [])));
    }

    /// <summary>The Verdict round-trips through its source-generated JSON metadata, camelCase, and a missing action is not read as "none".</summary>
    [Fact]
    public void Verdict_RoundTripsAndRequiresAnAction()
    {
        using TeamMemoryHarness harness = new();
        const string Json = """{"action": "create", "folder": "Research", "file": "consent.md", "description": "d", "text": "t", "reason": "new rule"}""";

        TeamMemoryVerdict? verdict = JsonSerializer.Deserialize(Json, harness.Helper.VerdictType);

        Assert.Equal(new TeamMemoryVerdict("create", "Research", "consent.md", "d", "t", "new rule"), verdict);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"reason": "x"}""", harness.Helper.VerdictType));
    }

    /// <summary>Each shape problem is caught before any write: an unknown action, no reason, and a create or update missing a field.</summary>
    /// <param name="action">The action.</param>
    /// <param name="file">The file, or <see langword="null"/>.</param>
    /// <param name="reason">The reason, or <see langword="null"/>.</param>
    /// <param name="problem">The expected problem, or <see langword="null"/> when the Verdict is usable.</param>
    [Theory]
    [InlineData("delete", "a.md", "why", "the action must be none, create or update")]
    [InlineData("none", null, " ", "the answer needs a reason")]
    [InlineData("NONE", null, "already recorded", null)]
    [InlineData("update", null, "why", "a create or an update needs a folder, a file, a description and a text")]
    [InlineData("Create", "a.md", "why", null)]
    public void Validate_ChecksTheShape(string action, string? file, string? reason, string? problem)
    {
        using TeamMemoryHarness harness = new();
        TeamMemoryVerdict verdict = new(action, "Research", file, Description, "Body.", reason);

        Assert.Equal(problem, harness.Helper.Validate(verdict));
    }

    /// <summary>A create writes the file with its frontmatter and records the proposer in its History and in the Feed (U-1).</summary>
    [Fact]
    public async Task Apply_Create_WritesTheFileAndAttributesIt()
    {
        using TeamMemoryHarness harness = new();
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        HelperApplied applied = await harness.ApplyAsync(input, new("create", "Research", "interview-consent.md", Description, "Written consent only.", "new rule on interview recording"));

        Assert.Equal("Created Research/interview-consent.md", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        string path = Path.Combine(harness.Folder(), "interview-consent.md");
        Assert.Equal(MemoryFileRules.Compose(Description, "Written consent only."), File.ReadAllText(path));
        Assert.True(harness.Resolver.TryResolveAbsolute(path, out LibraryPath? file));
        DocHistoryEntry entry = Assert.Single(harness.History.Read(file));
        Assert.Equal(TeamMemoryHelper.ActorName, entry.Actor);
        Assert.Equal(DocActorKind.Helper, entry.ActorKind);
        Assert.Equal(DocChangeKind.Created, entry.Kind);
        Assert.Equal("Proposed by Leo: new rule on interview recording", entry.Summary);
        FeedMessage posted = Assert.Single(harness.Feed.Posts);
        Assert.Equal(FeedSourceType.DocumentChanged, posted.SourceType);
        Assert.Equal(new FeedActor(FeedActorKind.Huddle, TeamMemoryHelper.ActorName), posted.Actor);
    }

    /// <summary>An update of a file the Helper was shown replaces it and records an edit (U-2).</summary>
    [Fact]
    public async Task Apply_UpdateOfAShownFile_ReplacesItAndRecordsAnEdit()
    {
        using TeamMemoryHarness harness = new();
        string path = harness.Put("interview-consent.md", Description, "Written consent only.");
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Consent can be verbal if recorded at the start of the interview."));

        HelperApplied applied = await harness.ApplyAsync(input, new("update", "Research", "interview-consent.md", Description, "Recorded verbal consent is enough (changed from written).", "consent rule relaxed"));

        Assert.Equal("Updated Research/interview-consent.md", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        Assert.Equal(MemoryFileRules.Compose(Description, "Recorded verbal consent is enough (changed from written)."), File.ReadAllText(path));
        Assert.True(harness.Resolver.TryResolveAbsolute(path, out LibraryPath? file));
        Assert.Equal(DocChangeKind.Edited, Assert.Single(harness.History.Read(file)).Kind);
    }

    /// <summary>A "none" writes nothing and leaves a Feed record with the reason, not a Notification (U-3).</summary>
    [Fact]
    public async Task Apply_None_WritesNothingAndRecordsWhy()
    {
        using TeamMemoryHarness harness = new();
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("I prefer short summaries."));

        HelperApplied applied = await harness.ApplyAsync(input, new("none", null, null, null, null, "A personal preference, not a Team fact"));

        Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.False(Directory.Exists(harness.Folder()));
        FeedMessage posted = Assert.Single(harness.Feed.Posts);
        Assert.Equal(FeedSourceType.TeamMemoryNotFiled, posted.SourceType);
        Assert.False(posted.Notify);
        TeamMemoryNotFiledPayload payload = Assert.IsType<TeamMemoryNotFiledPayload>(posted.Payload);
        Assert.Equal(("Research", "Leo", "I prefer short summaries.", "A personal preference, not a Team fact"), (payload.Team, payload.Proposer, payload.Fact, payload.Reason));
    }

    /// <summary>A folder outside the proposing Team is refused and nothing is written anywhere (U-6).</summary>
    /// <param name="folder">The folder the Verdict names.</param>
    [Theory]
    [InlineData("Marketing")]
    [InlineData("me")]
    [InlineData("../Research")]
    public async Task Apply_FolderOutsideTheTeam_IsRefused(string folder)
    {
        using TeamMemoryHarness harness = new();
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        HelperApplied applied = await harness.ApplyAsync(input, new("create", folder, "consent.md", Description, "Text.", "why"));

        Assert.Equal($"The answer names a folder outside Research: {folder}", Assert.IsType<HelperApplied.Refused>(applied).Message);
        Assert.Empty(harness.Feed.Posts);
    }

    /// <summary>A name, description or size the memory rules refuse is refused as it stands, never repaired.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="description">The description.</param>
    /// <param name="size">The body's length.</param>
    /// <param name="message">The refusal.</param>
    [Theory]
    [InlineData("../../Instructions.md", Description, 5, "The answer's file name is not a memory file name: ../../Instructions.md")]
    [InlineData("consent.md", "consent", 5, "The answer's description for consent.md is not usable: it only repeats the file name.")]
    [InlineData("consent.md", Description, 20000, "The answer's consent.md would be {size} bytes; the most is 16384")]
    public async Task Apply_InvalidVerdict_IsRefusedNotRepaired(string file, string description, int size, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using TeamMemoryHarness harness = new();
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());
        string body = new('x', size);

        HelperApplied applied = await harness.ApplyAsync(input, new("create", "Research", file, description, body, "why"));

        string composed = System.Text.Encoding.UTF8.GetByteCount(MemoryFileRules.Compose(description, body)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(message.Replace("{size}", composed, StringComparison.Ordinal), Assert.IsType<HelperApplied.Refused>(applied).Message);
        Assert.False(Directory.Exists(harness.Folder()) && Directory.EnumerateFiles(harness.Folder()).Any());
    }

    /// <summary>An update of a file the Helper was not shown in full is refused: it has no version to write against.</summary>
    [Fact]
    public async Task Apply_UpdateOfAFileNotShown_IsRefused()
    {
        using TeamMemoryHarness harness = new();
        string path = harness.Put("pricing.md", "Pricing decisions and discounts; read before quoting", "Ten percent.");
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        HelperApplied applied = await harness.ApplyAsync(input, new("update", "Research", "pricing.md", Description, "Overwritten.", "why"));

        Assert.Equal("The answer updates Research/pricing.md, which it was not shown", Assert.IsType<HelperApplied.Refused>(applied).Message);
        Assert.Equal(MemoryFileRules.Compose("Pricing decisions and discounts; read before quoting", "Ten percent."), File.ReadAllText(path));
    }

    /// <summary>A stale Verdict on its second judgement is dropped with a Feed record, and the file the Human wrote is kept (U-5).</summary>
    [Fact]
    public async Task Apply_StaleOnTheSecondJudgement_DropsWithAFeedRecord()
    {
        using TeamMemoryHarness harness = new();
        string path = harness.Put("interview-consent.md", Description, "Written consent only.");
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Interview recording consent."), attempt: 2);
        File.WriteAllText(path, MemoryFileRules.Compose(Description, "The Human's own rewrite."));

        HelperApplied applied = await harness.ApplyAsync(input, new("update", "Research", "interview-consent.md", Description, "Mine.", "why"));

        Assert.Equal("Research/interview-consent.md changed twice while it was being filed", Assert.IsType<HelperApplied.Dropped>(applied).Why);
        Assert.Equal(MemoryFileRules.Compose(Description, "The Human's own rewrite."), File.ReadAllText(path));
        Assert.Equal(FeedSourceType.TeamMemoryNotFiled, Assert.Single(harness.Feed.Posts).SourceType);
    }

    /// <summary>A stale Verdict on its first judgement is dropped for re-judging, naming the file, and nothing is written or posted yet.</summary>
    [Fact]
    public async Task Apply_StaleOnTheFirstJudgement_IsDroppedForReJudging()
    {
        using TeamMemoryHarness harness = new();
        string path = harness.Put("interview-consent.md", Description, "Written consent only.");
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal("Interview recording consent."));
        File.WriteAllText(path, MemoryFileRules.Compose(Description, "The Human's own rewrite."));

        HelperApplied applied = await harness.ApplyAsync(input, new("update", "Research", "interview-consent.md", Description, "Mine.", "why"));

        Assert.Equal("re-judging: Research/interview-consent.md changed", Assert.IsType<HelperApplied.Dropped>(applied).Why);
        Assert.Equal(MemoryFileRules.Compose(Description, "The Human's own rewrite."), File.ReadAllText(path));
        Assert.Empty(harness.Feed.Posts);
    }

    /// <summary>A Helper turned off while the run was going writes nothing.</summary>
    [Fact]
    public async Task Apply_HelperTurnedOffMeanwhile_Drops()
    {
        using TeamMemoryHarness harness = new();
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());
        harness.SettingsStore.Save(TeamMemoryHelper.HelperId, new HelperSettingsRow(false, null, null, null, 30));

        HelperApplied applied = await harness.ApplyAsync(input, new("create", "Research", "consent.md", Description, "Text.", "why"));

        Assert.Equal("the Helper was turned off", Assert.IsType<HelperApplied.Dropped>(applied).Why);
        Assert.False(File.Exists(Path.Combine(harness.Folder(), "consent.md")));
    }

    /// <summary>With History off the file is still written, and no History entry is made.</summary>
    [Fact]
    public async Task Apply_HistoryOff_WritesTheFileWithoutAnEntry()
    {
        using TeamMemoryHarness harness = new(static team => team.DocHistory.Enabled = false);
        TeamMemoryInput input = harness.Builder.Build(TeamMemoryHarness.Proposal());

        HelperApplied applied = await harness.ApplyAsync(input, new("create", "Research", "consent.md", Description, "Text.", "why"));

        Assert.IsType<HelperApplied.Changed>(applied);
        string path = Path.Combine(harness.Folder(), "consent.md");
        Assert.True(File.Exists(path));
        Assert.True(harness.Resolver.TryResolveAbsolute(path, out LibraryPath? file));
        Assert.Empty(harness.History.Read(file));
    }
}

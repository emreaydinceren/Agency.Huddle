using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.TeamMemory;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// A Team Memory proposal from the tool call to the file on disk (Team Memory Helper spec §5): the tool queues it, the dispatcher
/// waits and runs the fake adapter process, and the Helper's apply step writes the file. Also pins the re-judge of a stale Verdict,
/// a malformed answer, and the File Change another member sees at its next Turn.
/// </summary>
public sealed class TeamMemoryFlowTests
{
    private const string Description = "Interview recording consent rules; read before recording an interview";

    private const string CreateConsent =
        """{"action": "create", "folder": "Research", "file": "interview-consent.md", "description": "Interview recording consent rules; read before recording an interview", "text": "Written consent only.", "reason": "new rule on interview recording"}""";

    /// <summary>A member's proposal is filed: the tool answers at once, and after the wait the file exists with its description (U-1).</summary>
    [Fact]
    public async Task Propose_ThenTheWait_FilesTheFact()
    {
        using TeamMemoryHarness harness = new();
        harness.Process.Text = CreateConsent;

        string reply = await harness.ProposeAsync("Leo", "research", "Interviews are recorded only with written consent.", "Agreed with the Human");

        Assert.Equal("Proposed to Research's Team Memory.", reply);
        Assert.Equal(0, harness.Process.Calls);
        harness.PassTheWait();
        await harness.RecordedAsync();

        Assert.Equal("Created Research/interview-consent.md", harness.Status.Get(TeamMemoryHelper.HelperId).LastResult);
        Assert.Equal(MemoryFileRules.Compose(Description, "Written consent only."), File.ReadAllText(Path.Combine(harness.Folder(), "interview-consent.md")));
        Assert.Contains("<fact>\nInterviews are recorded only with written consent.\n</fact>", harness.Process.Requests[0].Prompt, StringComparison.Ordinal); // contains-ok: model prompt text; the proposal part is the assertion
    }

    /// <summary>Two proposals made within the wait are both judged: the dispatcher's key is the proposal, not the Team.</summary>
    [Fact]
    public async Task TwoProposalsWithinTheWait_AreBothJudged()
    {
        using TeamMemoryHarness harness = new();
        harness.Process.Text = """{"action": "none", "reason": "already recorded"}""";

        _ = await harness.ProposeAsync("Leo", "Research", "Pricing: ten percent discount at most.");
        _ = await harness.ProposeAsync("Miriam", "Research", "Interviews need written consent.");
        harness.PassTheWait();

        await harness.CallsAsync(2);
        Assert.Contains(harness.Process.Requests, static request => request.Prompt.Contains("<fact>\nPricing: ten percent discount at most.\n</fact>", StringComparison.Ordinal)); // contains-ok: model prompt text; which proposal it carries is the assertion
        Assert.Contains(harness.Process.Requests, static request => request.Prompt.Contains("<fact>\nInterviews need written consent.\n</fact>", StringComparison.Ordinal)); // contains-ok: model prompt text; which proposal it carries is the assertion
    }

    /// <summary>
    /// A create that finds its file written meanwhile is judged once more with that file in full, and the second Verdict's update
    /// lands: both facts end up in one file (U-4).
    /// </summary>
    [Fact]
    public async Task CreateRacesAnotherWrite_IsJudgedAgainAndUpdates()
    {
        using TeamMemoryHarness harness = new();
        string path = Path.Combine(harness.Folder(), "interview-consent.md");
        int call = 0;
        harness.Process.Handler = (request, _) =>
        {
            string text;
            if (Interlocked.Increment(ref call) == 1)
            {
                // Someone files the same topic while the first judgement runs.
                harness.Put("interview-consent.md", Description, "Consent is required.");
                text = CreateConsent;
            }
            else
            {
                Assert.Contains("Consent is required.", request.Prompt, StringComparison.Ordinal); // contains-ok: model prompt text; the file shown in full is the assertion
                text = """{"action": "update", "folder": "Research", "file": "interview-consent.md", "description": "Interview recording consent rules; read before recording an interview", "text": "Consent is required, and it must be written.", "reason": "merged with the earlier rule"}""";
            }

            return Task.FromResult(new HelperTurn(text, ModelOffered: true, ToolCallsRefused: 0, Cost: null));
        };

        _ = await harness.ProposeAsync("Leo", "Research", "Interview recording needs written consent.");
        harness.PassTheWait();
        await harness.CallsAsync(1);
        await Eventually.UntilAsync(() => harness.Status.Get(TeamMemoryHelper.HelperId).LastResult is not null, TestContext.Current.CancellationToken, TestBudgets.Standard);
        harness.PassTheWait();
        await harness.CallsAsync(2);
        await Eventually.UntilAsync(
            () => harness.Status.Get(TeamMemoryHelper.HelperId).LastResult == "Updated Research/interview-consent.md",
            TestContext.Current.CancellationToken,
            TestBudgets.Standard);

        Assert.Equal(MemoryFileRules.Compose(Description, "Consent is required, and it must be written."), File.ReadAllText(path));
    }

    /// <summary>An answer in prose is a failed run: nothing is written and the card shows the error.</summary>
    [Fact]
    public async Task ProseAnswer_WritesNothing()
    {
        using TeamMemoryHarness harness = new();
        harness.Process.Text = "I would file this under interview consent.";

        _ = await harness.ProposeAsync("Leo", "Research", "Interviews need written consent.");
        harness.PassTheWait();
        await harness.RecordedAsync();

        Assert.StartsWith("The answer wasn't the JSON the prompt asks for (", harness.Status.Get(TeamMemoryHelper.HelperId).LastError, StringComparison.Ordinal); // contains-ok: status text; the parser's problem after the prefix is not this Helper's
        Assert.False(Directory.Exists(harness.Folder()));
    }

    /// <summary>
    /// End to end: after a member's proposal is filed, another member's next Turn lists the file as a File Change, not as its own
    /// (U-10).
    /// </summary>
    [Fact]
    public async Task FiledFact_IsAFileChangeForAnotherMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamMemoryHarness harness = new();
        harness.Process.Text = CreateConsent;
        Directory.CreateDirectory(harness.Folder());
        AgentPaths agentPaths = new(harness.Options);
        TestPersonaFiles.Write(agentPaths, "miriam", "---\nName: Miriam\nTitle: Miriam\nAlias: Miriam\nTeams: [Research]\n---\nYou are Miriam.");
        SqliteTeamDirectory directory = new(harness.Options);
        await directory.InitializeAsync("You", ct);
        using PersonaStore personas = new(agentPaths, new PersonaModelStore(harness.Options), new PersonaEffortStore(harness.Options), new PersonaWorkModeStore(harness.Options), NullLogger<PersonaStore>.Instance);
        FileChangeTracker tracker = new(
            new FileStateStore(harness.Options, NullLogger<FileStateStore>.Instance),
            personas,
            directory,
            new WatchedFolderResolver(harness.Options, agentPaths),
            harness.Options,
            agentPaths,
            NullLogger<FileChangeTracker>.Instance,
            harness.Paths);
        await tracker.CommitAsync("Miriam", "room-a", await tracker.CollectAsync("Miriam", "room-a", [], ct), [], ct);

        _ = await harness.ProposeAsync("Leo", "Research", "Interviews are recorded only with written consent.");
        harness.PassTheWait();
        await harness.RecordedAsync();
        CollectedChanges next = await tracker.CollectAsync("Miriam", "room-a", [], ct);

        FileChange change = Assert.Single(next.Report.Changes);
        Assert.Equal(FileChangeKind.Added, change.Kind);
        Assert.Equal(Path.Combine(harness.Folder(), "interview-consent.md"), change.FullPath);
        Assert.Null(change.ByYouRoomName);
    }
}

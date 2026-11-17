using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Memory;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// The Memory Tidy pass with the Helper (Memory Tidy spec §6.5, §6.7 and §8.1 "Eligibility, the marker, failures", "The pass"): which folders are
/// judged, what is stored after a call, which failures count, how a pass ends early, and the list and the Feed item it leaves. The real judge runs
/// over a fake adapter process, a temp data folder and a manual clock.
/// </summary>
public sealed class MemoryTidyPassHelperTests
{
    private const string Key = "agent:Nova";
    private const string Today = "2026-11-16";
    private static readonly HelperId Helper = MemoryTidyHelper.HelperId;

    /// <summary>Fewer topic files than <c>MinFiles</c> makes no call and stores nothing.</summary>
    [Fact]
    public async Task RunAsync_FewerThanMinFiles_MakesNoCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 2);

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(0, rig.Process.Calls);
        Assert.Null(rig.State.GetState(Key));
    }

    /// <summary>A folder that has not changed since its Verdict is not asked about again.</summary>
    [Fact]
    public async Task RunAsync_UnchangedFingerprint_MakesNoSecondCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Text = Nothing;

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);
        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(1, rig.Process.Calls);
        Assert.Equal(FingerprintOf(rig, "Nova"), rig.State.GetState(Key)?.Fingerprint);
    }

    /// <summary>After a pass that changed a file the stored fingerprint is the folder's new one, so the pass's own write does not bring it back.</summary>
    [Fact]
    public async Task RunAsync_PassThatChangedFiles_StoresTheFingerprintAfterTheChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        string before = FingerprintOf(rig, "Nova");
        rig.Process.Text = Drops("t0.md");

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);
        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        string after = FingerprintOf(rig, "Nova");
        Assert.NotEqual(before, after);
        Assert.Equal(after, rig.State.GetState(Key)?.Fingerprint);
        Assert.Equal(1, rig.Process.Calls);
    }

    /// <summary>A Verdict whose steps were all refused stores the marker, is not a failed run, and three of them in a row raise no <c>HelperFailing</c> item.</summary>
    [Fact]
    public async Task RunAsync_VerdictWithEveryStepRefused_StoresTheMarkerAndIsNotAFailure()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Ada", "Bea", "Cyd");
        foreach (string agent in new[] { "Ada", "Bea", "Cyd" })
        {
            Fill(rig, agent, 3);
        }

        rig.Process.Text = Drops("nowhere.md");

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(3, rig.Process.Calls);
        foreach (string agent in new[] { "Ada", "Bea", "Cyd" })
        {
            MemoryTidyState? state = rig.State.GetState("agent:" + agent);
            Assert.Equal(FingerprintOf(rig, agent), state?.Fingerprint);
            Assert.Equal(0, state?.Failures);
        }

        HelperStatus status = rig.Status.Get(Helper);
        Assert.Null(status.LastError);
        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.DoesNotContain(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.HelperFailing);
    }

    /// <summary>A timeout stores no marker and counts one failure against the folder at the pass's end.</summary>
    [Fact]
    public async Task RunAsync_Timeout_StoresNoMarkerAndCountsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Handler = TimesOut;

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        MemoryTidyState? state = rig.State.GetState(Key);
        Assert.Null(state?.Fingerprint);
        Assert.Equal(1, state?.Failures);
        Assert.Equal(FingerprintOf(rig, "Nova"), state?.FailedFingerprint);
    }

    /// <summary>An answer that is not the JSON asked for stores no marker and counts one failure.</summary>
    [Fact]
    public async Task RunAsync_UnreadableAnswer_StoresNoMarkerAndCountsOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Text = "I could not decide.";

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        MemoryTidyState? state = rig.State.GetState(Key);
        Assert.Null(state?.Fingerprint);
        Assert.Equal(1, state?.Failures);
    }

    /// <summary>A Verdict the caps refuse as a whole is a failed call that ran: it counts one and stores no marker.</summary>
    [Fact]
    public async Task RunAsync_VerdictOverTheCaps_CountsOneAndStoresNoMarker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Memory.Tidy.MaxActions = 1, "Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Text = Drops("t0.md", "t1.md");

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        MemoryTidyState? state = rig.State.GetState(Key);
        Assert.Null(state?.Fingerprint);
        Assert.Equal(1, state?.Failures);
        Assert.True(File.Exists(Path.Combine(rig.MemoryDir("Nova"), "t0.md")));
    }

    /// <summary>An over-budget report stores no marker and counts none.</summary>
    [Fact]
    public async Task RunAsync_OverBudgetReport_StoresNoMarkerAndCountsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Helpers["MemoryTidy"] = new HelperOptions { DailyCap = 1 }, "Nova");
        Fill(rig, "Nova", 3);
        Assert.True(rig.Budget.TryReserve(Helper, 1));

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Cap, end);
        AssertNothingStored(rig);
    }

    /// <summary>A <c>null</c> report (Adapters off) stores no marker, counts none and makes no call.</summary>
    [Fact]
    public async Task RunAsync_AdaptersOff_StoresNoMarkerAndCountsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Acp.Enabled = false, "Nova");
        Fill(rig, "Nova", 3);

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(0, rig.Process.Calls);
        AssertNothingStored(rig);
    }

    /// <summary>A Helper the Human turned off makes no call, stores nothing, and the list says why.</summary>
    [Fact]
    public async Task RunAsync_HelperOff_JournalsWhyAndStoresNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Helpers["MemoryTidy"] = new HelperOptions { Enabled = false }, "Nova");
        Fill(rig, "Nova", 3);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(0, rig.Process.Calls);
        AssertNothingStored(rig);
        Assert.Contains(new MemoryTidyEntry.FolderNotJudged(Key, "the Helper was turned off"), MemoryTidyJournal.Read(PassFolder(rig)));
    }

    /// <summary>A Helper turned off while the call ran drops the Verdict: no marker, no count.</summary>
    [Fact]
    public async Task RunAsync_TurnedOffMeanwhile_StoresNoMarkerAndCountsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Handler = (_, _) =>
        {
            rig.Options.Value.Helpers["MemoryTidy"] = new HelperOptions { Enabled = false };
            return Task.FromResult(Turn(Drops("t0.md")));
        };

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(1, rig.Process.Calls);
        AssertNothingStored(rig);
        Assert.True(File.Exists(Path.Combine(rig.MemoryDir("Nova"), "t0.md")));
    }

    /// <summary>An Adapter that is not configured fails before the process starts: no call, no marker, no count.</summary>
    [Fact]
    public async Task RunAsync_AdapterNotConfigured_StoresNoMarkerAndCountsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Helpers["MemoryTidy"] = new HelperOptions { Adapter = "nope" }, "Nova");
        Fill(rig, "Nova", 3);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(0, rig.Process.Calls);
        AssertNothingStored(rig);
    }

    /// <summary>An Adapter that is not installed fails before the process starts: no call, no marker, no count.</summary>
    [Fact]
    public async Task RunAsync_AdapterNotInstalled_StoresNoMarkerAndCountsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Acp.Adapters = [new AdapterProfileOptions { Id = "claude", Command = "node", UsesToolNamePrefix = false }], "Nova");
        Fill(rig, "Nova", 3);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(0, rig.Process.Calls);
        AssertNothingStored(rig);
    }

    /// <summary>A Model the Adapter does not offer fails before an answer: no marker, no count.</summary>
    [Fact]
    public async Task RunAsync_ModelNotOffered_StoresNoMarkerAndCountsNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Handler = static (_, _) => Task.FromResult(new HelperTurn(string.Empty, ModelOffered: false, ToolCallsRefused: 0, Cost: null));

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        AssertNothingStored(rig);
    }

    /// <summary>Three failed calls in a row end the pass and count none of the three; the fourth folder is not asked.</summary>
    [Fact]
    public async Task RunAsync_ThreeFailedCalls_EndTheLoopAndCountNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Ada", "Bea", "Cyd", "Dov");
        foreach (string agent in new[] { "Ada", "Bea", "Cyd", "Dov" })
        {
            Fill(rig, agent, 3);
        }

        rig.Process.Handler = TimesOut;

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.ThreeFailures, end);
        Assert.Equal(3, rig.Process.Calls);
        Assert.All(rig.State.ListStates(), static state => Assert.Equal(0, state.Failures));
    }

    /// <summary>Two failures followed by a success in one pass count two, one against each folder.</summary>
    [Fact]
    public async Task RunAsync_TwoFailuresThenASuccess_CountTwo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Ada", "Bea", "Cyd");
        foreach (string agent in new[] { "Ada", "Bea", "Cyd" })
        {
            Fill(rig, agent, 3);
        }

        rig.Process.Handler = (request, token) => rig.Process.Calls <= 2 ? TimesOut(request, token) : Task.FromResult(Turn(Nothing));

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(1, rig.State.GetState("agent:Ada")?.Failures);
        Assert.Equal(1, rig.State.GetState("agent:Bea")?.Failures);
        Assert.Equal(0, rig.State.GetState("agent:Cyd")?.Failures);
        Assert.NotNull(rig.State.GetState("agent:Cyd")?.Fingerprint);
    }

    /// <summary>Tidy now twice at the cap makes the refused call each time, ends at the cap each time, raises the paused item, and leaves the folder eligible.</summary>
    [Fact]
    public async Task RunAsync_TidyNowTwiceAtTheCap_LeavesTheFolderEligible()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Helpers["MemoryTidy"] = new HelperOptions { DailyCap = 1 }, "Nova");
        Fill(rig, "Nova", 3);
        Assert.True(rig.Budget.TryReserve(Helper, 1));

        MemoryTidyPassEnd first = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);
        MemoryTidyPassEnd second = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal((MemoryTidyPassEnd.Cap, MemoryTidyPassEnd.Cap), (first, second));
        Assert.Contains(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.HelperPaused);
        Assert.Equal(2, rig.Budget.Today(Helper).Skipped);
        AssertNothingStored(rig);
    }

    /// <summary>A pass the Human did not start ends at the cap before the call and raises no paused item.</summary>
    [Theory]
    [InlineData("Timetable")]
    [InlineData("Overdue")]
    [InlineData("Continuation")]
    [InlineData("RunAtStart")]
    public async Task RunAsync_PassTheHumanDidNotStart_AtTheCap_EndsBeforeTheCallWithoutAnItem(string startName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Helpers["MemoryTidy"] = new HelperOptions { DailyCap = 1 }, "Nova");
        Fill(rig, "Nova", 3);
        Assert.True(rig.Budget.TryReserve(Helper, 1));

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(Enum.Parse<MemoryTidyStart>(startName), ct);

        Assert.Equal(MemoryTidyPassEnd.Cap, end);
        Assert.Equal(0, rig.Process.Calls);
        Assert.Equal(0, rig.Budget.Today(Helper).Skipped);
        Assert.DoesNotContain(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.HelperPaused);
    }

    /// <summary>Two counted failures on an unchanged folder leave it out with the list line, and a change brings it back.</summary>
    [Fact]
    public async Task RunAsync_TwoCountedFailures_LeaveTheFolderOutUntilItChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Handler = TimesOut;
        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);
        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);
        Assert.Equal(2, rig.Process.Calls);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(2, rig.Process.Calls);
        string list = await File.ReadAllTextAsync(MemoryTidyList.PathIn(PassFolder(rig, 3)), ct);
        Assert.Contains("Not tidied: failed twice; it will be tried again when it changes.", list, StringComparison.Ordinal);

        _ = rig.Seed("Nova", "t0.md", MemoryFileRules.Compose("Topic 0, read before using", "A changed body."));
        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(3, rig.Process.Calls);
    }

    /// <summary>A step that faulted (a file where the backup folder goes) stores no marker, and is not counted as a failed call.</summary>
    [Fact]
    public async Task RunAsync_FaultedStep_StoresNoMarker()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Handler = (_, _) =>
        {
            File.WriteAllText(Path.Combine(rig.Backups.RootPath, Today, "Agents"), "in the way");
            return Task.FromResult(Turn(Drops("t0.md")));
        };

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Contains(MemoryTidyJournal.Read(PassFolder(rig)), static entry => entry is MemoryTidyEntry.StepFaulted);
        MemoryTidyState? state = rig.State.GetState(Key);
        Assert.Null(state?.Fingerprint);
        Assert.Equal(0, state?.Failures ?? 0);
        Assert.True(File.Exists(Path.Combine(rig.MemoryDir("Nova"), "t0.md")));
    }

    /// <summary>The folders are taken least recently tidied first, never-tidied before all, then by key.</summary>
    [Fact]
    public async Task RunAsync_TakesTheLeastRecentlyTidiedFolderFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Ada", "Bea", "Cyd", "Dov");
        foreach (string agent in new[] { "Ada", "Bea", "Cyd", "Dov" })
        {
            Fill(rig, agent, 3);
        }

        rig.State.PutState(new MemoryTidyState("agent:Ada", TidiedUtc: new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero)));
        rig.State.PutState(new MemoryTidyState("agent:Bea", TidiedUtc: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)));
        rig.Process.Text = Nothing;

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        string[] order = [.. MemoryTidyJournal.Read(PassFolder(rig)).OfType<MemoryTidyEntry.FolderJudged>().Select(static entry => entry.FolderKey)];
        Assert.Equal(["agent:Cyd", "agent:Dov", "agent:Bea", "agent:Ada"], order);
    }

    /// <summary>One <c>changes.md</c> lists every applied, refused, skipped and faulted step, with the tokens a rewrite removed.</summary>
    [Fact]
    public async Task RunAsync_OnePass_WritesOneListWithEveryKindOfStep()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Memory.Tidy.MaxDropsPerFolder = 10, "Nova");
        const string Long = "Price is 5 euro on 2026-10-01 and shipping is slow and packaging is poor and colour is blue and size is large.";
        _ = rig.Seed("Nova", "t0.md", MemoryFileRules.Compose("Price notes, read before quoting", Long));
        foreach (string file in new[] { "t1.md", "t2.md", "t3.md" })
        {
            _ = rig.Seed("Nova", file, MemoryFileRules.Compose("Topic " + file + ", read before using", "Body of " + file));
        }

        rig.Process.Handler = (_, _) =>
        {
            _ = Directory.CreateDirectory(Path.Combine(rig.Backups.RootPath, Today, "Agents", "Nova", "t3.md"));
            File.WriteAllText(Path.Combine(rig.MemoryDir("Nova"), "t2.md"), MemoryFileRules.Compose("Topic t2.md, read before using", "Someone else wrote this."));
            return Task.FromResult(Turn("""
                {"actions":[
                {"action":"rewrite","file":"t0.md","text":"Price is 5 euro and shipping is slow and packaging is poor and colour is blue and size is large.","reason":"repeats itself"},
                {"action":"drop","file":"t1.md","reason":"out of date"},
                {"action":"drop","file":"nowhere.md","reason":"gone"},
                {"action":"drop","file":"t2.md","reason":"out of date"},
                {"action":"drop","file":"t3.md","reason":"out of date"}]}
                """));
        };

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        string[] lists = [.. Directory.EnumerateFiles(rig.Backups.RootPath, MemoryTidyList.FileName, SearchOption.AllDirectories)];
        string list = await File.ReadAllTextAsync(Assert.Single(lists), ct);
        MemoryTidyEntry[] entries = [.. MemoryTidyJournal.Read(PassFolder(rig))];
        Assert.Contains(entries, static entry => entry is MemoryTidyEntry.StepApplied { RemovedTokens: ["2026-10-01"] });
        Assert.Contains(entries, static entry => entry is MemoryTidyEntry.StepRefused);
        Assert.Contains(entries, static entry => entry is MemoryTidyEntry.StepSkipped);
        Assert.Contains(entries, static entry => entry is MemoryTidyEntry.StepFaulted);
        Assert.Contains("2026-10-01", list, StringComparison.Ordinal);
        Assert.Contains("t1.md", list, StringComparison.Ordinal);
        Assert.Contains("nowhere.md", list, StringComparison.Ordinal);
        Assert.Contains("changed while it was being tidied", list, StringComparison.Ordinal);
        Assert.Contains("the copy or the write failed", list, StringComparison.Ordinal);
    }

    /// <summary>A pass that changed topic files posts one <c>MemoryTidied</c> Notification with the counts and the list as its target.</summary>
    [Fact]
    public async Task RunAsync_ChangedFiles_PostsOneNotificationWithTheCounts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 4);
        rig.Process.Text = Drops("t0.md", "t1.md");

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        FeedMessage item = Assert.Single(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.MemoryTidied);
        MemoryTidiedPayload payload = Assert.IsType<MemoryTidiedPayload>(item.Payload);
        Assert.True(item.Notify);
        Assert.Equal((0, 2, 1, false), (payload.Tidied, payload.Dropped, payload.Folders, payload.Stopped));
        Assert.Equal(MemoryTidyList.PathIn(PassFolder(rig)), payload.Path);
        Assert.Equal(payload.Path, item.TargetKey);
    }

    /// <summary>A pass that changed nothing posts no item.</summary>
    [Fact]
    public async Task RunAsync_NothingChanged_PostsNoItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 3);
        rig.Process.Text = Nothing;

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.DoesNotContain(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.MemoryTidied);
    }

    /// <summary>A folder needing three calls, unchanged, is judged on three successive passes, each showing the next file, and not on the fourth.</summary>
    [Fact]
    public async Task RunAsync_FolderLargerThanOneCall_IsJudgedUntilTheRotationComesRound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig(team => team.Memory.Tidy.MaxInputChars = 130, "Nova");
        Fill(rig, "Nova", 3);
        int length = (await File.ReadAllTextAsync(Path.Combine(rig.MemoryDir("Nova"), "t0.md"), ct)).Length;
        rig.Options.Value.Memory.Tidy.MaxInputChars = length + (length / 2);
        rig.Process.Text = Nothing;
        List<int?> offsets = [];
        List<bool> marked = [];

        for (int pass = 0; pass < 4; pass++)
        {
            _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);
            offsets.Add(rig.State.GetState(Key)?.Offset);
            marked.Add(rig.State.GetState(Key)?.Fingerprint is not null);
        }

        Assert.Equal(3, rig.Process.Calls);
        Assert.Equal([1, 2, 0, 0], offsets);
        Assert.Equal([false, false, true, true], marked);
    }

    /// <summary>A stop after the second step leaves the list with two applied steps and exactly one Feed item.</summary>
    [Fact]
    public async Task RunAsync_CancelledAfterTheSecondStep_ListsTwoAndPostsOneItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = Rig("Nova");
        Fill(rig, "Nova", 4);
        rig.Process.Text = Drops("t0.md", "t1.md", "t2.md");
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        int writes = 0;
        rig.Writer.AfterReadForTests = _ =>
        {
            if (++writes == 3)
            {
                stop.Cancel();
                stop.Token.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        };

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, stop.Token);

        Assert.Equal(MemoryTidyPassEnd.Stopped, end);
        MemoryTidyEntry[] entries = [.. MemoryTidyJournal.Read(PassFolder(rig))];
        Assert.Equal(2, entries.OfType<MemoryTidyEntry.StepApplied>().Count());
        string list = await File.ReadAllTextAsync(MemoryTidyList.PathIn(PassFolder(rig)), ct);
        Assert.Contains("t0.md", list, StringComparison.Ordinal);
        Assert.Contains("t1.md", list, StringComparison.Ordinal);
        Assert.Single(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.MemoryTidied);
        Assert.Null(rig.State.GetState(Key)?.Fingerprint);
    }

    private static string Nothing => """{"actions":[]}""";

    private static TidyRig Rig(params string[] agents) => Rig(null, agents);

    private static TidyRig Rig(Action<TeamOptions>? configure, params string[] agents) => new(
        team =>
        {
            team.Memory.Tidy.MinFiles = 3;
            configure?.Invoke(team);
        },
        agents);

    private static string Drops(params string[] files) =>
        "{\"actions\":[" + string.Join(',', files.Select(static file => "{\"action\":\"drop\",\"file\":\"" + file + "\",\"reason\":\"out of date\"}")) + "]}";

    private static HelperTurn Turn(string text) => new(text, ModelOffered: true, ToolCallsRefused: 0, Cost: null);

    private static Task<HelperTurn> TimesOut(HelperSessionRequest request, CancellationToken cancellationToken) =>
        Task.FromException<HelperTurn>(new OperationCanceledException("The call timed out."));

    private static void Fill(TidyRig rig, string agent, int count)
    {
        for (int index = 0; index < count; index++)
        {
            _ = rig.Seed(agent, $"t{index}.md", MemoryFileRules.Compose($"Topic {index}, read before using", $"Body {index} of {agent}."));
        }
    }

    private static string FingerprintOf(TidyRig rig, string agent) => MemoryTidyInputBuilder.FolderFingerprint(
        Directory.EnumerateFiles(rig.MemoryDir(agent), "t*.md").Select(static path => (Path.GetFileName(path), MemoryVersion.Of(File.ReadAllBytes(path)))));

    private static string PassFolder(TidyRig rig, int number = 1) => Path.Combine(rig.Backups.RootPath, number == 1 ? Today : $"{Today}-{number}");

    private static void AssertNothingStored(TidyRig rig)
    {
        MemoryTidyState? state = rig.State.GetState(Key);
        Assert.Null(state?.Fingerprint);
        Assert.Equal(0, state?.Failures ?? 0);
    }
}

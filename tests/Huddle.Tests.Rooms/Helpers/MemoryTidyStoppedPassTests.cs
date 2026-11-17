using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Memory;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// A Memory Tidy pass that is stopped half way, or killed (Memory Tidy spec §6.12, §8.1 "A pass stopped half way"): the list is finished by the pass's
/// own <c>finally</c> or by recovery at the next start, an open step is settled the same way by both, a settled step is not settled twice, and a
/// finished pass is never posted again.
/// </summary>
public sealed class MemoryTidyStoppedPassTests
{
    private const string Head = "---\ndescription: What I have already told the Human, by date and Room. Read before reporting so nothing is said twice.\n---\n\n";
    private const string Old = "- 2026-10-01 · Nova · long ago\n";
    private const string Started = "2026-11-16";

    /// <summary>A stop after the second Agent's file was pruned leaves a finished pass: both drops listed, the end written as <c>Stopped</c>, and no Feed item.</summary>
    [Fact]
    public async Task RunAsync_StoppedAfterTwoPrunes_ListsBothAndFinishesThePass()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Ada", "Bea", "Cyd");
        foreach (string agent in new[] { "Ada", "Bea", "Cyd" })
        {
            _ = rig.Seed(agent, "reported.md", Head + Old);
        }

        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        rig.Writer.AfterReadForTests = folder =>
        {
            if (folder.Contains("Cyd", StringComparison.Ordinal))
            {
                stop.Cancel();
                stop.Token.ThrowIfCancellationRequested();
            }

            return Task.CompletedTask;
        };
        string passFolder = Path.Combine(rig.Backups.RootPath, Started);

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, stop.Token);

        Assert.Equal(MemoryTidyPassEnd.Stopped, end);
        IReadOnlyList<MemoryTidyEntry> entries = MemoryTidyJournal.Read(passFolder);
        Assert.Equal(["Ada", "Bea"], entries.OfType<MemoryTidyEntry.LinesDropped>().Select(static entry => entry.Agent));
        Assert.Contains(new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.Stopped), entries);
        Assert.Equal(new MemoryTidyEntry.PassFinished(false), entries[^1]);
        string list = await File.ReadAllTextAsync(MemoryTidyList.PathIn(passFolder), ct);
        Assert.Contains("Ada", list, StringComparison.Ordinal);
        Assert.Contains("Bea", list, StringComparison.Ordinal);
        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>A killed pass (a journal with entries and no <c>PassFinished</c>) is finished at start-up: the list is written once and one item is posted, never a second.</summary>
    [Fact]
    public async Task RecoverAsync_KilledPass_WritesTheListOnceAndPostsAtMostOneItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        string folder = rig.SeedPass(Started, Open("a.md", "version-1"), AppliedStep(1, "a.md"));
        string list = MemoryTidyList.PathIn(folder);

        await rig.Recovery.RecoverAsync(ct);
        string firstList = await File.ReadAllTextAsync(list, ct);
        int journalLength = MemoryTidyJournal.Read(folder).Count;
        await rig.Recovery.RecoverAsync(ct);

        FeedMessage item = Assert.Single(rig.Feed.Posts);
        MemoryTidiedPayload payload = Assert.IsType<MemoryTidiedPayload>(item.Payload);
        Assert.Equal(FeedSourceType.MemoryTidied, item.SourceType);
        Assert.True(item.Notify);
        Assert.Equal((1, 0, 1, true, true), (payload.Tidied, payload.Dropped, payload.Folders, payload.Stopped, payload.First));
        Assert.Equal(list, payload.Path);
        Assert.Equal(firstList, await File.ReadAllTextAsync(list, ct));
        Assert.Equal(journalLength, MemoryTidyJournal.Read(folder).Count);
        Assert.Equal(new MemoryTidyEntry.PassFinished(true), MemoryTidyJournal.Read(folder)[^1]);
    }

    /// <summary>A pass already finished is not posted again, even though its journal says topic files changed.</summary>
    [Fact]
    public async Task RecoverAsync_FinishedPass_IsNotPostedAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        _ = rig.SeedPass(
            Started,
            Open("a.md", "version-1"),
            AppliedStep(1, "a.md"),
            new MemoryTidyEntry.PassEnded(MemoryTidyPassEnd.Done),
            new MemoryTidyEntry.PassFinished(true));

        await rig.Recovery.RecoverAsync(ct);

        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>A killed pass that only dropped <c>reported.md</c> lines is finished with its list and no Feed item.</summary>
    [Fact]
    public async Task RecoverAsync_KilledPassThatOnlyDroppedLines_WritesTheListAndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        string folder = rig.SeedPass(
            Started,
            new MemoryTidyEntry.PassStarted(Started, rig.Clock.GetUtcNow(), MemoryTidyStart.TidyNow),
            new MemoryTidyEntry.LinesDropped("Nova", 2, "Agents/Nova/reported.md"));

        await rig.Recovery.RecoverAsync(ct);

        Assert.True(File.Exists(MemoryTidyList.PathIn(folder)));
        Assert.Empty(rig.Feed.Posts);
        Assert.Equal(new MemoryTidyEntry.PassFinished(false), MemoryTidyJournal.Read(folder)[^1]);
    }

    /// <summary>One pass folder that cannot be finished does not stop recovery of the next: the first folder's list cannot be written, the second is still finished.</summary>
    [Fact]
    public async Task RecoverAsync_FirstFolderFails_FinishesTheNextOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        MemoryTidyEntry[] dropped =
        [
            new MemoryTidyEntry.PassStarted(Started, rig.Clock.GetUtcNow(), MemoryTidyStart.TidyNow),
            new MemoryTidyEntry.LinesDropped("Nova", 1, "Agents/Nova/reported.md"),
        ];
        string bad = rig.SeedPass("2026-11-01", dropped);
        string good = rig.SeedPass("2026-11-02", dropped);
        _ = Directory.CreateDirectory(MemoryTidyList.PathIn(bad));

        await rig.Recovery.RecoverAsync(ct);

        Assert.DoesNotContain(MemoryTidyJournal.Read(bad), static entry => entry is MemoryTidyEntry.PassFinished);
        Assert.Equal(new MemoryTidyEntry.PassFinished(false), MemoryTidyJournal.Read(good)[^1]);
        Assert.True(File.Exists(MemoryTidyList.PathIn(good)));
    }

    /// <summary>The first item says it is the first; once a pass has posted one, the next does not.</summary>
    [Fact]
    public async Task RecoverAsync_AfterAnEarlierPostedPass_TheItemIsNotTheFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        _ = rig.SeedPass("2026-11-09", new MemoryTidyEntry.PassStarted("2026-11-09", rig.Clock.GetUtcNow(), MemoryTidyStart.Timetable), new MemoryTidyEntry.PassFinished(true));
        _ = rig.SeedPass(Started, Open("a.md", "version-1"), AppliedStep(1, "a.md"));

        await rig.Recovery.RecoverAsync(ct);

        MemoryTidiedPayload payload = Assert.IsType<MemoryTidiedPayload>(Assert.Single(rig.Feed.Posts).Payload);
        Assert.False(payload.First);
    }

    /// <summary>
    /// An open step is settled by the pass's <c>finally</c> and by recovery the same way: not done when the versions match, a copy to compare when they
    /// differ and a copy is in the pass folder, "changed by someone else" when they differ and there is none.
    /// </summary>
    [Theory]
    [InlineData(false, "same", false, true, "Not done: the pass was stopped")]
    [InlineData(false, "differs", true, false, "Compare it with its copy")]
    [InlineData(false, "differs", false, false, "It was changed by someone else")]
    [InlineData(true, "same", false, true, "Not done: the pass was stopped")]
    [InlineData(true, "differs", true, false, "Compare it with its copy")]
    [InlineData(true, "differs", false, false, "It was changed by someone else")]
    public async Task Settle_OpenStep_IsSettledTheSameByTheFinallyAndByRecovery(bool byFinally, string versions, bool hasCopy, bool notDone, string listed)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        string version = rig.Seed("Nova", "a.md", MemoryFileRules.Compose("Interview recording rules; read before scheduling", "Body.\n"));
        string recorded = versions == "same" ? version : "0123456789ab";
        MemoryTidyEntry.StepStarted open = new(1, "agent:Nova", "rewrite", [new MemoryTidyEntry.StepFile("a.md", recorded)], "tidy");
        string folder;
        if (byFinally)
        {
            folder = await StopWithOpenStepAsync(rig, open, hasCopy);
        }
        else
        {
            folder = rig.SeedPass(Started, Open("a.md", recorded), open);
            if (hasCopy)
            {
                WriteCopy(folder);
            }

            await rig.Recovery.RecoverAsync(ct);
        }

        MemoryTidyEntry.StepSettled settled = Assert.Single(MemoryTidyJournal.Read(folder).OfType<MemoryTidyEntry.StepSettled>());
        Assert.Equal(notDone, settled.NotDone);
        Assert.Equal(notDone ? [] : ["a.md"], settled.ChangedFiles);
        Assert.Equal(hasCopy && !notDone ? ["a.md"] : [], settled.CopiedFiles);
        Assert.Contains(listed, await File.ReadAllTextAsync(MemoryTidyList.PathIn(folder), ct), StringComparison.Ordinal);
        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>A step settled once is not settled again: a second settle appends nothing and the journal holds one settled entry.</summary>
    [Fact]
    public void Settle_AlreadySettledStep_IsNotSettledTwice()
    {
        using TidyRig rig = new("Nova");
        MemoryTidyEntry.StepStarted open = new(1, "agent:Nova", "rewrite", [new MemoryTidyEntry.StepFile("a.md", "0123456789ab")], "tidy");
        string folder = rig.SeedPass(Started, Open("a.md", "0123456789ab"), open);
        MemoryTidyJournal journal = new(folder);

        IReadOnlyList<MemoryTidyEntry.StepSettled> first = MemoryTidyRecovery.Settle(MemoryTidyJournal.Read(folder), journal, folder, static _ => null);
        IReadOnlyList<MemoryTidyEntry.StepSettled> second = MemoryTidyRecovery.Settle(MemoryTidyJournal.Read(folder), journal, folder, static _ => null);

        Assert.Single(first);
        Assert.Empty(second);
        _ = Assert.Single(MemoryTidyJournal.Read(folder).OfType<MemoryTidyEntry.StepSettled>());
    }

    /// <summary>A step that already has an outcome is left alone by settle.</summary>
    [Fact]
    public void Settle_StepWithAnOutcome_IsLeftAlone()
    {
        using TidyRig rig = new("Nova");
        string folder = rig.SeedPass(Started, Open("a.md", "0123456789ab"), AppliedStep(1, "a.md"));

        IReadOnlyList<MemoryTidyEntry.StepSettled> settled = MemoryTidyRecovery.Settle(MemoryTidyJournal.Read(folder), new MemoryTidyJournal(folder), folder, static _ => null);

        Assert.Empty(settled);
    }

    private static MemoryTidyEntry.StepStarted Open(string file, string version) => new(1, "agent:Nova", "rewrite", [new MemoryTidyEntry.StepFile(file, version)], "tidy");

    private static void WriteCopy(string passFolder)
    {
        string copy = Path.Combine(passFolder, "Agents", "Nova", "a.md");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(copy) ?? passFolder);
        File.WriteAllText(copy, "as it was");
    }

    private static MemoryTidyEntry.StepApplied AppliedStep(int step, string file) =>
        new(step, "agent:Nova", [file], [], ["Agents/Nova/" + file], [], [], false, null);

    /// <summary>Runs a real pass whose token is cancelled after it journalled an open step, so the pass's own <c>finally</c> settles it.</summary>
    private static async Task<string> StopWithOpenStepAsync(TidyRig rig, MemoryTidyEntry.StepStarted open, bool hasCopy)
    {
        using CancellationTokenSource stop = new();
        rig.Pass.AfterStartedForTests = folder =>
        {
            new MemoryTidyJournal(folder).Append(open);
            if (hasCopy)
            {
                WriteCopy(folder);
            }

            stop.Cancel();
            stop.Token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, stop.Token);

        Assert.Equal(MemoryTidyPassEnd.Stopped, end);
        return Path.Combine(rig.Backups.RootPath, Started);
    }
}

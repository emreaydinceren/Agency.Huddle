namespace Agency.Huddle.Tests.Memory;

using Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Pins the first step of every Memory Tidy pass, dropping old <c>reported.md</c> lines (Memory Tidy spec §6.6, §8.1 "Line dropping in the pass"):
/// the old lines go and the copy is in the backup, nothing to drop writes nothing, a concurrent <c>append</c> is not lost, no Helper is needed, and the
/// drop is in the journal and the list but raises no Feed item.
/// </summary>
public sealed class MemoryTidyReportedPruneTests
{
    private const string Head = "---\ndescription: What I have already told the Human, by date and Room. Read before reporting so nothing is said twice.\n---\n\n";
    private const string Old = "- 2026-10-01 · Nova · long ago\n- 2026-10-02 · Nova · also long ago\n";
    private const string Fresh = "- 2026-11-10 · Nova · last week\n";
    private const string PassId = "2026-11-16";

    /// <summary>The old lines go, every other line stays, and the file as it was is in the pass's backup folder, under the Agent's name.</summary>
    [Fact]
    public async Task RunAsync_OldReportedLines_AreDroppedAndTheOriginalIsInTheBackup()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        _ = rig.Seed("Nova", "reported.md", Head + Old + Fresh);
        string passFolder = Path.Combine(rig.Backups.RootPath, PassId);

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(Head + Fresh, await File.ReadAllTextAsync(rig.ReportedPath("Nova"), ct));
        Assert.Equal(Head + Old + Fresh, await File.ReadAllTextAsync(Path.Combine(passFolder, "Agents", "Nova", "reported.md"), ct));
    }

    /// <summary>A file with nothing older than the limit is left alone: no copy, no journal line, no list, no Feed item.</summary>
    [Fact]
    public async Task RunAsync_NothingToDrop_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        _ = rig.Seed("Nova", "reported.md", Head + Fresh);
        byte[] before = await File.ReadAllBytesAsync(rig.ReportedPath("Nova"), ct);
        string passFolder = Path.Combine(rig.Backups.RootPath, PassId);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(before, await File.ReadAllBytesAsync(rig.ReportedPath("Nova"), ct));
        Assert.False(Directory.Exists(Path.Combine(passFolder, "Agents")));
        Assert.DoesNotContain(MemoryTidyJournal.Read(passFolder), static entry => entry is MemoryTidyEntry.LinesDropped);
        Assert.False(File.Exists(MemoryTidyList.PathIn(passFolder)));
        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>An Agent's <c>append</c> between the read and the prune is not lost: the stale version skips the file, and the next pass takes it.</summary>
    [Fact]
    public async Task RunAsync_AppendBetweenReadAndPrune_IsNotLost()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        _ = rig.Seed("Nova", "reported.md", Head + Old + Fresh);
        const string appended = "- 2026-11-16 · Nova · said just now\n";
        rig.Pass.AfterReportedReadForTests = async file =>
        {
            await File.AppendAllTextAsync(file, appended, ct);
        };
        string passFolder = Path.Combine(rig.Backups.RootPath, PassId);

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(Head + Old + Fresh + appended, await File.ReadAllTextAsync(rig.ReportedPath("Nova"), ct));
        Assert.DoesNotContain(MemoryTidyJournal.Read(passFolder), static entry => entry is MemoryTidyEntry.LinesDropped);
        Assert.False(File.Exists(Path.Combine(passFolder, "Agents", "Nova", "reported.md")));
    }

    /// <summary>The prune needs no Helper: the pass is built from the catalog, the writer and the backups only, and every Agent is looked at, in Name order.</summary>
    [Fact]
    public async Task RunAsync_NoHelperAtAll_DropsEveryAgentsOldLinesInNameOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova", "Ember", "Quiet");
        _ = rig.Seed("Nova", "reported.md", Head + Old + Fresh);
        _ = rig.Seed("Ember", "reported.md", Head + Old);
        string passFolder = Path.Combine(rig.Backups.RootPath, PassId);

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.RunAtStart, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        MemoryTidyEntry.LinesDropped[] dropped = [.. MemoryTidyJournal.Read(passFolder).OfType<MemoryTidyEntry.LinesDropped>()];
        Assert.Equal(["Ember", "Nova"], dropped.Select(static entry => entry.Agent));
        Assert.All(dropped, static entry => Assert.Equal(2, entry.Count));
        Assert.Equal("Agents/Ember/reported.md", dropped[0].Copy);
        Assert.Equal(Head, await File.ReadAllTextAsync(rig.ReportedPath("Ember"), ct));
    }

    /// <summary>The drop is in the journal and in the pass's list, and the pass ends finished with no Feed item, because no topic file changed.</summary>
    [Fact]
    public async Task RunAsync_DroppedLines_AreInTheJournalAndTheListAndRaiseNoFeedItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new("Nova");
        _ = rig.Seed("Nova", "reported.md", Head + Old + Fresh);
        string passFolder = Path.Combine(rig.Backups.RootPath, PassId);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        IReadOnlyList<MemoryTidyEntry> entries = MemoryTidyJournal.Read(passFolder);
        MemoryTidyEntry.LinesDropped dropped = Assert.Single(entries.OfType<MemoryTidyEntry.LinesDropped>());
        Assert.Equal(("Nova", 2), (dropped.Agent, dropped.Count));
        Assert.Equal(new MemoryTidyEntry.PassFinished(false), entries[^1]);
        string list = await File.ReadAllTextAsync(MemoryTidyList.PathIn(passFolder), ct);
        Assert.Contains("reported.md", list, StringComparison.Ordinal);
        Assert.Empty(rig.Feed.Posts);
    }

    /// <summary>A <c>KeepDays</c> of zero turns the dropping off: the pass leaves the file alone.</summary>
    [Fact]
    public async Task RunAsync_KeepDaysZero_DropsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new(static team => team.Memory.Reported.KeepDays = 0, "Nova");
        _ = rig.Seed("Nova", "reported.md", Head + Old + Fresh);

        _ = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(Head + Old + Fresh, await File.ReadAllTextAsync(rig.ReportedPath("Nova"), ct));
    }
}

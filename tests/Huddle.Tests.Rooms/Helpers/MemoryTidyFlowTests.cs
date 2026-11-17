using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// Memory Tidy end to end (Memory Tidy spec Appendix A MT-25): one Agent's memory folder of twelve topic files goes through a real pass started on the
/// real runner, with a fake Helper process answering a merge, a rewrite and a describe. The files, the backup copies, the journal, the list, the one Feed
/// item, the History entries and the Agent's next Turn's File Changes are all read back.
/// </summary>
public sealed class MemoryTidyFlowTests
{
    private const string Agent = "Nova";
    private const string PricingBody = "Price is 5 euro per unit.";
    private const string PriceListBody = "Bulk price is 4 euro per unit above 100.";
    private const string MergedBody = "Price is 5 euro per unit; bulk price is 4 euro above 100 units.";
    private const string ShippingBody = "Shipping takes 7 days. Carrier is Acme. Labels are printed at the dock. Parcels are weighed first.";
    private const string RewrittenBody = "Carrier is Acme. Labels are printed at the dock. Parcels are weighed first.";
    private const string NotesBody = "Parking is behind the depot.";
    private const string NotesDescription = "Parking rules, read before parking";

    private const string Answer =
        """
        {"actions":[
        {"action":"merge","into":"pricing.md","from":["price-list.md"],"text":"Price is 5 euro per unit; bulk price is 4 euro above 100 units.","reason":"same topic"},
        {"action":"rewrite","file":"shipping.md","text":"Carrier is Acme. Labels are printed at the dock. Parcels are weighed first.","reason":"delivery.md replaced the 7 days"},
        {"action":"describe","file":"notes.md","description":"Parking rules, read before parking","reason":"no description"}]}
        """;

    /// <summary>
    /// A pass over a seeded folder merges, rewrites and describes: the files, the copies, the journal, the list, the one Notification and the History
    /// are right, and the Agent's next Turn lists the changed and the removed files as File Changes.
    /// </summary>
    [Fact]
    public async Task TidyNow_SeededFolder_MergesRewritesDescribesAndTheAgentSeesTheChanges()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new(static team => team.Memory.Tidy.MinFiles = 3, Agent);
        Seed(rig);
        string[] touched = ["pricing.md", "price-list.md", "shipping.md", "notes.md"];
        Dictionary<string, byte[]> originals = touched.ToDictionary(static file => file, file => File.ReadAllBytes(Path.Combine(rig.MemoryDir(Agent), file)), StringComparer.Ordinal);
        rig.Process.Text = Answer;
        SqliteTeamDirectory directory = new(rig.Options);
        await directory.InitializeAsync("You", ct);
        FileChangeTracker tracker = new(
            new FileStateStore(rig.Options, NullLogger<FileStateStore>.Instance),
            rig.Personas,
            directory,
            new WatchedFolderResolver(rig.Options, rig.Paths),
            rig.Options,
            rig.Paths,
            NullLogger<FileChangeTracker>.Instance,
            rig.MemoryPaths);
        await tracker.CommitAsync(Agent, "room-a", await tracker.CollectAsync(Agent, "room-a", [], ct), [], ct);
        using MemoryTidyRunner runner = new(rig.Pass, rig.Recovery, rig.Options, NullLogger<MemoryTidyRunner>.Instance, rig.Clock);
        await runner.StartAsync(ct);

        Task<MemoryTidyPassEnd>? started = runner.TryStart(MemoryTidyStart.TidyNow);
        Assert.NotNull(started);
        MemoryTidyPassEnd end = await started.WaitAsync(ct);
        await runner.StopAsync(ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        string folder = rig.MemoryDir(Agent);
        string pass = Path.Combine(rig.Backups.RootPath, "2026-11-16");
        AssertFiles(folder);
        AssertCopies(pass, originals);
        AssertJournal(pass);
        AssertList(pass);
        AssertFeed(rig, pass);
        AssertHistory(rig);
        CollectedChanges next = await tracker.CollectAsync(Agent, "room-a", [], ct);
        Assert.Equal(
            [
                (FileChangeKind.Deleted, Path.Combine(folder, "price-list.md")),
                (FileChangeKind.Changed, Path.Combine(folder, "notes.md")),
                (FileChangeKind.Changed, Path.Combine(folder, "pricing.md")),
                (FileChangeKind.Changed, Path.Combine(folder, "shipping.md")),
            ],
            [.. next.Report.Changes.Select(static change => (change.Kind, change.FullPath)).OrderBy(static pair => pair.Kind == FileChangeKind.Deleted ? 0 : 1).ThenBy(static pair => pair.FullPath, StringComparer.Ordinal)]);
        Assert.All(next.Report.Changes, static change => Assert.Null(change.ByYouRoomName));
    }

    private static void Seed(TidyRig rig)
    {
        _ = rig.Seed(Agent, "pricing.md", MemoryFileRules.Compose("Unit prices, read before quoting", PricingBody));
        _ = rig.Seed(Agent, "price-list.md", MemoryFileRules.Compose("Bulk prices, read before quoting", PriceListBody));
        _ = rig.Seed(Agent, "shipping.md", MemoryFileRules.Compose("Shipping times, read before promising a date", ShippingBody));
        _ = rig.Seed(Agent, "delivery.md", MemoryFileRules.Compose("Delivery times, read before promising a date", "Shipping now takes 3 days."));
        _ = rig.Seed(Agent, "notes.md", NotesBody);
        for (int index = 0; index < 7; index++)
        {
            _ = rig.Seed(Agent, $"topic{index}.md", MemoryFileRules.Compose($"Topic {index}, read before using", $"Body {index} of {Agent}."));
        }
    }

    private static void AssertFiles(string folder)
    {
        Assert.Equal(MemoryFileRules.Compose("Unit prices, read before quoting", MergedBody), File.ReadAllText(Path.Combine(folder, "pricing.md")));
        Assert.False(File.Exists(Path.Combine(folder, "price-list.md")));
        Assert.Equal(MemoryFileRules.Compose("Shipping times, read before promising a date", RewrittenBody), File.ReadAllText(Path.Combine(folder, "shipping.md")));
        string notes = File.ReadAllText(Path.Combine(folder, "notes.md"));
        Assert.Equal(NotesDescription, MemoryFileRules.DescriptionOf(notes));
        Assert.Contains(NotesBody, notes, StringComparison.Ordinal);
    }

    private static void AssertCopies(string pass, Dictionary<string, byte[]> originals)
    {
        foreach ((string file, byte[] bytes) in originals)
        {
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(pass, "Agents", Agent, file)));
        }
    }

    private static void AssertJournal(string pass)
    {
        MemoryTidyEntry[] entries = [.. MemoryTidyJournal.Read(pass)];
        Assert.Equal(3, entries.OfType<MemoryTidyEntry.StepStarted>().Count());
        Assert.Equal(3, entries.OfType<MemoryTidyEntry.StepApplied>().Count());
        string raw = File.ReadAllText(Path.Combine(pass, MemoryTidyJournal.FileName));
        Assert.DoesNotContain(PriceListBody, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(RewrittenBody, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(MergedBody, raw, StringComparison.Ordinal);
    }

    private static void AssertList(string pass)
    {
        string list = File.ReadAllText(MemoryTidyList.PathIn(pass));
        foreach (string name in new[] { "pricing.md", "price-list.md", "shipping.md", "notes.md" })
        {
            Assert.Contains(name, list, StringComparison.Ordinal);
        }

        Assert.Contains("Agents", list, StringComparison.Ordinal);
        Assert.Contains("2026-11-16", list, StringComparison.Ordinal);
    }

    private static void AssertFeed(TidyRig rig, string pass)
    {
        FeedMessage item = Assert.Single(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.MemoryTidied);
        MemoryTidiedPayload payload = Assert.IsType<MemoryTidiedPayload>(item.Payload);
        Assert.True(item.Notify);
        Assert.Equal((3, 0, 1, false), (payload.Tidied, payload.Dropped, payload.Folders, payload.Stopped));
        Assert.Equal(MemoryTidyList.PathIn(pass), payload.Path);
        Assert.Equal(payload.Path, item.TargetKey);
        Assert.DoesNotContain(rig.Feed.Posts, static post => post.SourceType == FeedSourceType.DocumentChanged);
    }

    private static void AssertHistory(TidyRig rig)
    {
        LibraryRootStore roots = new(rig.Options, rig.Paths, NullLogger<LibraryRootStore>.Instance);
        LibraryPathResolver resolver = new(roots, rig.Options, NullLogger<LibraryPathResolver>.Instance);
        DocHistoryStore history = new(rig.Options, NullLogger<DocHistoryStore>.Instance, rig.Feed);
        foreach (string file in new[] { "pricing.md", "shipping.md", "notes.md" })
        {
            Assert.True(resolver.TryResolveAbsolute(Path.Combine(rig.MemoryDir(Agent), file), out LibraryPath? path), file);
            DocHistoryEntry entry = Assert.Single(history.Read(path));
            Assert.Equal("Memory Tidy (Helper)", entry.Actor);
            Assert.Equal(DocActorKind.Helper, entry.ActorKind);
        }
    }
}

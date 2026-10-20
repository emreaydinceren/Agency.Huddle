using Agency.Huddle.App.Feed;

namespace Agency.Huddle.Tests.Feed;

/// <summary>Pins <see cref="FeedStore"/> on a real <c>team.db</c> (spec 6.1, 6.7, 8).</summary>
public sealed class FeedStoreTests
{
    private static readonly DateTimeOffset T0 = FeedTestSupport.T0;

    /// <summary>Opening the store twice on the same folder is harmless and keeps the rows.</summary>
    [Fact]
    public void Constructor_Twice_KeepsRows()
    {
        using TempDataDir dir = new();
        FeedStore first = new(dir.Options());
        _ = FeedTestSupport.Post(first, FeedTestSupport.TaskCreated());

        FeedStore second = new(dir.Options());

        Assert.Single(second.Page(new FeedQuery(null, 10, null, null)));
    }

    /// <summary>A row round-trips every column, including the three Notification timestamps and the actor kind.</summary>
    [Fact]
    public void Apply_Post_RoundTripsEveryColumn()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        FeedMessage message = FeedTestSupport.NeedsYou("GRO-7", "Growth", "Nova");
        FeedItem inserted = FeedTestSupport.Post(store, message, T0);
        _ = store.Apply([
            new MarkSeenCommand(inserted.Id, T0.AddMinutes(1)),
            new DismissCommand(inserted.Id, T0.AddMinutes(2)),
            new ResolveCommand("GRO-7", FeedSourceType.TaskNeedsYou, T0.AddMinutes(3)),
        ]);

        FeedItem item = store.Page(new FeedQuery(null, 10, null, null)).Single();

        Assert.Equal(inserted.Id, item.Id);
        Assert.Equal(T0, item.At);
        Assert.Equal(FeedSourceType.TaskNeedsYou, item.SourceType);
        Assert.Equal("GRO-7", item.TargetKey);
        Assert.Equal(new FeedActor(FeedActorKind.Agent, "Nova"), item.Actor);
        Assert.True(item.Notify);
        Assert.Equal(T0.AddMinutes(1), item.SeenAt);
        Assert.Equal(T0.AddMinutes(2), item.DismissedAt);
        Assert.Equal(T0.AddMinutes(3), item.ResolvedAt);
        Assert.Equal(message.Payload, item.Payload);
    }

    /// <summary>Newest first, and a page keyed by the lowest id shown never repeats or skips when rows arrive between pages.</summary>
    [Fact]
    public void Page_KeysetPaging_HasNoDuplicatesWhenRowsAreInsertedBetweenPages()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        for (int i = 1; i <= 5; i++)
        {
            _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-" + i));
        }

        IReadOnlyList<FeedItem> first = store.Page(new FeedQuery(null, 2, null, null));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-6"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-7"));
        IReadOnlyList<FeedItem> second = store.Page(new FeedQuery(first[^1].Id, 2, null, null));
        IReadOnlyList<FeedItem> third = store.Page(new FeedQuery(second[^1].Id, 2, null, null));

        List<string> targets = [.. first.Concat(second).Concat(third).Select(item => item.TargetKey)];
        Assert.Equal(["GRO-5", "GRO-4", "GRO-3", "GRO-2", "GRO-1"], targets);
    }

    /// <summary>The Teammate and Team filters match case-insensitively and combine.</summary>
    [Fact]
    public void Page_TeammateAndTeamFilters_MatchCaseInsensitivelyAndCombine()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1", "Growth", "Nova"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-2", "Growth", "Sage"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("SAL-1", "Sales", "Nova"));

        IReadOnlyList<FeedItem> nova = store.Page(new FeedQuery(null, 10, "nova", null));
        IReadOnlyList<FeedItem> growth = store.Page(new FeedQuery(null, 10, null, "GROWTH"));
        IReadOnlyList<FeedItem> both = store.Page(new FeedQuery(null, 10, "NOVA", "growth"));

        Assert.Equal(["SAL-1", "GRO-1"], nova.Select(item => item.TargetKey));
        Assert.Equal(["GRO-2", "GRO-1"], growth.Select(item => item.TargetKey));
        Assert.Equal("GRO-1", both.Single().TargetKey);
    }

    /// <summary>An unknown wire name or a payload that is not JSON reads as a null payload, with and without a Team filter, never a throw.</summary>
    [Fact]
    public void Page_UnknownTypeAndBadJson_ReadAsNullPayload()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        FeedTestSupport.InsertRaw(dir.Path, "from-the-future", "{\"v\":9,\"actorKind\":\"agent\",\"data\":{}}");
        FeedTestSupport.InsertRaw(dir.Path, "task-created", "not json at all");

        IReadOnlyList<FeedItem> items = store.Page(new FeedQuery(null, 10, null, null));
        IReadOnlyList<FeedItem> filtered = store.Page(new FeedQuery(null, 10, null, "Growth"));

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Null(item.Payload));
        Assert.Null(items.Single(item => item.TargetKey == "raw" && item.SourceType is null).SourceType);
        Assert.Empty(filtered);
    }

    /// <summary>The panel lists Notifications only, newest first, without dismissed ones, up to the limit.</summary>
    [Fact]
    public void Notifications_ExcludesActivityAndDismissed()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1"));
        FeedItem a = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-2"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-3"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-4"));
        _ = store.Apply([new DismissCommand(a.Id, T0)]);

        IReadOnlyList<FeedItem> all = store.Notifications(10);
        IReadOnlyList<FeedItem> limited = store.Notifications(1);

        Assert.Equal(["GRO-4", "GRO-3"], all.Select(item => item.TargetKey));
        Assert.Equal("GRO-4", limited.Single().TargetKey);
    }

    /// <summary>ExcludeDismissed leaves dismissed Notifications out before the limit is applied, so older undismissed ones fill the page.</summary>
    [Fact]
    public void Page_ExcludeDismissed_AppliesBeforeTheLimit()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        FeedItem older = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-1"));
        FeedItem dismissedA = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-2"));
        FeedItem dismissedB = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-3"));
        _ = store.Apply([new DismissCommand(dismissedA.Id, T0), new DismissCommand(dismissedB.Id, T0)]);

        IReadOnlyList<FeedItem> kept = store.Page(new FeedQuery(null, 2, null, null, FeedCategory.NeedsYou, ExcludeDismissed: true));
        IReadOnlyList<FeedItem> everything = store.Page(new FeedQuery(null, 2, null, null, FeedCategory.NeedsYou));

        Assert.Equal([older.Id], kept.Select(item => item.Id));
        Assert.Equal([dismissedB.Id, dismissedA.Id], everything.Select(item => item.Id));
    }

    /// <summary>The unseen count is Notifications that are unseen, undismissed and unresolved.</summary>
    [Fact]
    public void UnseenCount_CountsOnlyUnseenUndismissedUnresolvedNotifications()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1"));
        FeedItem seen = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-2"));
        FeedItem dismissed = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-3"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-4"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-5"));
        Assert.Equal(4, store.UnseenCount());

        _ = store.Apply([
            new MarkSeenCommand(seen.Id, T0),
            new DismissCommand(dismissed.Id, T0),
            new ResolveCommand("GRO-5", FeedSourceType.TaskNeedsYou, T0),
        ]);

        Assert.Equal(1, store.UnseenCount());
    }

    /// <summary>Unresolved lists the unresolved Notifications of one type, dismissed ones included.</summary>
    [Fact]
    public void Unresolved_OneType_ListsOnlyUnresolvedNotificationsOfThatType()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        FeedItem keep = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-1"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-2"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.RoomMention("room-1"));
        _ = store.Apply([new ResolveCommand("GRO-2", FeedSourceType.TaskNeedsYou, T0), new DismissCommand(keep.Id, T0)]);

        IReadOnlyList<FeedItem> unresolved = store.Unresolved(FeedSourceType.TaskNeedsYou);

        Assert.Equal("GRO-1", unresolved.Single().TargetKey);
    }

    /// <summary>Related returns items about the item's own target, or about an extra target, newest first up to the limit, the item itself included.</summary>
    [Fact]
    public void Related_ItemTargetAndExtraTargets_NewestFirstWithinLimit()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        FeedItem first = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("GRO-1"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.TaskCreated("OTHER-1"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.NeedsYou("GRO-1"));
        _ = FeedTestSupport.Post(store, FeedTestSupport.RoomMention("room-9"));

        IReadOnlyList<FeedItem> own = store.Related(first, [], 10);
        IReadOnlyList<FeedItem> withRoom = store.Related(first, ["room-9"], 10);
        IReadOnlyList<FeedItem> limited = store.Related(first, ["room-9"], 2);

        Assert.Equal(["GRO-1", "GRO-1"], own.Select(item => item.TargetKey));
        Assert.Equal(["room-9", "GRO-1", "GRO-1"], withRoom.Select(item => item.TargetKey));
        Assert.Equal(2, limited.Count);
        Assert.Equal(first.Id, own[^1].Id);
    }

    /// <summary>The Tab's seen mark starts at zero and only ever moves forward.</summary>
    [Fact]
    public void TabSeenUpTo_StartsAtZeroAndIsMonotonic()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        Assert.Equal(0, store.TabSeenUpTo());

        _ = store.Apply([new MarkTabSeenCommand(9, T0)]);
        _ = store.Apply([new MarkTabSeenCommand(4, T0)]);
        long afterLower = store.TabSeenUpTo();
        _ = store.Apply([new MarkTabSeenCommand(12, T0)]);

        Assert.Equal(9, afterLower);
        Assert.Equal(12, store.TabSeenUpTo());
    }

    /// <summary>Every Category count equals the length of that Category's unpaged list, with and without filters, and an unknown row counts only in All.</summary>
    [Fact]
    public void Counts_EveryCategory_EqualsTheUnpagedListLength()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        int n = 0;
        foreach (FeedSourceType type in Enum.GetValues<FeedSourceType>())
        {
            n++;
            _ = FeedTestSupport.Post(store, FeedTestSupport.Of(type, "target-" + n, n % 2 == 0 ? "Nova" : "Sage", n % 3 == 0 ? "Sales" : "Growth"));
        }

        _ = store.Apply([new ResolveCommand("target-3", FeedSourceType.TaskNeedsYou, T0)]);
        FeedTestSupport.InsertRaw(dir.Path, "from-the-future", "{}", notify: true);

        foreach ((string? teammate, string? team) in new (string?, string?)[] { (null, null), ("Nova", null), (null, "Growth"), ("Sage", "Sales") })
        {
            FeedCounts counts = store.Counts(teammate, team);

            Assert.Equal(Length(store, FeedCategory.All, teammate, team), counts.All);
            Assert.Equal(Length(store, FeedCategory.NeedsYou, teammate, team), counts.NeedsYou);
            Assert.Equal(Length(store, FeedCategory.Tasks, teammate, team), counts.Tasks);
            Assert.Equal(Length(store, FeedCategory.Documents, teammate, team), counts.Documents);
            Assert.Equal(Length(store, FeedCategory.Rooms, teammate, team), counts.Rooms);
            Assert.Equal(Length(store, FeedCategory.Gates, teammate, team), counts.Gates);
        }

        FeedCounts unfiltered = store.Counts(null, null);
        Assert.Equal(Enum.GetValues<FeedSourceType>().Length + 1, unfiltered.All);
        Assert.Equal(unfiltered.All - 1, unfiltered.Tasks + unfiltered.Documents + unfiltered.Rooms + unfiltered.Gates);
        Assert.True(unfiltered.NeedsYou > 0);
    }

    /// <summary>Documents carry no Team, so with a Team chosen the Documents count is zero.</summary>
    [Fact]
    public void Counts_TeamFilter_DocumentsAreZero()
    {
        using TempDataDir dir = new();
        FeedStore store = new(dir.Options());
        _ = FeedTestSupport.Post(store, FeedTestSupport.Of(FeedSourceType.DocumentChanged, "C:/lib/a.md"));

        Assert.Equal(1, store.Counts(null, null).Documents);
        Assert.Equal(0, store.Counts(null, "Growth").Documents);
    }

    private static int Length(FeedStore store, FeedCategory category, string? teammate, string? team) =>
        store.Page(new FeedQuery(null, 1000, teammate, team, category)).Count;
}

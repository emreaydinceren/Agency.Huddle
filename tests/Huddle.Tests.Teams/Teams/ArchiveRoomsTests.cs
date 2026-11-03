using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// What happens to a Team's Room when the Team is archived (Spec 6.7 steps 7 and 8, corrections-12 items 12 and 17):
/// the archive service archives the Room directly, the <see cref="TeamRoomReconciler"/> must agree with it and never
/// undo or repeat it, and the Feed gets one "Team Home archived" item. The Archive and Unarchive paths themselves are
/// pinned in <see cref="ArchiveTeamTests"/> and <see cref="UnarchiveTeamTests"/>; the fixture is theirs.
/// A Team owns one Room (<c>room_teams.team</c> is unique), so "its Rooms" is that one Room.
/// </summary>
public sealed class ArchiveRoomsTests
{
    private const string Home = "Home";

    /// <summary>The last Team leaving the catalog on a catalog change archives its Room through the reconciler's own loop.</summary>
    [Fact]
    public async Task CatalogRaise_LastTeamGone_ReconcilerArchivesItsRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        using Harness h = new(f);
        f.Catalog.Teams = [new TeamSummary(Home, [], ["Nova", "Ada"], HasFolder: true)];
        Room room = await f.CreateRoomAsync(Home, ct);
        await h.StartAsync(ct);
        Assert.False((await RoomAsync(f, room.Id, ct)).Archived);

        f.Catalog.Teams = [];
        f.Catalog.Raise();
        await h.Reconciler.LastRun;

        Assert.True((await RoomAsync(f, room.Id, ct)).Archived);
    }

    /// <summary>After the archive service has archived the Room, the reconciler's pass over the empty catalog neither archives it again nor changes it.</summary>
    [Fact]
    public async Task ArchiveThenCatalogRaise_ReconcilerDoesNotArchiveTwice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        using Harness h = new(f);
        f.Catalog.Teams = [new TeamSummary(Home, [], ["Nova", "Ada"], HasFolder: true)];
        Room room = await f.CreateRoomAsync(Home, ct);
        await h.StartAsync(ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        int before = h.RoomsChanged;

        f.Catalog.Teams = [];
        f.Catalog.Raise();
        await h.Reconciler.LastRun;

        Assert.True((await RoomAsync(f, room.Id, ct)).Archived);
        // ChatService.SetRoomArchivedAsync publishes RoomsChanged; a second archive of the same Room would raise it again.
        Assert.Equal(before, h.RoomsChanged);
        Assert.Equal([room.Id], f.ReadArchivedTeamFile().ArchivedRooms);
    }

    /// <summary>A fresh reconciler (the app restarted) leaves an archived Team's Room archived, also when the Team's label comes back: only unarchive restores it.</summary>
    [Fact]
    public async Task Restart_ArchivedTeamRoom_StaysArchivedAndIsNotUnarchivedByTheReconciler()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        using Harness h = new(f);
        Room room = await f.CreateRoomAsync(Home, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        TeamSummary other = new("Other", [], ["Nova"], HasFolder: true);
        f.Catalog.Teams = [other];

        await h.StartAsync(ct);
        Assert.True((await RoomAsync(f, room.Id, ct)).Archived);
        f.Catalog.Teams = [new TeamSummary(Home, [], ["Nova"], HasFolder: true), other];
        f.Catalog.Raise();
        await h.Reconciler.LastRun;

        Assert.True((await RoomAsync(f, room.Id, ct)).Archived);
    }

    /// <summary>A Room the Human archived before the Team was archived is not recorded, so unarchiving the Team leaves it archived.</summary>
    [Fact]
    public async Task Unarchive_RoomArchivedByHumanBeforehand_StaysArchived()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);
        Room room = await f.CreateRoomAsync(Home, ct);
        await f.Directory.SetRoomArchivedAsync(room.Id, true, ct);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        Assert.Empty(f.ReadArchivedTeamFile().ArchivedRooms);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];

        ArchiveOutcome outcome = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.True(outcome.Done);
        Assert.True((await RoomAsync(f, room.Id, ct)).Archived);
    }

    /// <summary>Archiving a Team posts one Feed item, "Team Home archived", about the Team, as Huddle and not as a Notification.</summary>
    [Fact]
    public async Task ArchiveTeamAsync_Done_PostsTeamArchivedToTheFeed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);

        _ = await f.Service.ArchiveTeamAsync(Home, ct);

        FeedMessage post = Assert.Single(f.Feed.Posts);
        Assert.Equal(FeedSourceType.TeamArchived, post.SourceType);
        Assert.Equal(Home, post.TargetKey);
        Assert.Equal(FeedActor.Huddle, post.Actor);
        Assert.False(post.Notify);
        Assert.Equal("Team Home archived", Assert.IsType<TeamArchivedPayload>(post.Payload).Message);
    }

    /// <summary>A refused archive posts nothing, and unarchiving posts no further item (the Spec names none).</summary>
    [Fact]
    public async Task Feed_RefusedArchiveAndUnarchive_PostNothingMore()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ArchiveTeamTests.Fixture f = await ArchiveTeamTests.Fixture.CreateAsync(labelOnly: false, ct);

        ArchiveOutcome refused = await f.Service.ArchiveTeamAsync("Nowhere", ct);
        Assert.False(refused.Done);
        Assert.Empty(f.Feed.Posts);
        _ = await f.Service.ArchiveTeamAsync(Home, ct);
        f.Catalog.Teams = [new TeamSummary("Other", [], ["Nova"], HasFolder: true)];
        _ = await f.Service.UnarchiveTeamAsync(Home, ct);

        Assert.Single(f.Feed.Posts);
    }

    private static async Task<Room> RoomAsync(ArchiveTeamTests.Fixture f, string id, CancellationToken ct)
    {
        Room? room = await f.Directory.GetRoomAsync(id, ct);
        Assert.NotNull(room);
        return room;
    }

    /// <summary>A reconciler over the fixture's directory and catalog, with a chat service of its own and a count of Room change signals.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly TempDataDir dir = new();
        private int roomsChanged;

        public Harness(ArchiveTeamTests.Fixture f)
        {
            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            events.RoomsChanged += () => Interlocked.Increment(ref this.roomsChanged);
            ChatService chat = new(
                f.Directory,
                this.dir.CreateChatStore(),
                events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(events),
                new QuestionStore(events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = f.Catalog,
            };
            this.Reconciler = new TeamRoomReconciler(f.Directory, chat, f.Catalog, events, NullLogger<TeamRoomReconciler>.Instance);
        }

        public TeamRoomReconciler Reconciler { get; }

        /// <summary>Gets how many times the Rooms changed since the harness was built.</summary>
        public int RoomsChanged => Volatile.Read(ref this.roomsChanged);

        /// <summary>Subscribes the reconciler and waits for its first run.</summary>
        /// <param name="ct">Cancels the start.</param>
        public async Task StartAsync(CancellationToken ct)
        {
            await this.Reconciler.StartAsync(ct);
            await this.Reconciler.LastRun;
        }

        public void Dispose()
        {
            this.Reconciler.Dispose();
            this.dir.Dispose();
        }
    }
}

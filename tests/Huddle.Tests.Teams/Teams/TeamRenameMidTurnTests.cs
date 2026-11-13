using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Agents;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="TeamRenameService.MidTurnMembersAsync"/>: which members of a Team are inside a Turn right now, read from
/// the real <see cref="TurnActivity"/> through the Room database's user ids.
/// </summary>
public sealed class TeamRenameMidTurnTests
{
    /// <summary>Only the member with a Turn open is named, in member order, skipping a member the directory does not know and matching names ignoring case.</summary>
    [Fact]
    public async Task MidTurnMembersAsync_OneBusyMember_NamesOnlyThatMemberInMemberOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        TurnActivity activity = new();
        using Rig rig = await Rig.CreateAsync(activity, ["Ghost", "ADA", "nova", "Bo"], ct);
        User? ada = await rig.Directory.FindUserByNameAsync("Ada", ct);
        User? bo = await rig.Directory.FindUserByNameAsync("Bo", ct);
        Assert.NotNull(ada);
        Assert.NotNull(bo);

        Assert.Empty(await rig.Service.MidTurnMembersAsync("Marketing", ct));

        activity.Begin(bo.Id, "room-1");
        activity.Begin(ada.Id, "room-1");
        Assert.Equal(["ADA", "Bo"], await rig.Service.MidTurnMembersAsync("Marketing", ct));

        activity.End(ada.Id, "room-1");
        activity.End(bo.Id, "room-1");
        Assert.Empty(await rig.Service.MidTurnMembersAsync("Marketing", ct));
    }

    /// <summary>With no Turn activity registered the service reports nobody busy.</summary>
    [Fact]
    public async Task MidTurnMembersAsync_NoTurnActivity_ReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(null, ["Ada", "Nova", "Bo"], ct);

        Assert.Empty(await rig.Service.MidTurnMembersAsync("Marketing", ct));
    }

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly LibraryFileServiceFixture library;
        private readonly IDisposable[] owned;

        private Rig(TempDataDir dir, LibraryFileServiceFixture library, SqliteTeamDirectory directory, TeamRenameService service, IDisposable[] owned)
        {
            this.owned = owned;
            this.dir = dir;
            this.library = library;
            this.Directory = directory;
            this.Service = service;
        }

        public SqliteTeamDirectory Directory { get; }

        public TeamRenameService Service { get; }

        public static async Task<Rig> CreateAsync(TurnActivity? activity, string[] members, CancellationToken ct)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(dir.Path);
            PersonaStore personas = dir.CreatePersonaStore();
            FakeTeamCatalog catalog = new();
            TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Ada", null, null, string.Empty);
            Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam("Marketing", file).Outcome);
            catalog.Teams = [new TeamSummary("Marketing", [], members, HasFolder: true) { Manager = "Ada" }];
            SqliteTeamDirectory directory = new(dir.Options());
            await directory.InitializeAsync("You", ct);
            foreach (string agent in new[] { "Ada", "Nova", "Bo" })
            {
                _ = await directory.UpsertAgentUserAsync(agent, null, ct);
            }

            RoomEvents events = new(NullLogger<RoomEvents>.Instance);
            ChatService chat = new(
                directory,
                dir.CreateChatStore(),
                events,
                new FakeMentionAliasSource { Aliases = [] },
                TestOptions.Create(),
                new ProposalStore(events),
                new QuestionStore(events),
                NullLogger<ChatService>.Instance)
            {
                TeamCatalog = catalog,
            };
            TaskStore tasks = TestTaskStore.CreateTaskStore(dir, personas);
            TeamRoomReconciler reconciler = new(directory, chat, catalog, events, NullLogger<TeamRoomReconciler>.Instance);
            TeamRenameService service = new(
                library.RootStore,
                catalog,
                new TeamMembership(personas, catalog, folders, RoleFixtures.Types(dir)),
                folders,
                tasks,
                new TaskIdAllocator(dir.Options()),
                new CadenceStateStore(dir.Options()),
                personas,
                directory,
                new PhysicalArchiveFileSystem(),
                new TeamRenameStore(dir.Options()),
                reconciler,
                [],
                new TeamOperationGate(),
                NullLogger<TeamRenameService>.Instance,
                turnActivity: activity);
            return new Rig(dir, library, directory, service, [reconciler, tasks]);
        }

        public void Dispose()
        {
            foreach (IDisposable item in this.owned)
            {
                item.Dispose();
            }

            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}

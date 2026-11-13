using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Utilities;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>
/// Seeds the scenario into a temp folder and reads it back with Huddle's own loaders. These tests fail when the
/// app and <c>agents/DataSeeding.md</c> drift apart, which is the whole reason the seeder loads through the app.
/// Seeder spec §7.
/// </summary>
public sealed class SeedRoundTripTests
{
    private static readonly string[] SavedViewIds =
        ["platform-board", "growth-by-project", "my-open-work", "blocked-work", "awaiting-review", "support-urgent", "unassigned", "current-sprint", "closed-work", "home-this-week", "who-owns-what", "home-board"];

    /// <summary>SEED-RT-1: Every Agent loads, none is rejected, and Team membership survives.</summary>
    [Fact]
    public async Task Agents_AllLoadWithoutRejections()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        var options = seed.Options();

        using PersonaStore store = new(new AgentPaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);

        Assert.Empty(store.RejectedFiles);
        Assert.Equal(plan.Agents.Count, store.Entries.Count);
        Assert.All(plan.Agents, seeded =>
        {
            PersonaEntry loaded = Assert.Single(store.Entries, e => e.Name == seeded.Name);
            Assert.Equal(seeded.Alias, loaded.Alias);
            Assert.Equal(seeded.Title, loaded.Title);
            Assert.Equal(seeded.Teams.Order(StringComparer.OrdinalIgnoreCase), loaded.Teams.Order(StringComparer.OrdinalIgnoreCase));
        });
    }

    /// <summary>SEED-RT-2: Every Task loads, none is rejected, the Change log dates round-trip, and start-up reconciliation adds nothing.</summary>
    [Fact]
    public async Task Tasks_AllLoadAndKeepTheirBackdatedDates()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        var options = seed.Options();
        using PersonaStore personas = new(new AgentPaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);

        using TaskStore store = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        Assert.Empty(store.RejectedFiles);
        Assert.Equal(plan.Tasks.Count, store.All.Count);
        Assert.Equal(plan.Tasks.Count(t => t.Closed), store.All.Count(t => t.Location.Closed));
        Assert.DoesNotContain(store.All, t => t.ChangeLog.Any(e => e.Summary.StartsWith("edited outside Huddle", StringComparison.Ordinal)));
        Assert.All(plan.Tasks, seeded =>
        {
            TaskItem loaded = Assert.Single(store.All, t => t.Id.ToString() == seeded.Id);
            IReadOnlyList<TaskLogLine> expected = TaskFileWriter.BuildLog(seeded, plan.Today);
            Assert.Equal(expected[0].At, loaded.Created);
            Assert.Equal(expected[^1].At, loaded.Updated);
            Assert.Equal(seeded.Location.Team, loaded.Location.Team);
            Assert.Equal(seeded.Location.Project, loaded.Location.Project);
            Assert.Equal(seeded.Closed, loaded.Location.Closed);
            Assert.Equal(seeded.Assignee, loaded.Assignee);
            Assert.Equal(seeded.Closed, loaded.ClosedAt is not null);
            Assert.Equal(seeded.BlockedBy, loaded.BlockedBy.Select(b => b.ToString()));
            Assert.Equal(seeded.Parent, loaded.Parent?.ToString());
            Assert.Equal(seeded.DueOffset is { } due ? plan.Today.AddDays(due) : (DateOnly?)null, loaded.DueDate);
        });
    }

    /// <summary>SEED-RT-3: A Task created from a Room carries that Room's real id.</summary>
    [Fact]
    public async Task Tasks_OriginPointsAtTheSeededRoom()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        var options = seed.Options();
        using PersonaStore personas = new(new AgentPaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
        using TaskStore store = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        TaskItem task = Assert.Single(store.All, t => t.Id.ToString() == "SUPP-0005");

        Assert.Equal(seed.Summary.Ids.RoomIds[SoftwareCoRooms.IncidentWarRoom], task.OriginRoomId);
    }

    /// <summary>SEED-RT-4: The Skill loads for Alan and the Skill store reports no problems with it.</summary>
    [Fact]
    public async Task Skills_LoadWithoutIssues()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        using SkillStore store = new(seed.Options(), NullLogger<SkillStore>.Instance);

        Assert.NotNull(store.Get(SoftwareCoAgents.PostmortemSkill));
        Assert.DoesNotContain(store.Issues, i => i.Skill == SoftwareCoAgents.PostmortemSkill);
    }

    /// <summary>SEED-RT-4: Every saved View in <c>views.json</c> is valid, and none is dropped.</summary>
    [Fact]
    public async Task Views_AllLoadWithoutErrors()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        using ViewStore store = new(seed.Options(), NullLogger<ViewStore>.Instance);

        Assert.Null(store.LoadError);
        Assert.Empty(store.InvalidViews);
        Assert.All(SavedViewIds, id => Assert.NotNull(store.Get(id)));
    }

    /// <summary>SEED-RT-5: The database holds the Human, every Agent under its seeded id, and the Rooms with their members, names and archive flag.</summary>
    [Fact]
    public async Task Database_HoldsUsersAndRooms()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        SqliteTeamDirectory directory = new(seed.Options());

        User human = await directory.GetHumanAsync(ct);
        IReadOnlyList<Room> rooms = await directory.GetRoomsAsync(ct);

        Assert.Equal("You", human.Name);
        foreach (SeedAgent agent in plan.Agents)
        {
            User? user = await directory.FindUserByNameAsync(agent.Name, ct);
            Assert.Equal(seed.Summary.Ids.UserIds[agent.Name], user?.Id);
        }

        Assert.Equal(plan.Rooms.Count, rooms.Count);
        foreach (SeedRoom seeded in plan.Rooms)
        {
            Room room = Assert.Single(rooms, r => r.Id == seed.Summary.Ids.RoomIds[seeded.Key]);
            IReadOnlyList<User> members = await directory.GetRoomMembersAsync(room.Id, ct);
            Assert.Equal(seeded.Name, room.Name);
            Assert.Equal(seeded.Archived, room.Archived);
            Assert.Equal(seeded.Members.Count + 1, members.Count);
            Assert.Contains(members, m => m.Id == KnownIds.Human);
        }
    }

    /// <summary>SEED-RT-5: Each transcript reads back in full, in order, from its members.</summary>
    [Fact]
    public async Task Transcripts_ReadBackInOrderFromTheRoomMembers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        FileChatStore chat = new(seed.Options(), NullLogger<FileChatStore>.Instance);

        foreach (SeedRoom seeded in seed.Summary.Plan.Rooms)
        {
            string roomId = seed.Summary.Ids.RoomIds[seeded.Key];
            IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(roomId, ct);

            Assert.Equal(seeded.Messages.Count, messages.Count);
            Assert.Equal(messages.OrderBy(m => m.Timestamp).Select(m => m.Id), messages.Select(m => m.Id));
            Assert.Equal(messages.Count, messages.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count());
            string[] allowed = [KnownIds.Human, .. seeded.Members.Select(m => seed.Summary.Ids.UserIds[m])];
            Assert.All(messages, m => Assert.Contains(m.SenderId, allowed));
            string file = Path.Combine(seed.DataDir, "rooms", roomId + ".jsonl");
            Assert.EndsWith("\n", await File.ReadAllTextAsync(file, ct), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// SEED-AV-4 and -6: every Agent's stored avatar is a copy of its library picture with a palette background no other Agent
    /// has, read back through the app's <see cref="AvatarStore"/>; the Human keeps the label avatar.
    /// </summary>
    [Fact]
    public async Task Avatars_EveryAgentsImageIsTheLibraryPicture()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        using AvatarStore store = new(seed.Options(), NullLogger<AvatarStore>.Instance);
        HashSet<string> palette = new(AvatarPalette.Colours.Select(static c => c.ToString(MudColorOutputFormats.Hex)), StringComparer.OrdinalIgnoreCase);
        List<string> backgrounds = [];

        foreach (SeedAgent agent in seed.Summary.Plan.Agents)
        {
            Avatar avatar = store.Get(agent.Name);
            Assert.NotNull(avatar.Image);
            Assert.NotNull(avatar.Background);
            Assert.Contains(avatar.Background, palette);
            backgrounds.Add(avatar.Background);
            byte[] stored = await File.ReadAllBytesAsync(Path.Combine(store.ImageDirectory, avatar.Image), ct);
            byte[] library = await File.ReadAllBytesAsync(Path.Combine(AvatarLibraryLocator.BundledDir, agent.AvatarFile), ct);
            Assert.Equal(library, stored);
        }

        Assert.Equal(backgrounds.Count, backgrounds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("ME", store.Get(seed.Summary.Plan.HumanName).Label);
    }

    /// <summary>SEED-AV-5: every Team has a <c>_team.md</c> the app parses, of its seeded type (blank unless named) with its seeded manager, and a palette colour no other Team has.</summary>
    [Fact]
    public async Task Teams_EachHasItsOwnAvatarColour()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        HashSet<string> palette = new(AvatarPalette.Colours.Select(static c => c.ToString(MudColorOutputFormats.Hex)), StringComparer.OrdinalIgnoreCase);
        List<string> colours = [];

        foreach (SeedTeam team in seed.Summary.Plan.Teams)
        {
            string text = await File.ReadAllTextAsync(Path.Combine(seed.DataDir, "Teams", team.Name, TeamPaths.TeamFileName), ct);
            Assert.True(TeamFile.TryParse(text, out TeamFile? file));
            Assert.Equal(team.TeamType ?? $"{BlankTeamType.Id}@{BlankTeamType.Manifest.Version}", file.TeamType.ToString());
            Assert.Equal(team.Manager, file.Manager);
            Assert.True(file.Onboarded);
            Assert.NotNull(file.Avatar.Background);
            Assert.Contains(file.Avatar.Background, palette);
            colours.Add(file.Avatar.Background);
        }

        Assert.Equal(colours.Count, colours.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// SEED-SC-16, SEED-SC-17: the archive on disk is what <c>ArchiveService</c> leaves. <c>Archive/Legacy Site/_team.md</c> parses
    /// with the roster and exactly the ids of the Team's Rooms, no manager and a colour no live Team has; no <c>Teams/Legacy Site</c>
    /// exists; Growth's Spring Promo sits under <c>Archive/Growth/</c> with no team file there; and every archived Room is stored
    /// archived with its Team and Project.
    /// </summary>
    [Fact]
    public async Task Archive_IsWhatTheArchiveServiceLeaves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        SeedArchivedTeam legacy = Assert.Single(plan.ArchivedTeams);
        string archive = Path.Combine(seed.DataDir, "Archive");

        string text = await File.ReadAllTextAsync(Path.Combine(archive, legacy.Name, TeamPaths.TeamFileName), ct);
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        Assert.Equal(legacy.Members, file.ArchivedMembers);
        string[] roomIds = [.. plan.Rooms.Where(r => r.Team == legacy.Name).Select(r => seed.Summary.Ids.RoomIds[r.Key])];
        Assert.Equal(roomIds.Order(StringComparer.Ordinal), file.ArchivedRooms.Order(StringComparer.Ordinal));
        Assert.Null(file.Manager);
        Assert.False(Directory.Exists(Path.Combine(seed.DataDir, "Teams", legacy.Name)));
        Assert.All(legacy.Projects, p => Assert.True(Directory.Exists(Path.Combine(archive, legacy.Name, p, TeamNames.MemoryFolder))));
        string[] liveColours = [.. plan.Teams.Select(t => TeamFile.TryParse(File.ReadAllText(Path.Combine(seed.DataDir, "Teams", t.Name, TeamPaths.TeamFileName)), out TeamFile? f) ? f.Avatar.Background ?? string.Empty : string.Empty)];
        Assert.DoesNotContain(file.Avatar.Background, liveColours, StringComparer.OrdinalIgnoreCase);

        Assert.True(Directory.Exists(Path.Combine(archive, "Growth", SoftwareCoArchive.SpringPromo)));
        Assert.False(File.Exists(Path.Combine(archive, "Growth", TeamPaths.TeamFileName)));
        Assert.False(Directory.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", SoftwareCoArchive.SpringPromo)));

        SqliteTeamDirectory directory = new(seed.Options());
        IReadOnlyList<Room> rooms = await directory.GetRoomsAsync(ct);
        foreach (SeedRoom seeded in plan.Rooms.Where(r => r.Archived && r.Team is not null))
        {
            Room room = Assert.Single(rooms, r => r.Id == seed.Summary.Ids.RoomIds[seeded.Key]);
            Assert.True(room.Archived);
            Assert.Equal(seeded.Team, room.Team);
            Assert.Equal(seeded.Project, room.Project);
        }
    }

    /// <summary>
    /// SEED-SC-16, SEED-SC-17: the app's own <see cref="ArchiveService"/>, built over the seeded folder, leaves the archive alone at
    /// start-up (<c>RecoverAsync</c>), lists Legacy Site and Spring Promo as the Archived page does, and restores both: Legacy Site's
    /// folder returns to <c>Teams/</c>, its roster gets the label back and both its Rooms are open again; Spring Promo returns under
    /// <c>Teams/Growth/</c>. Nothing else in the archive is left behind.
    /// </summary>
    [Fact]
    public async Task Archive_RestoresThroughTheAppsArchiveService()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        SeedArchivedTeam legacy = Assert.Single(plan.ArchivedTeams);
        var options = seed.Options();
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(seed.DataDir);
        using PersonaStore personas = new(new AgentPaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
        using TaskStore tasks = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
        FakeTeamCatalog catalog = new()
        {
            Teams = [.. plan.Teams.Select(t => new TeamSummary(t.Name, t.Projects, [.. plan.Agents.Where(a => a.Teams.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).Select(a => a.Name)], HasFolder: true) { Manager = t.Manager })],
        };
        TeamFolderProvisioner folders = new(personas, library.RootStore, library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog);
        TeamMembership membership = new(personas, catalog, folders, new TeamTypeCatalog(options, NullLogger<TeamTypeCatalog>.Instance));
        SqliteTeamDirectory directory = new(options);
        using ArchiveService archive = new(library.RootStore, catalog, membership, folders, tasks, new CadenceStateStore(options), personas, directory, new PhysicalArchiveFileSystem(), new RecordingFeedPublisher(), NullLogger<ArchiveService>.Instance);

        await archive.RecoverAsync(ct);
        Assert.Equal([legacy.Name], archive.ArchivedTeams());
        Assert.Equal([SoftwareCoArchive.SpringPromo], archive.ArchivedProjects("Growth"));
        Assert.Equal(legacy.Projects, archive.ArchivedProjects(legacy.Name));

        ArchiveOutcome team = await archive.UnarchiveTeamAsync(legacy.Name, ct);
        ArchiveOutcome project = await archive.UnarchiveProjectAsync("Growth", SoftwareCoArchive.SpringPromo, ct);

        Assert.True(team.Done, team.Refusal);
        Assert.Empty(team.Skipped);
        Assert.True(project.Done, project.Refusal);
        Assert.Empty(archive.ArchivedTeams());
        Assert.Empty(archive.ArchivedProjects("Growth"));
        Assert.All(legacy.Projects, p => Assert.True(Directory.Exists(Path.Combine(seed.DataDir, "Teams", legacy.Name, p))));
        Assert.True(Directory.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", SoftwareCoArchive.SpringPromo)));
        Assert.Equal(legacy.Members, plan.Agents.Where(a => personas.Get(a.Name) is { } p && PersonaFrontmatter.TryReadIdentity(p.Text, out PersonaIdentity? identity, out _) && identity.Teams.Contains(legacy.Name, StringComparer.OrdinalIgnoreCase)).Select(a => a.Name).Order(StringComparer.Ordinal));
        IReadOnlyList<Room> rooms = await directory.GetRoomsForTeamAsync(legacy.Name, ct);
        Assert.Equal(2, rooms.Count);
        Assert.All(rooms, r => Assert.False(r.Archived));
    }

    /// <summary>
    /// SEED-HOME-8: Home and Family is set up as a hire from <c>household@1.1.0</c> would leave it: a typed, onboarded
    /// <c>_team.md</c> naming Hattie as manager, the type's default process prose as <c>AGENTS.md</c>, and a <c>_role</c> on every member.
    /// </summary>
    [Fact]
    public async Task Teams_HomeAndFamilyIsSetUpFromTheHouseholdType()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        string folder = Path.Combine(seed.DataDir, "Teams", SoftwareCoHome.Team);
        TeamTypeCatalog types = new(seed.Options(), NullLogger<TeamTypeCatalog>.Instance);

        Assert.True(TeamFile.TryParse(await File.ReadAllTextAsync(Path.Combine(folder, TeamPaths.TeamFileName), ct), out TeamFile? file));
        string process = await File.ReadAllTextAsync(Path.Combine(folder, TeamPaths.AgentsMdFileName), ct);
        string? template = types.ReadProcess("household", "household-basics");
        string[] templateLines = (template ?? string.Empty).ReplaceLineEndings("\n").Split('\n');

        // The template's frontmatter goes to _team.md; its body (everything after the closing fence) is the whole of AGENTS.md.
        string? expected = template is null ? null : string.Join('\n', templateLines[(FrontmatterFence.FindClosing(templateLines, 1) + 1)..]);

        Assert.Equal("household@1.1.0", file.TeamType.ToString());
        Assert.Equal("Hattie", file.Manager);
        Assert.True(file.Onboarded);
        Assert.False(string.IsNullOrWhiteSpace(file.Owns));
        Assert.NotNull(expected);
        Assert.Equal(expected.ReplaceLineEndings("\n").TrimEnd('\n'), process.ReplaceLineEndings("\n").TrimEnd('\n'));
        Assert.True(ProcessDocument.TryParse(process, out _));
        Assert.Equal(["household/household-manager@1.1.0", "household/meal-planner@1.1.0", "household/home-keeper@1.1.0", "household/schedule-keeper@1.1.0"], await RolesAsync(seed, ["Hattie", "Theo", "Ravi", "Iris"], ct));
    }

    /// <summary>SEED-RT-6: The layout marker exists, so Huddle's one-time migration will not move the seeded notes.</summary>
    [Fact]
    public async Task Layout_MarkerIsWrittenAndNotesStayUnderTeams()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(seed.DataDir, "Agents", ".layout-migrated")));
        Assert.True(File.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", "Personas.md")));
        Assert.True(File.Exists(Path.Combine(seed.Root, SeedRootGuard.MarkerFileName)));
        Assert.True(File.Exists(Path.Combine(seed.Root, "manifest.md")));
        Assert.True(Directory.Exists(Path.Combine(seed.DataDir, "Teams", "Sandbox")));
        Assert.True(Directory.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", "Lifecycle Emails")));
    }

    /// <summary>SEED-SAFE-6: Seeding again wipes what was there: a stray file and the old database are gone, and the run still succeeds.</summary>
    [Fact]
    public async Task Rerun_StartsFromACleanSlate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        string stray = Path.Combine(seed.DataDir, "stray.txt");
        await File.WriteAllTextAsync(stray, "left over from an earlier run", ct);
        string firstUserId = seed.Summary.Ids.UserIds["Grace"];

        SeedSummary second = await SeedRunner.RunAsync(new SeedRequest(ScenarioCatalog.All[0], seed.Root, SeededFolder.Today), [], ct);

        Assert.False(File.Exists(stray));
        Assert.NotEqual(firstUserId, second.Ids.UserIds["Grace"]);
        Assert.Equal(seed.Summary.Plan.Tasks.Count, Directory.EnumerateFiles(Path.Combine(second.DataDir, "Teams"), "*.md", SearchOption.AllDirectories).Count(f => f.Contains("_tasks", StringComparison.Ordinal)));
    }

    /// <summary>SEED-RT-7: The manifest lists every Task, every Room id, the derived-state facts and what is archived.</summary>
    [Fact]
    public async Task Manifest_ListsTheSeededEntities()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);

        string manifest = await File.ReadAllTextAsync(Path.Combine(seed.Root, "manifest.md"), ct);

        Assert.All(seed.Summary.Plan.Tasks, t => Assert.Contains(t.Id, manifest, StringComparison.Ordinal));
        Assert.All(seed.Summary.Ids.RoomIds.Values, id => Assert.Contains(id, manifest, StringComparison.Ordinal));
        Assert.Contains("Legal-Checklist (unresolved)", manifest, StringComparison.Ordinal);
        Assert.Contains("Overdue: ", manifest, StringComparison.Ordinal);
        Assert.Contains("| Team | Legacy Site (Projects: Checkout v1, Old Marketing Site) | `Archive/Legacy Site` | Lena, Owen, Sofia |", manifest, StringComparison.Ordinal); // contains-ok: one whole table row of a generated multi-section file
        Assert.Contains("| Project | Growth / Spring Promo | `Archive/Growth/Spring Promo` | - |", manifest, StringComparison.Ordinal); // contains-ok: one whole table row of a generated multi-section file
        IReadOnlyDictionary<string, string> ids = seed.Summary.Ids.RoomIds;
        Assert.Equal(
            [
                $"| Chat | Q2 retro (root) | - | `{ids[SoftwareCoRooms.Q2Retro]}` |",
                $"| Chat | Legacy Site (Legacy Site Team Room) | - | `{ids[SoftwareCoArchive.TeamLegacySite]}` |",
                $"| Chat | Checkout v1 sunset (Legacy Site / Checkout v1 chat) | - | `{ids[SoftwareCoArchive.ChatLegacyCheckout]}` |",
                $"| Chat | Log shipping spike (Platform / Observability chat) | - | `{ids[SoftwareCoArchive.ChatPlatformLogSpike]}` |",
                $"| Chat | Spring promo wrap-up (Growth / Spring Promo chat) | - | `{ids[SoftwareCoArchive.ChatGrowthSpringPromo]}` |",
            ],
            manifest.Split('\n').Where(static line => line.StartsWith("| Chat | ", StringComparison.Ordinal)));
    }

    private static async Task<string?[]> RolesAsync(SeededFolder seed, string[] names, CancellationToken ct)
    {
        List<string?> roles = [];
        foreach (string name in names)
        {
            string persona = await File.ReadAllTextAsync(Path.Combine(seed.DataDir, "Agents", name, name + ".md"), ct);
            roles.Add(PersonaFrontmatter.TryReadIdentity(persona, out PersonaIdentity? identity, out _) ? identity.Role?.ToString() : null);
        }

        return [.. roles];
    }
}

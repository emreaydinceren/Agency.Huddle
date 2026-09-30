using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>
/// Seeds the scenario into a temp folder and reads it back with Huddle's own loaders. These tests fail when the
/// app and <c>agents/DataSeeding.md</c> drift apart, which is the whole reason the seeder loads through the app.
/// </summary>
public sealed class SeedRoundTripTests
{
    private static readonly string[] SavedViewIds =
        ["platform-board", "growth-by-project", "my-open-work", "blocked-work", "awaiting-review", "support-urgent", "unassigned", "closed-work"];

    /// <summary>Every Teammate loads, none is rejected, and Team membership survives.</summary>
    [Fact]
    public async Task Teammates_AllLoadWithoutRejections()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        var options = seed.Options();

        using PersonaStore store = new(new TeammatePaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);

        Assert.Empty(store.RejectedFiles);
        Assert.Equal(plan.Teammates.Count, store.Entries.Count);
        Assert.All(plan.Teammates, seeded =>
        {
            PersonaEntry loaded = Assert.Single(store.Entries, e => e.Name == seeded.Name);
            Assert.Equal(seeded.Alias, loaded.Alias);
            Assert.Equal(seeded.Title, loaded.Title);
            Assert.Equal(seeded.Teams.Order(StringComparer.OrdinalIgnoreCase), loaded.Teams.Order(StringComparer.OrdinalIgnoreCase));
        });
    }

    /// <summary>Every Task loads, none is rejected, the Change log dates round-trip, and start-up reconciliation adds nothing.</summary>
    [Fact]
    public async Task Tasks_AllLoadAndKeepTheirBackdatedDates()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        var options = seed.Options();
        using PersonaStore personas = new(new TeammatePaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);

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

    /// <summary>A Task created from a Room carries that Room's real id.</summary>
    [Fact]
    public async Task Tasks_OriginPointsAtTheSeededRoom()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        var options = seed.Options();
        using PersonaStore personas = new(new TeammatePaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);
        using TaskStore store = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        TaskItem task = Assert.Single(store.All, t => t.Id.ToString() == "SUPP-0005");

        Assert.Equal(seed.Summary.Ids.RoomIds[SoftwareCoRooms.IncidentWarRoom], task.OriginRoomId);
    }

    /// <summary>The Skill loads for Alan and the Skill store reports no problems with it.</summary>
    [Fact]
    public async Task Skills_LoadWithoutIssues()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        using SkillStore store = new(seed.Options(), NullLogger<SkillStore>.Instance);

        Assert.NotNull(store.Get(SoftwareCoTeammates.PostmortemSkill));
        Assert.DoesNotContain(store.Issues, i => i.Skill == SoftwareCoTeammates.PostmortemSkill);
    }

    /// <summary>Every saved View in <c>views.json</c> is valid, and none is dropped.</summary>
    [Fact]
    public async Task Views_AllLoadWithoutErrors()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        using ViewStore store = new(seed.Options(), NullLogger<ViewStore>.Instance);

        Assert.Null(store.LoadError);
        Assert.Empty(store.InvalidViews);
        Assert.All(SavedViewIds, id => Assert.NotNull(store.Get(id)));
    }

    /// <summary>The database holds the Human, every Teammate under its seeded id, and the Rooms with their members, names and archive flag.</summary>
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
        foreach (SeedTeammate teammate in plan.Teammates)
        {
            User? user = await directory.FindUserByNameAsync(teammate.Name, ct);
            Assert.Equal(seed.Summary.Ids.UserIds[teammate.Name], user?.Id);
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

    /// <summary>Each transcript reads back in full, in order, from its members.</summary>
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

    /// <summary>The layout marker exists, so Huddle's one-time migration will not move the seeded notes.</summary>
    [Fact]
    public async Task Layout_MarkerIsWrittenAndNotesStayUnderTeams()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(seed.DataDir, "Teammates", ".layout-migrated")));
        Assert.True(File.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", "Personas.md")));
        Assert.True(File.Exists(Path.Combine(seed.Root, SeedRootGuard.MarkerFileName)));
        Assert.True(File.Exists(Path.Combine(seed.Root, "manifest.md")));
        Assert.True(Directory.Exists(Path.Combine(seed.DataDir, "Teams", "Sandbox")));
        Assert.True(Directory.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", "Lifecycle Emails")));
    }

    /// <summary>Seeding again wipes what was there: a stray file and the old database are gone, and the run still succeeds.</summary>
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

    /// <summary>The manifest lists every Task, every Room id and the derived-state facts.</summary>
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
    }
}

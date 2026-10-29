using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.References;

/// <summary>The resolver looks each candidate up now and reports what exists (spec §8.2 step 2, AT-T6).</summary>
public sealed class ReferenceResolverTests
{
    private static ReferenceCandidate Candidate(ReferenceKind kind, string key) => new(kind, key, 0);

    private static TaskItem PlatformTask() => Tasks.TestTasks.Make(
        id: "PLAT-0001",
        title: "Fix login",
        status: TaskState.InProgress,
        assignee: "Nova",
        location: new TaskLocation("Platform", null, false));

    /// <summary>A Task that exists comes back with its title, status, assignee and Team as they are now.</summary>
    [Fact]
    public async Task Resolve_Task_FreshTitleStatusAssigneeTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync([ReferenceResolverTests.Candidate(ReferenceKind.Task, "PLAT-0001")], ct);

        ResolvedTask task = Assert.IsType<ResolvedTask>(Assert.Single(result));
        Assert.Equal("PLAT-0001", task.Id.ToString());
        Assert.Equal("Fix login", task.Title);
        Assert.Equal(TaskState.InProgress, task.Status);
        Assert.Equal("Nova", task.Assignee);
        Assert.Equal("Platform", task.Team);
    }

    /// <summary>A Room comes back with its Name and how many Members it has.</summary>
    [Fact]
    public async Task Resolve_Room_NameAndMemberCount()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync([ReferenceResolverTests.Candidate(ReferenceKind.Room, room.Id)], ct);

        Assert.Equal([new ResolvedRoom(room.Id, "Launch", 2, null, false)], result);
    }

    /// <summary>A root Room, a Team-scoped Room and a Team Room report their Scope.</summary>
    [Fact]
    public async Task Resolve_Room_ScopeAndTeamRoomFlag()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        List<string> members = [KnownIds.Human, fixture.Nova.Id];
        Room root = await fixture.Directory.CreateRoomAsync("Plan", members, ct);
        Room scoped = await fixture.Directory.CreateRoomAsync("Plan", members, team: "Marketing", ct: ct);
        Room teamRoom = await fixture.Directory.CreateRoomAsync("Marketing", members, "Marketing", true, ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync(
            [
                ReferenceResolverTests.Candidate(ReferenceKind.Room, root.Id),
                ReferenceResolverTests.Candidate(ReferenceKind.Room, scoped.Id),
                ReferenceResolverTests.Candidate(ReferenceKind.Room, teamRoom.Id),
            ],
            ct);

        Assert.Equal(
            [
                new ResolvedRoom(root.Id, "Plan", 2, null, false),
                new ResolvedRoom(scoped.Id, "Plan", 2, "Marketing", false),
                new ResolvedRoom(teamRoom.Id, "Marketing", 2, "Marketing", true),
            ],
            result);
    }

    /// <summary>An Archived Room whose Team no longer exists is still a Room, not a gone reference.</summary>
    [Fact]
    public async Task Resolve_ArchivedRoomOfAGoneTeam_IsNotGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Old", [KnownIds.Human, fixture.Nova.Id], team: "Vanished", ct: ct);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync([ReferenceResolverTests.Candidate(ReferenceKind.Room, room.Id)], ct);

        Assert.Equal([new ResolvedRoom(room.Id, "Old", 2, "Vanished", false)], result);
    }

    /// <summary>An uploaded file resolves while its folder exists and is gone once the Room's upload folder is deleted.</summary>
    [Fact]
    public async Task Resolve_UploadOfADeletedRoom_IsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        string folder = Path.Combine(fixture.DataDir, "Teams", "Marketing", "Uploads", "room-1");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "brief.txt");
        await File.WriteAllTextAsync(path, "x", ct);
        ReferenceResolver resolver = fixture.Resolver();
        ReferenceCandidate candidate = ReferenceResolverTests.Candidate(ReferenceKind.LibraryFile, path);

        IReadOnlyList<ResolvedReference> before = await resolver.ResolveAsync([candidate], ct);
        Directory.Delete(folder, true);
        IReadOnlyList<ResolvedReference> after = await resolver.ResolveAsync([candidate], ct);

        Assert.Equal([new ResolvedLibraryFile(path, "brief.txt")], before);
        Assert.Equal([new GoneReference(ReferenceKind.LibraryFile, path)], after);
    }

    /// <summary>At scan time a missing Task or file is dropped, but a Room link that names nothing stays gone (pasted from a deleted Room).</summary>
    [Fact]
    public async Task ResolveCurrent_Unresolved_TasksAndFilesDropped_RoomsGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);

        ResolvedSet result = await fixture.Resolver().ResolveCurrentAsync(
            [
                ReferenceResolverTests.Candidate(ReferenceKind.Task, "ZZZ-0009"),
                ReferenceResolverTests.Candidate(ReferenceKind.Room, "no-such-room"),
            ],
            ct);

        Assert.Equal([new GoneReference(ReferenceKind.Room, "no-such-room")], result.Items);
    }

    /// <summary>An absolute path outside every Library Root is not a reference: it is dropped, not reported as gone (finding 1).</summary>
    [Fact]
    public async Task Resolve_PathOutsideTheLibrary_IsDropped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);

        ResolvedSet result = await fixture.Resolver().ResolveCurrentAsync(
            [ReferenceResolverTests.Candidate(ReferenceKind.LibraryFile, @"C:\Repos\x.cs")],
            ct);

        Assert.Empty(result.Items);
    }

    /// <summary>Twelve Task lookalikes that name no Task do not use up the cap: a Room link after them is kept (finding 2).</summary>
    [Fact]
    public async Task Resolve_LookalikesBeforeARoom_DoNotCrowdItOut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        List<ReferenceCandidate> candidates = Enumerable.Range(1, 12)
            .Select(index => ReferenceResolverTests.Candidate(ReferenceKind.Task, "ZZZ-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        candidates.Add(ReferenceResolverTests.Candidate(ReferenceKind.Room, room.Id));

        ResolvedSet result = await fixture.Resolver().ResolveCurrentAsync(candidates, ct);

        Assert.Equal([new ResolvedRoom(room.Id, "Launch", 2, null, false)], result.Items);
        Assert.Equal(1, result.EligibleCount);
    }

    /// <summary>The cap applies to what resolved: twelve existing Rooms keep ten.</summary>
    [Fact]
    public async Task Resolve_TwelveRooms_KeepsTen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        List<ReferenceCandidate> candidates = [];
        for (int index = 0; index < 12; index++)
        {
            Room made = await fixture.Directory.CreateRoomAsync("R" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), [KnownIds.Human, fixture.Nova.Id], ct);
            candidates.Add(ReferenceResolverTests.Candidate(ReferenceKind.Room, made.Id));
        }

        ResolvedSet result = await fixture.Resolver().ResolveCurrentAsync(candidates, ct);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(12, result.EligibleCount);
    }

    /// <summary>A Team comes back with its type's display name and its Agents.</summary>
    [Fact]
    public async Task Resolve_Team_TypeAndMembers()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        fixture.Catalog.Teams = [new TeamSummary("Marketing", [], ["Nova", "Echo"], true) { TeamType = new VersionedRef("web", "1.0.0") }];

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync([ReferenceResolverTests.Candidate(ReferenceKind.Team, "marketing")], ct);

        ResolvedTeam team = Assert.IsType<ResolvedTeam>(Assert.Single(result));
        Assert.Equal("Marketing", team.Name);
        Assert.Equal("Web", team.TypeLabel);
        Assert.Equal(["Nova", "Echo"], team.Members);
        Assert.Null(team.TeamRoomId);
    }

    /// <summary>A Team's Team Room id is reported when it has one, archived or not, and null when it has none.</summary>
    [Fact]
    public async Task Resolve_Team_TeamRoomIdOrNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        fixture.Catalog.Teams = [new TeamSummary("Marketing", [], ["Nova"], true), new TeamSummary("Sales", [], [], true)];
        Room teamRoom = await fixture.Directory.CreateRoomAsync("Marketing", [KnownIds.Human, fixture.Nova.Id], "Marketing", true, ct);
        await fixture.Directory.SetRoomArchivedAsync(teamRoom.Id, true, ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync(
            [ReferenceResolverTests.Candidate(ReferenceKind.Team, "Marketing"), ReferenceResolverTests.Candidate(ReferenceKind.Team, "Sales")],
            ct);

        Assert.Equal(2, result.Count);
        ResolvedTeam marketing = Assert.IsType<ResolvedTeam>(result[0]);
        ResolvedTeam sales = Assert.IsType<ResolvedTeam>(result[1]);
        Assert.Equal(teamRoom.Id, marketing.TeamRoomId);
        Assert.Equal("Sales", sales.Name);
        Assert.Null(sales.TeamRoomId);
    }

    /// <summary>A file that exists resolves; a path inside the Library that does not exist is gone.</summary>
    [Fact]
    public async Task Resolve_LibraryFile_ExistingOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        string folder = Path.Combine(fixture.DataDir, "Teams", "Marketing");
        Directory.CreateDirectory(folder);
        string present = Path.Combine(folder, "plan.md");
        string missing = Path.Combine(folder, "nope.md");
        await File.WriteAllTextAsync(present, "x", ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync(
            [ReferenceResolverTests.Candidate(ReferenceKind.LibraryFile, present), ReferenceResolverTests.Candidate(ReferenceKind.LibraryFile, missing)],
            ct);

        Assert.Equal([new ResolvedLibraryFile(present, "plan.md"), new GoneReference(ReferenceKind.LibraryFile, missing)], result);
    }

    /// <summary>A Task, Room and Team that name nothing are gone, in candidate order (the chip lookup of a recorded reference).</summary>
    [Fact]
    public async Task Resolve_UnknownKeys_AreGone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync(
            [
                ReferenceResolverTests.Candidate(ReferenceKind.Task, "ZZZ-0009"),
                ReferenceResolverTests.Candidate(ReferenceKind.Room, "no-such-room"),
                ReferenceResolverTests.Candidate(ReferenceKind.Team, "Nobody"),
            ],
            ct);

        Assert.Equal(
            [
                new GoneReference(ReferenceKind.Task, "ZZZ-0009"),
                new GoneReference(ReferenceKind.Room, "no-such-room"),
                new GoneReference(ReferenceKind.Team, "Nobody"),
            ],
            result);
    }

    /// <summary>With Tasks disabled a Task candidate yields nothing, not even a gone reference.</summary>
    [Fact]
    public async Task Resolve_TasksDisabled_NoTaskResolved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, static options => options.Tasks.Enabled = false, PlatformTask());

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync(
            [ReferenceResolverTests.Candidate(ReferenceKind.Task, "PLAT-0001"), ReferenceResolverTests.Candidate(ReferenceKind.Task, "ZZZ-0009")],
            ct);

        Assert.Empty(result);
    }

    /// <summary>With the Library disabled a file candidate yields nothing, not even a gone reference.</summary>
    [Fact]
    public async Task Resolve_LibraryDisabled_NoFileResolved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, static options => options.Library.Enabled = false);
        string folder = Path.Combine(fixture.DataDir, "Teams", "Marketing");
        Directory.CreateDirectory(folder);
        string present = Path.Combine(folder, "plan.md");
        await File.WriteAllTextAsync(present, "x", ct);

        IReadOnlyList<ResolvedReference> result = await fixture.Resolver().ResolveAsync([ReferenceResolverTests.Candidate(ReferenceKind.LibraryFile, present)], ct);

        Assert.Empty(result);
    }
}

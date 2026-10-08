using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Tests for <see cref="SprintProposalService"/> (Task 63, PL-4): Approve is the only path that creates a Sprint,
/// it puts each proposed Task in it, and a second Approve of the same proposal does nothing.
/// </summary>
public sealed class SprintProposalServiceTests
{
    private const string OneSprint = "# Sprints\n\n- S1 | Sprint 1\n";

    /// <summary>Everything a test needs: the harness, a manager's room and the service under test.</summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(TaskToolHarness harness, SprintProposalStore proposals, SprintProposalService service, string managerRoomId, string otherRoomId)
        {
            this.Harness = harness;
            this.Proposals = proposals;
            this.Service = service;
            this.ManagerRoomId = managerRoomId;
            this.OtherRoomId = otherRoomId;
        }

        /// <summary>The task tool harness with a real Sprint store.</summary>
        public TaskToolHarness Harness { get; }

        /// <summary>The proposal store the service reads.</summary>
        public SprintProposalStore Proposals { get; }

        /// <summary>The service under test.</summary>
        public SprintProposalService Service { get; }

        /// <summary>The Human's room with Nova, the Platform manager.</summary>
        public string ManagerRoomId { get; }

        /// <summary>The Human's room with Kai, who manages nothing.</summary>
        public string OtherRoomId { get; }

        /// <summary>The real Sprint store.</summary>
        public SprintStore Sprints => (SprintStore)this.Harness.Sprints;

        /// <summary>Builds the fixture.</summary>
        /// <param name="ct">Cancels the seeding.</param>
        /// <param name="sprintsText">The starting <c>sprints.md</c>.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct, string sprintsText = OneSprint)
        {
            TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: sprintsText);
            Room managerRoom = await harness.Directory.CreateRoomAsync("Plan", [KnownIds.Human, harness.NovaId ?? ""], ct);
            Room otherRoom = await harness.Directory.CreateRoomAsync("Chat", [KnownIds.Human, harness.KaiId ?? ""], ct);
            SprintProposalStore proposals = new(new DiskTeamFolders(Path.Combine(harness.Options.Value.DataDir, "platform-folder")));
            FakeTeamCatalog catalog = new()
            {
                Teams = [new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" }],
            };
            SprintProposalService service = new(
                proposals,
                (SprintStore)harness.Sprints,
                harness.Service,
                harness.Store,
                harness.Chat,
                harness.Directory,
                catalog,
                harness.Options,
                NullLogger<SprintProposalService>.Instance);
            return new Fixture(harness, proposals, service, managerRoom.Id, otherRoom.Id);
        }

        /// <summary>Writes a Task straight into the store.</summary>
        /// <param name="id">The Task id.</param>
        /// <param name="status">The Task's state.</param>
        public TaskItem Put(string id, TaskState status = TaskState.ToDo)
        {
            TaskItem task = TestTasks.Make(id: id, status: status);
            TaskItem toWrite = task with { Path = TaskLayout.PathFor(this.Harness.Store.RootDirectory, task.Location, task.Id) };
            return this.Harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite))
                ?? throw new InvalidOperationException("Fixture collided with an existing file.");
        }

        /// <summary>Stores a proposal from Nova and returns it.</summary>
        /// <param name="name">The proposed Sprint's name.</param>
        /// <param name="taskIds">The proposed Task ids.</param>
        public SprintProposal Propose(string name, params string[] taskIds)
        {
            SprintProposal proposal = new(Guid.NewGuid().ToString("N"), name, taskIds, "Nova", "Platform");
            Assert.True(this.Proposals.Add(proposal));
            return proposal;
        }

        /// <inheritdoc />
        public void Dispose() => this.Harness.Dispose();
    }

    /// <summary>Approve creates the Sprint, sets it on each Task with a Change log entry, removes the proposal and tells the manager.</summary>
    [Fact]
    public async Task ApproveAsync_CreatesSprintAndSetsItOnEachTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        TaskItem first = fixture.Put("PLAT-0001");
        TaskItem second = fixture.Put("PLAT-0002");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001", "PLAT-0002");

        SprintProposalOutcome outcome = await fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct);

        Sprint sprint = fixture.Sprints.FindByName("Launch") ?? throw new InvalidOperationException("Sprint was not created.");
        Assert.Equal(SprintProposalOutcomeKind.Approved, outcome.Kind);
        Assert.False(outcome.SprintExisted);
        Assert.Equal(["PLAT-0001", "PLAT-0002"], outcome.Applied);
        Assert.Empty(outcome.Skipped);
        Assert.Equal("Approved Sprint 'Launch'. Added: PLAT-0001, PLAT-0002. @Nova", outcome.PostedText);
        TaskItem firstNow = fixture.Harness.Store.Get(first.Id) ?? throw new InvalidOperationException("Task vanished.");
        TaskItem secondNow = fixture.Harness.Store.Get(second.Id) ?? throw new InvalidOperationException("Task vanished.");
        Assert.Equal(sprint.Id, firstNow.Sprint);
        Assert.Equal(sprint.Id, secondNow.Sprint);
        Assert.Equal(first.ChangeLog.Count + 1, firstNow.ChangeLog.Count);
        Assert.Equal(fixture.Harness.Options.Value.HumanName, firstNow.ChangeLog[^1].Actor);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>Review fix 4: when creating the Sprint throws, the proposal stays pending so the manager can approve it again.</summary>
    [Fact]
    public async Task ApproveAsync_CreateThrows_KeepsTheProposal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        _ = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");
        File.Delete(fixture.Harness.SprintsFilePath);
        _ = Directory.CreateDirectory(fixture.Harness.SprintsFilePath);

        _ = await Record.ExceptionAsync(() => fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct));

        Assert.Equal(proposal.Id, Assert.Single(fixture.Proposals.Pending("Platform")).Id);
    }

    /// <summary>A Task that no longer exists or is Done is skipped and named; the others are still added.</summary>
    [Fact]
    public async Task ApproveAsync_VanishedAndDoneTasks_AreSkippedAndNamed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        _ = fixture.Put("PLAT-0001");
        _ = fixture.Put("PLAT-0003", TaskState.Done);
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001", "PLAT-0002", "PLAT-0003");

        SprintProposalOutcome outcome = await fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct);

        Assert.Equal(SprintProposalOutcomeKind.Approved, outcome.Kind);
        Assert.Equal(["PLAT-0001"], outcome.Applied);
        Assert.Equal([new SkippedTask("PLAT-0002", "no longer exists"), new SkippedTask("PLAT-0003", "already Done")], outcome.Skipped);
        Assert.Equal("Approved Sprint 'Launch'. Added: PLAT-0001. Skipped: PLAT-0002 (no longer exists), PLAT-0003 (already Done). @Nova", outcome.PostedText);
        Assert.Null(fixture.Harness.Store.Get(new TaskId("PLAT", 3))?.Sprint);
    }

    /// <summary>Decline posts the reason to the manager, creates no Sprint and leaves every Task alone.</summary>
    [Fact]
    public async Task DeclineAsync_PostsReasonAndCreatesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        TaskItem task = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");

        SprintProposalOutcome outcome = await fixture.Service.DeclineAsync(fixture.ManagerRoomId, proposal.Id, "Not this week.", ct);

        Assert.Equal(SprintProposalOutcomeKind.Declined, outcome.Kind);
        Assert.Equal("Declined the proposed Sprint 'Launch': Not this week. @Nova", outcome.PostedText);
        Assert.Null(fixture.Sprints.FindByName("Launch"));
        Assert.Equal(["Sprint 1"], fixture.Sprints.All.Select(static sprint => sprint.Name));
        Assert.Null(fixture.Harness.Store.Get(task.Id)?.Sprint);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A Decline with a blank reason still tells the manager there was none.</summary>
    [Fact]
    public async Task DeclineAsync_BlankReason_SaysNoReasonWasGiven()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        _ = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");

        SprintProposalOutcome outcome = await fixture.Service.DeclineAsync(fixture.ManagerRoomId, proposal.Id, "  ", ct);

        Assert.Equal("Declined the proposed Sprint 'Launch': no reason given. @Nova", outcome.PostedText);
    }

    /// <summary>A second Approve of the same proposal is a no-op: Gone, nothing posted, no second Sprint.</summary>
    [Fact]
    public async Task ApproveAsync_Twice_SecondIsGoneAndChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        TaskItem task = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");
        _ = await fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct);
        int logAfterFirst = fixture.Harness.Store.Get(task.Id)?.ChangeLog.Count ?? -1;

        SprintProposalOutcome second = await fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct);

        Assert.Equal(SprintProposalOutcomeKind.Gone, second.Kind);
        Assert.Equal(string.Empty, second.PostedText);
        Assert.Equal(["Sprint 1", "Launch"], fixture.Sprints.All.Select(static sprint => sprint.Name));
        Assert.Equal(logAfterFirst, fixture.Harness.Store.Get(task.Id)?.ChangeLog.Count);
    }

    /// <summary>A Sprint name already in sprints.md is accepted as Existing and the Tasks still join it.</summary>
    [Fact]
    public async Task ApproveAsync_ExistingSprintName_IsAcceptedAsExisting()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        TaskItem task = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Sprint 1", "PLAT-0001");

        SprintProposalOutcome outcome = await fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct);

        Assert.Equal(SprintProposalOutcomeKind.Approved, outcome.Kind);
        Assert.True(outcome.SprintExisted);
        Assert.Equal("Approved Sprint 'Sprint 1' (it already existed). Added: PLAT-0001. @Nova", outcome.PostedText);
        Assert.Equal(["Sprint 1"], fixture.Sprints.All.Select(static sprint => sprint.Name));
        Assert.Equal(new SprintId(1), fixture.Harness.Store.Get(task.Id)?.Sprint);
    }

    /// <summary>A Sprint name Huddle cannot write is reported, nothing is posted, and the proposal stays for the Human.</summary>
    [Fact]
    public async Task ApproveAsync_SprintRefused_KeepsTheProposal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        _ = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("none", "PLAT-0001");

        SprintProposalOutcome outcome = await fixture.Service.ApproveAsync(fixture.ManagerRoomId, proposal.Id, ct);

        Assert.Equal(SprintProposalOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(SprintFileFormat.ValidateName("none"), outcome.PostedText);
        Assert.Same(proposal, Assert.Single(fixture.Proposals.Pending("Platform")));
        Assert.Equal(["Sprint 1"], fixture.Sprints.All.Select(static sprint => sprint.Name));
    }

    /// <summary>A proposal can only be answered from its own manager's room.</summary>
    [Fact]
    public async Task ApproveAsync_WrongRoom_IsGoneAndKeepsTheProposal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        _ = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");

        SprintProposalOutcome outcome = await fixture.Service.ApproveAsync(fixture.OtherRoomId, proposal.Id, ct);

        Assert.Equal(SprintProposalOutcomeKind.Gone, outcome.Kind);
        Assert.Null(fixture.Sprints.FindByName("Launch"));
        Assert.Same(proposal, Assert.Single(fixture.Proposals.Pending("Platform")));
    }

    /// <summary>The pending list for a room holds the proposals of the Teams its agent manages, and nothing for other rooms.</summary>
    [Fact]
    public async Task PendingAsync_ListsOnlyTheManagersProposals()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        _ = fixture.Put("PLAT-0001");
        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");

        IReadOnlyList<SprintProposal> inManagerRoom = await fixture.Service.PendingAsync(fixture.ManagerRoomId, ct);
        IReadOnlyList<SprintProposal> inOtherRoom = await fixture.Service.PendingAsync(fixture.OtherRoomId, ct);

        Assert.Same(proposal, Assert.Single(inManagerRoom));
        Assert.Empty(inOtherRoom);
    }

    /// <summary>Adding or removing a proposal raises the store's Changed event with the Team's name.</summary>
    [Fact]
    public async Task Store_AddAndRemove_RaiseChanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        List<string> raised = [];
        fixture.Proposals.Changed += raised.Add;

        SprintProposal proposal = fixture.Propose("Launch", "PLAT-0001");
        _ = fixture.Proposals.Remove(proposal.Id);

        Assert.Equal(["Platform", "Platform"], raised);
    }
}

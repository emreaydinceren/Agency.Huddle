using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for <see cref="SprintProposalCard"/> (Task 63, PL-4): the Human sees a manager's Sprint proposal in the
/// manager's room, and Approve or Decline goes through <see cref="SprintProposalService"/>.
/// </summary>
public sealed class SprintProposalCardTests
{
    /// <summary>The harness, a manager's room, the proposal store and the real service the card calls.</summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(TaskToolHarness harness, SprintProposalStore proposals, SprintProposalService service, string roomId, DirectoryGate gate)
        {
            this.Gate = gate;
            this.Harness = harness;
            this.Proposals = proposals;
            this.Service = service;
            this.RoomId = roomId;
        }

        /// <summary>The task tool harness with a real Sprint store.</summary>
        public TaskToolHarness Harness { get; }

        /// <summary>The proposal store the card reads.</summary>
        public SprintProposalStore Proposals { get; }

        /// <summary>The real service the card calls.</summary>
        public SprintProposalService Service { get; }

        /// <summary>The Human's room with Nova, the Platform manager.</summary>
        public string RoomId { get; }

        /// <summary>Builds the fixture.</summary>
        /// <param name="ct">Cancels the seeding.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct)
        {
            TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct, sprintsText: "# Sprints\n\n- S1 | Sprint 1\n");
            Room room = await harness.Directory.CreateRoomAsync("Plan", [KnownIds.Human, harness.NovaId ?? ""], ct);
            SprintProposalStore proposals = new(new DiskTeamFolders(Path.Combine(harness.Options.Value.DataDir, "platform-folder")));
            FakeTeamCatalog catalog = new()
            {
                Teams = [new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" }],
            };
            ITeamDirectory serviceDirectory = DirectoryGate.Wrap(harness.Directory, out DirectoryGate gate);
            SprintProposalService service = new(
                proposals,
                (SprintStore)harness.Sprints,
                harness.Service,
                harness.Store,
                harness.Chat,
                serviceDirectory,
                catalog,
                new Agency.Huddle.App.Profile.ConfiguredHumanIdentity(harness.Options.Value.HumanName),
                NullLogger<SprintProposalService>.Instance);
            return new Fixture(harness, proposals, service, room.Id, gate);
        }

        /// <summary>Holds the service's directory calls while a test keeps a decision in flight.</summary>
        public DirectoryGate Gate { get; }

        /// <summary>Writes a Task straight into the store.</summary>
        /// <param name="id">The Task id.</param>
        /// <param name="title">The Task's title.</param>
        /// <param name="assignee">The Task's assignee, if any.</param>
        public void Put(string id, string title, string? assignee)
        {
            TaskItem task = TestTasks.Make(id: id, title: title, assignee: assignee);
            TaskItem toWrite = task with { Path = TaskLayout.PathFor(this.Harness.Store.RootDirectory, task.Location, task.Id) };
            _ = this.Harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite))
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

        /// <summary>Registers what the card injects into <paramref name="ctx"/>.</summary>
        /// <param name="ctx">The bUnit context to register into.</param>
        public void RegisterInto(MudBunitContext ctx)
        {
            ctx.Services.AddSingleton(this.Proposals);
            ctx.Services.AddSingleton(this.Service);
            ctx.Services.AddSingleton(this.Harness.Store);
            AgentCardAssert.RegisterRoomServices(ctx, this.Harness.Directory);
        }

        /// <inheritdoc />
        public void Dispose() => this.Harness.Dispose();
    }

    /// <summary>Renders the card for a room.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="roomId">The room the card watches.</param>
    private static IRenderedComponent<SprintProposalCard> RenderCard(MudBunitContext ctx, string roomId) =>
        ctx.Render<SprintProposalCard>(parameters => parameters.Add(p => p.RoomId, roomId));

    /// <summary>The card lists the Sprint's name and each Task's id, title and assignee.</summary>
    [Fact]
    public async Task Renders_NameAndEachTask()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", "Kai");
        fixture.Put("PLAT-0002", "Review the parser", null);
        _ = fixture.Propose("Launch", "PLAT-0001", "PLAT-0002");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        cut.WaitForAssertion(() => Assert.Equal("Launch", cut.Find(".sprint-proposal-name").TextContent.Trim()));
        Assert.Equal(["PLAT-0001", "PLAT-0002"], cut.FindAll(".sprint-proposal-task-id").Select(static each => each.TextContent.Trim()));
        Assert.Equal(["Write the parser", "Review the parser"], cut.FindAll(".sprint-proposal-task-title").Select(static each => each.TextContent.Trim()));
        Assert.Equal(["Kai", "Unassigned"], cut.FindAll(".sprint-proposal-task-assignee").Select(static each => each.TextContent.Trim()));
    }

    /// <summary>A room with no proposal renders nothing.</summary>
    [Fact]
    public async Task NoProposal_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        Assert.Empty(cut.FindAll(".sprint-proposal-card"));
    }

    /// <summary>Approve creates the Sprint, sets it on the Tasks and the card disappears.</summary>
    [Fact]
    public async Task Approve_CreatesSprintAndCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", null);
        _ = fixture.Propose("Launch", "PLAT-0001");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".sprint-proposal-card")));

        await cut.InvokeAsync(() => cut.Find(".sprint-proposal-approve").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".sprint-proposal-card")));
        Sprint sprint = ((SprintStore)fixture.Harness.Sprints).FindByName("Launch") ?? throw new InvalidOperationException("Sprint was not created.");
        Assert.Equal(sprint.Id, fixture.Harness.Store.Get(new TaskId("PLAT", 1))?.Sprint);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>Decline with a reason creates nothing and the card disappears.</summary>
    [Fact]
    public async Task Decline_WithReason_CreatesNothingAndCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", null);
        _ = fixture.Propose("Launch", "PLAT-0001");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".sprint-proposal-card")));

        cut.Find(".sprint-proposal-reason input").Change("Not this week.");
        await cut.InvokeAsync(() => cut.Find(".sprint-proposal-decline").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".sprint-proposal-card")));
        Assert.Null(((SprintStore)fixture.Harness.Sprints).FindByName("Launch"));
        Assert.Null(fixture.Harness.Store.Get(new TaskId("PLAT", 1))?.Sprint);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>UX-030 to UX-034: the card is the shared shell; the reason field sits directly above the actions row, which reads Approve then Decline.</summary>
    [Fact]
    public async Task Card_IsTheSharedShell_ReasonAboveApproveThenDecline()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", null);
        _ = fixture.Propose("Launch", "PLAT-0001");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);

        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=region]")));
        IElement actions = AgentCardAssert.IsShell(cut, "Nova is proposing a Sprint", "Sprint proposal from Nova");
        Assert.Equal(["Approve", "Decline"], AgentCardAssert.Verbs(actions));
        IElement? field = actions.PreviousElementSibling;
        Assert.True(field is not null && field.Matches(".sprint-proposal-reason"), "The reason field belongs directly above the actions row.");
    }

    /// <summary>UX-035: the card is not rendered for an Archived Room, as with Proposal, Question and Elicitation.</summary>
    [Fact]
    public async Task ArchivedRoom_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", null);
        _ = fixture.Propose("Launch", "PLAT-0001");
        await fixture.Harness.Directory.SetRoomArchivedAsync(fixture.RoomId, true, ct);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);

        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);

        Assert.True(string.IsNullOrWhiteSpace(cut.Markup));
    }

    /// <summary>UX-033: while Approve is in flight its button reads "Approving…" and Decline is disabled.</summary>
    [Fact]
    public async Task Approve_InFlight_ShowsBusyLabel()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", null);
        _ = fixture.Propose("Launch", "PLAT-0001");
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        AgentCardAssert.RegisterRoomServices(ctx, fixture.Harness.Directory);
        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=region]")));
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Gate.Gate = gate;

        // Fire-and-forget: the approve parks on the gate, so awaiting Click would never return.
        _ = cut.InvokeAsync(() => cut.FindAll("button").First(static button => button.TextContent.Trim() == "Approve").ClickAsync());

        cut.WaitForAssertion(() => Assert.Equal(["Approving…", "Decline"], AgentCardAssert.Verbs(cut.Find(".agent-card-actions"))));
        Assert.True(cut.FindAll("button").First(static button => button.TextContent.Trim() == "Decline").HasAttribute("disabled"));

        await cut.InvokeAsync(gate.SetResult);

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
    }

    /// <summary>A proposal that arrives after the card rendered appears without a reload.</summary>
    [Fact]
    public async Task ProposalArrivesLater_CardAppears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        fixture.Put("PLAT-0001", "Write the parser", null);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        IRenderedComponent<SprintProposalCard> cut = RenderCard(ctx, fixture.RoomId);
        Assert.Empty(cut.FindAll(".sprint-proposal-card"));

        _ = fixture.Propose("Launch", "PLAT-0001");

        cut.WaitForAssertion(() => Assert.Equal("Launch", cut.Find(".sprint-proposal-name").TextContent.Trim()));
    }
}

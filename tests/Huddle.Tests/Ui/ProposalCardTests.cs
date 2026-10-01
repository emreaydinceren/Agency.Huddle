using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins Spec §6.11: <see cref="ProposalCard"/> renders a pending <see cref="Proposal"/> where the
/// conversation happened, computes its headroom line live from <see cref="PersonaStore.Entries"/>,
/// acts through the real (internal) <see cref="ProposalService"/> - no Envelope, exactly like the
/// Budget prompt in <c>Chat.razor</c> - and stays in step with <see cref="RoomEvents.ProposalChanged"/>
/// whether it caused the change itself or another tab did. Built the same way
/// <c>ProposalServiceTests.Fixture</c> is: real <see cref="SqliteTeamDirectory"/>,
/// <see cref="PersonaStore"/>, <see cref="ProposalStore"/>, <see cref="ChatService"/> and
/// <see cref="ProposalService"/> sharing one <see cref="TempDataDir"/>, registered into a plain
/// <see cref="MudBunitContext"/> - <see cref="ProposalCard"/> opens no popover and no dialog, so it
/// needs neither provider <see cref="MudBunitContext.RenderWithPopovers"/> exists for.
/// </summary>
public sealed class ProposalCardTests
{
    /// <summary>
    /// Spec §6.11's Responsibilities: the proposer, the copy ("{Proposer} proposes new Teammates.
    /// Approve creates these Teammates." - never "will be created"), every Candidate's Name, Alias,
    /// Title, Teams and Consult When, and the headroom line recomputed from
    /// <see cref="PersonaStore.Entries"/> against the default <see cref="AcpOptions.MaxTeammates"/>
    /// of 8 - five existing Teammates plus a two-Candidate Proposal reads "This adds 2 Teammates; 5
    /// of 8 exist.", the Spec's own worked example with the count changed.
    /// </summary>
    [Fact]
    public async Task Renders_CandidatesAndHeadroom()
    {
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        SeedExistingPersonas(fixture, 5);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);
        IReadOnlyList<Candidate> candidates =
        [
            new Candidate("Vera", "vera", "Researcher", "You research things.", [], null),
            new Candidate("Quill", "quill", "Writer", "You write things.", ["Business"], "When launching a product."),
        ];
        var proposal = MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        var cut = RenderCard(ctx, room.Id);

        Assert.Contains("Chief of Staff proposes new Teammates. Approve creates these Teammates.", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("will be created", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("This adds 2 Teammates; 5 of 8 exist.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Vera", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("vera", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Researcher", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Quill", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Writer", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Business", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("When launching a product.", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Approve calls the real <see cref="ProposalService.ApproveAsync"/> - proven by the Candidate's
    /// Persona file actually landing on disk, the same observable effect
    /// <c>ProposalServiceTests</c> checks - and, once it completes, the card renders nothing: Spec
    /// §6.11 says the outcome appears as the posted Message, never as card state, so there is nothing
    /// left here to show once the Proposal is gone.
    /// </summary>
    [Fact]
    public async Task Approve_CallsServiceAndCardDisappears()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);
        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        var cut = RenderCard(ctx, room.Id);

        await cut.InvokeAsync(() => FindButton(cut, "Approve").ClickAsync());

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Vera")));
        Assert.Null(fixture.Proposals.Get(room.Id));
    }

    /// <summary>
    /// Spec §6.11's Internal flow: <see cref="ProposalCard"/> subscribes to
    /// <see cref="RoomEvents.ProposalChanged"/> and re-reads <see cref="ProposalStore.Get"/> on any
    /// fire for its own Room id - not only the ones its own Approve/Decline click caused. Here a
    /// second caller (standing in for another browser tab, or another Human device) takes the same
    /// Proposal by calling <see cref="ProposalService.ApproveAsync"/> directly, bypassing this card's
    /// own buttons entirely, and the rendered card must still notice and hide.
    /// </summary>
    [Fact]
    public async Task OtherTabApproves_CardDisappearsOnProposalChanged()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);
        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        var cut = RenderCard(ctx, room.Id);
        Assert.Contains("proposes new Teammates", cut.Markup, StringComparison.Ordinal);

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Created, outcome.Kind);
        cut.WaitForAssertion(() => Assert.DoesNotContain("proposes new Teammates", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>
    /// Spec §6.11's Implementation notes: "The card is not rendered for an Archived Room." The
    /// Proposal is stored directly through <see cref="ProposalStore.TryPut"/> rather than through
    /// <see cref="ChatService"/>, so the already-tested "archiving drops the pending Proposal"
    /// cascade (D9's <c>ChatServiceTests.SetRoomArchived_WithProposal_Drops</c>) cannot be why nothing
    /// renders - only <see cref="ProposalCard"/>'s own Archived check can be.
    /// </summary>
    [Fact]
    public async Task ArchivedRoom_RendersNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);
        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);

        var cut = RenderCard(ctx, room.Id);

        Assert.True(string.IsNullOrWhiteSpace(cut.Markup));
    }

    /// <summary>
    /// Settled design point for D12: the busy flag disables BOTH buttons while either action runs,
    /// so a double click cannot call the service twice. <see cref="GatedTeamDirectory"/> suspends the
    /// proposer lookup <see cref="ProposalService.ApproveAsync"/> makes (through
    /// <c>ResolveMentionAsync</c>) so the busy window is observable, the same
    /// hold-a-real-await-open technique <c>TeammateCardTests</c> uses via
    /// <c>FakeModelCatalog.ModelsGate</c>. Releasing the gate lets the call complete and the card
    /// disappear, proving the busy state was transient rather than stuck.
    /// </summary>
    [Fact]
    public async Task Approve_Busy_ButtonsDisabled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);
        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);
        await using MudBunitContext ctx = new();
        fixture.RegisterInto(ctx);
        var cut = RenderCard(ctx, room.Id);
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Directory.Gate = gate;

        // Fire-and-forget: the approve handler parks on the gate, so awaiting ClickAsync would never return.
        _ = cut.InvokeAsync(() => FindButton(cut, "Approve").ClickAsync());

        cut.WaitForAssertion(() => Assert.True(FindButton(cut, "Approve").HasAttribute("disabled")));
        Assert.True(FindButton(cut, "Decline").HasAttribute("disabled"));

        await cut.InvokeAsync(gate.SetResult);

        cut.WaitForAssertion(() => Assert.True(string.IsNullOrWhiteSpace(cut.Markup)));
    }

    /// <summary>Renders <see cref="ProposalCard"/> alone - it opens no popover and no dialog, so the plain generic render is enough.</summary>
    /// <param name="ctx">The context to render into.</param>
    /// <param name="roomId">The Room id passed as <see cref="ProposalCard.RoomId"/>.</param>
    private static IRenderedComponent<ProposalCard> RenderCard(MudBunitContext ctx, string roomId)
    {
        return ctx.Render<ProposalCard>(parameters => parameters.Add(p => p.RoomId, roomId));
    }

    /// <summary>Adds <paramref name="count"/> minimally-valid existing Personas, named <c>Existing1</c>, <c>Existing2</c>, and so on, so a headroom line has something real to count.</summary>
    /// <param name="fixture">The fixture whose <see cref="PersonaStore"/> to add them through.</param>
    /// <param name="count">How many to add.</param>
    private static void SeedExistingPersonas(Fixture fixture, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            fixture.Personas.Add(new PersonaIdentity($"Existing{i}", "Role", $"existing{i}", []), "Body text.");
        }
    }

    /// <summary>Builds a Proposal ready to store via <see cref="ProposalStore.TryPut"/>, timestamped from the real clock - matching <c>ProposalServiceTests.MakeProposal</c>.</summary>
    /// <param name="roomId">The Room the Proposal is pending in.</param>
    /// <param name="proposerAgentId">The proposer's stable user id.</param>
    /// <param name="proposerName">The proposer's Name at the moment of proposing.</param>
    /// <param name="candidates">The Candidates the Proposal carries.</param>
    private static Proposal MakeProposal(string roomId, string proposerAgentId, string proposerName, IReadOnlyList<Candidate> candidates)
    {
        return new Proposal(Guid.CreateVersion7().ToString("N"), roomId, proposerAgentId, proposerName, candidates, TimeProvider.System.GetUtcNow());
    }

    /// <summary>The first rendered button whose trimmed text, OR its <c>aria-label</c>, equals <paramref name="text"/>.</summary>
    /// <param name="cut">The rendered card to search.</param>
    /// <param name="text">The button's expected label or <c>aria-label</c>.</param>
    private static IElement FindButton(IRenderedComponent<ProposalCard> cut, string text)
    {
        return cut.FindAll("button").First(button => MatchesButtonLabel(button, text));
    }

    /// <summary>Whether <paramref name="button"/>'s trimmed text, or its <c>aria-label</c>, equals <paramref name="text"/>.</summary>
    /// <param name="button">The candidate button.</param>
    /// <param name="text">The expected label or <c>aria-label</c>.</param>
    private static bool MatchesButtonLabel(IElement button, string text)
    {
        return string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal)
            || string.Equals(button.GetAttribute("aria-label"), text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bundles the real collaborators <see cref="ProposalCard"/> needs - a
    /// <see cref="GatedTeamDirectory"/> wrapping a real <see cref="SqliteTeamDirectory"/>, a
    /// <see cref="PersonaStore"/>, a <see cref="RoomEvents"/>, a <see cref="ProposalStore"/> and the
    /// <see cref="ProposalService"/> under test - all sharing one <see cref="TempDataDir"/>, the same
    /// style <c>ProposalServiceTests.Fixture</c> already uses for this layer.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(
            TempDataDir dataDir,
            PersonaStore personas,
            GatedTeamDirectory directory,
            RoomEvents events,
            ProposalStore proposals,
            ProposalService service)
        {
            this.DataDir = dataDir;
            this.Personas = personas;
            this.Directory = directory;
            this.Events = events;
            this.Proposals = proposals;
            this.Service = service;
        }

        /// <summary>The temp directory backing this fixture's <see cref="TeamOptions.DataDir"/>.</summary>
        public TempDataDir DataDir { get; }

        /// <summary>The real <see cref="PersonaStore"/> a test seeds existing Teammates into and reads created files back from.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The gated <see cref="ITeamDirectory"/> a test arranges Rooms and Members through, and can suspend for the busy test.</summary>
        public GatedTeamDirectory Directory { get; }

        /// <summary>The real <see cref="RoomEvents"/> hub <see cref="ProposalCard"/> subscribes to.</summary>
        public RoomEvents Events { get; }

        /// <summary>The real <see cref="ProposalStore"/> a test stores a Proposal into before rendering the card.</summary>
        public ProposalStore Proposals { get; }

        /// <summary>The real <see cref="ProposalService"/> a test can call directly to simulate another tab acting on the same Proposal.</summary>
        public ProposalService Service { get; }

        /// <summary>Builds a fresh <see cref="Fixture"/> with an initialised Team Directory (Human Name <c>"You"</c>) and no Personas yet loaded.</summary>
        /// <param name="ct">Cancels Team Directory initialisation.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct)
        {
            var dataDir = new TempDataDir();
            IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(new TeamOptions { DataDir = dataDir.Path });

            var realDirectory = new SqliteTeamDirectory(options);
            await realDirectory.InitializeAsync("You", ct);
            var directory = new GatedTeamDirectory(realDirectory);

            var personas = new PersonaStore(
                new TeammatePaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);
            var gateway = new FakeAgentGateway();
            var checker = new CandidateChecker(personas, directory, gateway);
            var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            var proposals = new ProposalStore(events);
            var store = new FileChatStore(options, NullLogger<FileChatStore>.Instance);
            var chat = new ChatService(directory, store, events, personas, options, proposals, NullLogger<ChatService>.Instance);
            var service = new ProposalService(proposals, checker, personas, chat, directory, options, NullLogger<ProposalService>.Instance);

            return new Fixture(dataDir, personas, directory, events, proposals, service);
        }

        /// <summary>Registers every real collaborator <see cref="ProposalCard"/> injects into <paramref name="ctx"/>'s container.</summary>
        /// <param name="ctx">The bUnit context to register into.</param>
        public void RegisterInto(MudBunitContext ctx)
        {
            ctx.Services.AddSingleton(this.Personas);
            ctx.Services.AddSingleton<ITeamDirectory>(this.Directory);
            ctx.Services.AddSingleton(this.Events);
            ctx.Services.AddSingleton(this.Proposals);
            ctx.Services.AddSingleton(this.Service);
        }

        /// <summary>Disposes the real <see cref="PersonaStore"/> and the underlying temp directory.</summary>
        public void Dispose()
        {
            this.Personas.Dispose();
            this.DataDir.Dispose();
        }
    }

    /// <summary>
    /// Delegates every <see cref="ITeamDirectory"/> member to a real instance, except
    /// <see cref="GetUserAsync"/>, which awaits <see cref="Gate"/> first when one is set. Lets
    /// <see cref="Approve_Busy_ButtonsDisabled"/> hold <see cref="ProposalService.ApproveAsync"/>'s
    /// proposer lookup open long enough to observe the busy state before releasing it - the same
    /// hold-a-real-await-open technique <c>TeammateCardTests</c> uses via
    /// <c>FakeModelCatalog.ModelsGate</c>, applied to the one dependency of
    /// <see cref="ProposalService"/> that is an interface rather than a sealed class.
    /// </summary>
    /// <param name="inner">The real <see cref="ITeamDirectory"/> every call is forwarded to.</param>
    private sealed class GatedTeamDirectory(ITeamDirectory inner) : ITeamDirectory
    {
        /// <summary>When set, <see cref="GetUserAsync"/> awaits this before delegating. Left <see langword="null"/> for every test but the busy one, so every other test's calls pass straight through.</summary>
        public TaskCompletionSource? Gate { get; set; }

        /// <inheritdoc />
        public Task InitializeAsync(string humanName, CancellationToken ct = default)
        {
            return inner.InitializeAsync(humanName, ct);
        }

        /// <inheritdoc />
        public Task<User> GetHumanAsync(CancellationToken ct = default)
        {
            return inner.GetHumanAsync(ct);
        }

        /// <inheritdoc />
        public Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default)
        {
            return inner.UpsertAgentUserAsync(name, description, ct);
        }

        /// <inheritdoc />
        public bool RenameUser(string userId, string newName)
        {
            return inner.RenameUser(userId, newName);
        }

        /// <inheritdoc />
        public async Task<User?> GetUserAsync(string id, CancellationToken ct = default)
        {
            if (this.Gate is { } gate)
            {
                await gate.Task.WaitAsync(ct);
            }

            return await inner.GetUserAsync(id, ct);
        }

        /// <inheritdoc />
        public Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default)
        {
            return inner.FindUserByNameAsync(name, ct);
        }

        /// <inheritdoc />
        public User? FindUserByName(string name)
        {
            return inner.FindUserByName(name);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default)
        {
            return inner.GetUsersAsync(ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken ct = default)
        {
            return inner.GetRoomsAsync(ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Room>> GetRoomsForUserAsync(string userId, CancellationToken ct = default)
        {
            return inner.GetRoomsForUserAsync(userId, ct);
        }

        /// <inheritdoc />
        public Task<Room?> GetRoomAsync(string roomId, CancellationToken ct = default)
        {
            return inner.GetRoomAsync(roomId, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<User>> GetRoomMembersAsync(string roomId, CancellationToken ct = default)
        {
            return inner.GetRoomMembersAsync(roomId, ct);
        }

        /// <inheritdoc />
        public Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, CancellationToken ct = default)
        {
            return inner.CreateRoomAsync(name, memberIds, ct);
        }

        /// <inheritdoc />
        public Task<bool> AddMemberAsync(string roomId, string userId, CancellationToken ct = default)
        {
            return inner.AddMemberAsync(roomId, userId, ct);
        }

        /// <inheritdoc />
        public Task RenameRoomAsync(string roomId, string name, CancellationToken ct = default)
        {
            return inner.RenameRoomAsync(roomId, name, ct);
        }

        /// <inheritdoc />
        public Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, CancellationToken ct = default)
        {
            return inner.FindRoomWithExactMembersAsync(humanId, agentId, ct);
        }

        /// <inheritdoc />
        public Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct = default)
        {
            return inner.FindRoomWithExactMemberSetAsync(memberIds, ct);
        }

        /// <inheritdoc />
        public Task SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default)
        {
            return inner.SetRoomArchivedAsync(roomId, archived, ct);
        }

        /// <inheritdoc />
        public Task DeleteRoomAsync(string roomId, CancellationToken ct = default)
        {
            return inner.DeleteRoomAsync(roomId, ct);
        }
    }
}

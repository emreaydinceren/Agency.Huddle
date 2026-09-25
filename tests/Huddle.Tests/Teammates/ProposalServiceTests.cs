namespace Agency.Huddle.Tests.Teammates;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins <see cref="ProposalService"/> against Spec §6.10's happy path and non-happy paths, Spec
/// §8.5's invariants (exactly once, serial, honest, wakes), Spec §14 D-7 (no rollback) and use
/// cases U2 and U5 - using real collaborators throughout (<see cref="SqliteTeamDirectory"/>,
/// <see cref="PersonaStore"/>, <see cref="FileChatStore"/>, <see cref="ChatService"/>,
/// <see cref="ProposalStore"/>) in one <see cref="TempDataDir"/> per test, the same style
/// <c>ProposeTeammatesToolTests</c> already uses for this layer.
/// </summary>
public sealed class ProposalServiceTests
{
    /// <summary>
    /// Spec §6.10's happy path: approving a Proposal of three valid Candidates creates all three
    /// Persona files, reports <see cref="ProposalOutcomeKind.Created"/>, posts the outcome as a
    /// Message from the Human that Mentions the proposer by its current Name, and leaves the
    /// Proposal store holding nothing for that Room.
    /// </summary>
    [Fact]
    public async Task Approve_ThreeValid_CreatesAllAndPostsHumanMessageMentioningProposer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates =
        [
            new Candidate("Vera", "vera", "Researcher", "You research things.", [], null),
            new Candidate("Quill", "quill", "Writer", "You write things.", [], null),
            new Candidate("Iris", "iris", "Reviewer", "You review things.", [], null),
        ];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Created, outcome.Kind);
        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Vera")));
        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Quill")));
        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Iris")));

        var human = await fixture.Directory.GetHumanAsync(ct);
        var history = await fixture.Store.ReadAllAsync(room.Id, ct);
        var last = history[^1];
        Assert.Equal(human.Id, last.SenderId);
        Assert.Equal("Approved. Created Vera, Quill and Iris. @Chief of Staff go ahead.", last.Text);

        Assert.Null(fixture.Proposals.Get(room.Id));
    }

    /// <summary>
    /// Spec §12 F-10: the library grows between propose and Approve - here, a Persona sharing
    /// Iris's Alias is added after the Proposal was made. Vera and Quill still become Teammates,
    /// Iris does not, and the outcome is honest (Spec §8.5): every Candidate ends up in
    /// <see cref="ProposalOutcome.Created"/> or <see cref="ProposalOutcome.Failed"/>, with the
    /// failure's reason model-facing text (Spec §6.8) rather than a filesystem path.
    /// </summary>
    [Fact]
    public async Task Approve_OneCollidesSinceProposing_PartlyCreatedWithReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates =
        [
            new Candidate("Vera", "vera", "Researcher", "You research things.", [], null),
            new Candidate("Quill", "quill", "Writer", "You write things.", [], null),
            new Candidate("Iris", "scout", "Reviewer", "You review things.", [], null),
        ];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        // Written AFTER proposing, sharing Iris's Alias "scout" with an unrelated Persona - the
        // library moved since propose_teammates ran, and Approve must catch it (Spec §12 F-10).
        fixture.Personas.Add(new PersonaIdentity("Rowan", "Existing Role", "scout", []), "Existing Rowan body.");

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.PartlyCreated, outcome.Kind);
        Assert.Equal(["Vera", "Quill"], outcome.Created);
        var failure = Assert.Single(outcome.Failed);
        Assert.Equal("Iris", failure.Name);
        CandidateCheckerTests.AssertProblemsAreModelFacing([failure.Reason]);
        Assert.Equal(
            "Approved. Created Vera and Quill. Could not create Iris: Persona Alias 'scout' is also used by 'Teammates/Rowan/Rowan.md'. @Chief of Staff",
            outcome.PostedText);

        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Vera")));
        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Quill")));
        Assert.False(File.Exists(fixture.Personas.Paths.DefinitionFile("Iris")));
    }

    /// <summary>
    /// Spec §6.10's OverLimit branch, worded exactly as the Spec's own example: 7 Teammates
    /// already exist, the limit is 8, and the Proposal adds 3 - nothing is attempted, so no
    /// Candidate file is ever written (Spec §14 D-7: the only all-or-nothing rule is the limit).
    /// </summary>
    [Fact]
    public async Task Approve_OverLimit_CreatesNoneAndSaysBy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct, maxTeammates: 8);
        for (var i = 1; i <= 7; i++)
        {
            fixture.Personas.Add(new PersonaIdentity($"Existing{i}", "Role", $"existing{i}", []), "Body text.");
        }

        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates =
        [
            new Candidate("Vera", "vera", "Researcher", "You research things.", [], null),
            new Candidate("Quill", "quill", "Writer", "You write things.", [], null),
            new Candidate("Iris", "iris", "Reviewer", "You review things.", [], null),
        ];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.OverLimit, outcome.Kind);
        Assert.Empty(outcome.Created);
        Assert.Empty(outcome.Failed);
        Assert.Equal(
            "Approved, but nothing was created: 7 Teammates exist, the limit is 8, and this Proposal adds 3. @Chief of Staff",
            outcome.PostedText);

        Assert.False(File.Exists(fixture.Personas.Paths.DefinitionFile("Vera")));
        Assert.False(File.Exists(fixture.Personas.Paths.DefinitionFile("Quill")));
        Assert.False(File.Exists(fixture.Personas.Paths.DefinitionFile("Iris")));
    }

    /// <summary>
    /// Spec §8.5's "exactly once" invariant and Spec §12 F-11: a second Approve of the same
    /// Proposal id - the loser of two tabs racing the same click - finds nothing, reports
    /// <see cref="ProposalOutcomeKind.Gone"/>, and posts no second Message.
    /// </summary>
    [Fact]
    public async Task Approve_SecondCall_GoneAndPostsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var first = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);
        Assert.Equal(ProposalOutcomeKind.Created, first.Kind);
        var countAfterFirst = (await fixture.Store.ReadAllAsync(room.Id, ct)).Count;

        var second = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Gone, second.Kind);
        Assert.Empty(second.Created);
        Assert.Empty(second.Failed);
        Assert.Equal(string.Empty, second.PostedText);
        var countAfterSecond = (await fixture.Store.ReadAllAsync(room.Id, ct)).Count;
        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    /// <summary>
    /// Spec §6.10's implementation notes: the Mention is resolved from
    /// <see cref="Proposal.ProposerAgentId"/> at post time, not the <see cref="Proposal.ProposerName"/>
    /// captured when the Proposal was made - a proposer renamed while its Proposal waited is still
    /// woken, under its new Name.
    /// </summary>
    [Fact]
    public async Task Approve_ProposerRenamed_MentionsNewName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var renamed = fixture.Directory.RenameUser(proposer.Id, "Ops Lead");
        Assert.True(renamed);

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Created, outcome.Kind);
        Assert.Equal("Approved. Created Vera. @Ops Lead go ahead.", outcome.PostedText);
    }

    /// <summary>
    /// Spec §12 F-17: the proposer no longer exists when Approve runs - Approve still creates the
    /// valid Candidates, and the text omits the Mention entirely rather than send a dangling '@'.
    /// </summary>
    [Fact]
    public async Task Approve_ProposerDeleted_NoMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human], ct);

        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];

        // No User is ever registered for this id - standing in for a proposer whose Persona (and
        // whose Team Directory row, in spirit) is gone by the time the Human clicks Approve.
        var proposal = ProposalServiceTests.MakeProposal(room.Id, "ghost-agent-id", "Chief of Staff", candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Created, outcome.Kind);

        // "Go ahead" is an instruction to the proposer; addressed to nobody, it is as much a
        // dangling reference as the '@' itself, so a missing proposer drops the whole trailing
        // clause, not just the Mention.
        Assert.Equal("Approved. Created Vera.", outcome.PostedText);
        Assert.DoesNotContain('@', outcome.PostedText);
    }

    /// <summary>
    /// Spec §6.10's Constraints: when <c>PostAsync</c> fails after Teammates were already created -
    /// here, because the Room was deleted mid-Approve - the failure is logged, never thrown, and
    /// <see cref="ProposalOutcome.PostedText"/> still carries the text that would have been said, so
    /// a caller can show it. No rollback (Spec §14 D-7): the Teammate created before the failure
    /// stays. The Room is deleted directly through <see cref="Fixture.Directory"/>, not through
    /// <see cref="ChatService.DeleteRoomAsync"/>, because that higher-level door also drops the
    /// pending Proposal (Spec §12 F-16) - this test needs the Proposal to still be there when
    /// Approve runs, and only the Room gone underneath it.
    /// </summary>
    [Fact]
    public async Task Approve_RoomDeletedMidApprove_LogsAndStillReturnsPostedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        await fixture.Directory.DeleteRoomAsync(room.Id, ct);

        var outcome = await fixture.Service.ApproveAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Created, outcome.Kind);
        Assert.Equal("Approved. Created Vera. @Chief of Staff go ahead.", outcome.PostedText);
        Assert.True(File.Exists(fixture.Personas.Paths.DefinitionFile("Vera")));
    }

    /// <summary>
    /// Spec §6.10's Declined template and use case U4: declining a Proposal creates nothing, posts
    /// the outcome as a Message from the Human that names every declined Candidate and Mentions the
    /// proposer, and leaves the Proposal store holding nothing for that Room. Unlike the Created,
    /// PartlyCreated and NoneCreated templates, the Declined template's own worked example joins
    /// Names with plain commas, not "and" - <c>"Declined the proposed Teammates: Vera, Quill."</c> -
    /// so this deliberately does not reuse the "A, B and C" Name-list helper the other outcomes do.
    /// </summary>
    [Fact]
    public async Task Decline_PostsDeclinedNamesAndMentionsProposer_CreatesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates =
        [
            new Candidate("Vera", "vera", "Researcher", "You research things.", [], null),
            new Candidate("Quill", "quill", "Writer", "You write things.", [], null),
        ];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var outcome = await fixture.Service.DeclineAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Declined, outcome.Kind);
        Assert.Empty(outcome.Created);
        Assert.Empty(outcome.Failed);
        Assert.Equal("Declined the proposed Teammates: Vera, Quill. @Chief of Staff", outcome.PostedText);

        Assert.False(File.Exists(fixture.Personas.Paths.DefinitionFile("Vera")));
        Assert.False(File.Exists(fixture.Personas.Paths.DefinitionFile("Quill")));
        Assert.Empty(fixture.Personas.Entries);

        Assert.Null(fixture.Proposals.Get(room.Id));
    }

    /// <summary>
    /// Spec §8.5's "exactly once" invariant, now proven for Decline too: a second Decline of the
    /// same Proposal id finds nothing, reports <see cref="ProposalOutcomeKind.Gone"/>, and posts no
    /// second Message.
    /// </summary>
    [Fact]
    public async Task Decline_SecondCall_Gone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var proposer = await fixture.Directory.UpsertAgentUserAsync("Chief of Staff", null, ct);
        Assert.NotNull(proposer);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, proposer.Id], ct);

        IReadOnlyList<Candidate> candidates = [new Candidate("Vera", "vera", "Researcher", "You research things.", [], null)];
        var proposal = ProposalServiceTests.MakeProposal(room.Id, proposer.Id, proposer.Name, candidates);
        _ = fixture.Proposals.TryPut(proposal);

        var first = await fixture.Service.DeclineAsync(room.Id, proposal.Id, ct);
        Assert.Equal(ProposalOutcomeKind.Declined, first.Kind);
        var countAfterFirst = (await fixture.Store.ReadAllAsync(room.Id, ct)).Count;

        var second = await fixture.Service.DeclineAsync(room.Id, proposal.Id, ct);

        Assert.Equal(ProposalOutcomeKind.Gone, second.Kind);
        Assert.Empty(second.Created);
        Assert.Empty(second.Failed);
        Assert.Equal(string.Empty, second.PostedText);
        var countAfterSecond = (await fixture.Store.ReadAllAsync(room.Id, ct)).Count;
        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    /// <summary>Builds a Proposal ready to store via <see cref="ProposalStore.TryPut"/>, timestamped from the real clock.</summary>
    /// <param name="roomId">The Room the Proposal is pending in.</param>
    /// <param name="proposerAgentId">The proposer's stable user id.</param>
    /// <param name="proposerName">The proposer's Name at the moment of proposing.</param>
    /// <param name="candidates">The Candidates the Proposal carries.</param>
    private static Proposal MakeProposal(string roomId, string proposerAgentId, string proposerName, IReadOnlyList<Candidate> candidates)
    {
        return new Proposal(Guid.CreateVersion7().ToString("N"), roomId, proposerAgentId, proposerName, candidates, TimeProvider.System.GetUtcNow());
    }

    /// <summary>
    /// Bundles the real collaborators one <see cref="ProposalService"/> under test needs - a
    /// <see cref="SqliteTeamDirectory"/>, a <see cref="PersonaStore"/>, a <see cref="CandidateChecker"/>,
    /// a <see cref="ProposalStore"/>, a <see cref="FileChatStore"/> and a <see cref="ChatService"/> -
    /// all sharing one <see cref="TempDataDir"/>, torn down together.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(
            TempDataDir dataDir,
            SqliteTeamDirectory directory,
            PersonaStore personas,
            ProposalStore proposals,
            FileChatStore store,
            ChatService chat,
            ProposalService service)
        {
            this.DataDir = dataDir;
            this.Directory = directory;
            this.Personas = personas;
            this.Proposals = proposals;
            this.Store = store;
            this.Chat = chat;
            this.Service = service;
        }

        /// <summary>The temp directory backing this fixture's <see cref="TeamOptions.DataDir"/>.</summary>
        public TempDataDir DataDir { get; }

        /// <summary>The real <see cref="SqliteTeamDirectory"/> a test arranges Rooms and Members through.</summary>
        public SqliteTeamDirectory Directory { get; }

        /// <summary>The real <see cref="PersonaStore"/> a test seeds existing Teammates into and reads created files back from.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The real <see cref="ProposalStore"/> a test stores a Proposal into before approving it.</summary>
        public ProposalStore Proposals { get; }

        /// <summary>The real <see cref="FileChatStore"/> a test reads the posted outcome Message back from.</summary>
        public FileChatStore Store { get; }

        /// <summary>The real <see cref="ChatService"/> the <see cref="ProposalService"/> under test posts through.</summary>
        public ChatService Chat { get; }

        /// <summary>The <see cref="ProposalService"/> under test.</summary>
        public ProposalService Service { get; }

        /// <summary>
        /// Builds a fresh <see cref="Fixture"/> with an initialised Team Directory (Human Name
        /// <c>"You"</c>) and no Personas yet loaded.
        /// </summary>
        /// <param name="ct">Cancels Team Directory initialisation.</param>
        /// <param name="maxTeammates"><see cref="AcpOptions.MaxTeammates"/> for this fixture; defaults to the Spec §7.3 default of 8.</param>
        public static async Task<Fixture> CreateAsync(CancellationToken ct, int maxTeammates = 8)
        {
            var dataDir = new TempDataDir();

            // Fully qualified, not a bare "Options.Create(...)": this type's own Options property
            // would otherwise bind first and fail with CS0120, since a static method cannot reach an
            // instance member through a simple name - ProposeTeammatesToolTests' Fixture sidesteps
            // the same hazard the same way.
            IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(new TeamOptions
            {
                DataDir = dataDir.Path,
                Acp = new AcpOptions { MaxTeammates = maxTeammates },
            });

            var directory = new SqliteTeamDirectory(options);
            await directory.InitializeAsync("You", ct);

            var personas = new PersonaStore(
                new TeammatePaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);
            var gateway = new FakeAgentGateway();
            var checker = new CandidateChecker(personas, directory, gateway);
            var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            var proposals = new ProposalStore(events);
            var store = new FileChatStore(options, NullLogger<FileChatStore>.Instance);
            var chat = new ChatService(directory, store, events, personas, options, proposals, NullLogger<ChatService>.Instance);
            var service = new ProposalService(proposals, checker, personas, chat, directory, options, NullLogger<ProposalService>.Instance);

            return new Fixture(dataDir, directory, personas, proposals, store, chat, service);
        }

        /// <summary>Disposes the real <see cref="PersonaStore"/> and the underlying temp directory.</summary>
        public void Dispose()
        {
            this.Personas.Dispose();
            this.DataDir.Dispose();
        }
    }
}

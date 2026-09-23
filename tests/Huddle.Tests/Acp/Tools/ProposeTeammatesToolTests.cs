namespace Agency.Huddle.Tests.Acp.Tools;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins <see cref="ProposeTeammatesTool"/> against Spec §6.9's internal flow, Spec §5.3
/// Contract B's return shape (always a string, never a throw), and use cases U3 (the same
/// proposer replaces its own pending Proposal) and U6 (another Agent is refused, naming the
/// proposer) - using real collaborators throughout (<see cref="SqliteTeamDirectory"/>,
/// <see cref="PersonaStore"/>, <see cref="ProposalStore"/>,
/// <see cref="CandidateChecker"/>), the same style <c>CreateRoomToolTests</c> and
/// <c>CandidateCheckerTests</c> already use for this App Tool layer.
/// </summary>
public sealed class ProposeTeammatesToolTests
{
    /// <summary>Spec §6.9 internal flow, first branch: the Room named in the call does not exist.</summary>
    [Fact]
    public async Task InvokeAsync_UnknownRoom_ReturnsUnknownRoomText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments("no-such-room", MakeCandidates(1));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Unknown room 'no-such-room'.", result);
    }

    /// <summary>Spec §6.9 internal flow, second branch: the Room exists, but the caller is not one of its Members.</summary>
    [Fact]
    public async Task InvokeAsync_CallerNotAMember_ReturnsNotAMemberText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var member = await fixture.Directory.UpsertAgentUserAsync("Member", null, ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(member);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, member.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments(room.Id, MakeCandidates(1));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("You are not a Member of that Room.", result);
    }

    /// <summary>Spec §6.9 internal flow, third branch: the caller is a Member, but the Room is Archived.</summary>
    [Fact]
    public async Task InvokeAsync_RoomArchived_ReturnsArchivedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments(room.Id, MakeCandidates(1));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("That Room is Archived.", result);
    }

    /// <summary>
    /// Spec §6.9 internal flow: a Candidate with three independent faults - an invalid Name, a
    /// blank Title, and a Body starting with <c>---</c> - reports every problem, one per line, and
    /// nothing is stored: a Proposal is never partially accepted.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_InvalidCandidate_ReturnsProblemsOnePerLineAndStoresNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var badCandidate = new JsonObject
        {
            ["name"] = "Vera.",
            ["alias"] = "vera",
            ["title"] = "   ",
            ["body"] = "---\nSecond frontmatter block?",
        };
        var arguments = MakeArguments(room.Id, new JsonArray(badCandidate));

        var result = await tool.InvokeAsync(arguments, ct);

        var lines = result.Split('\n');
        Assert.True(lines.Length >= 3, $"Expected at least 3 problem lines; got {lines.Length}: {result}");
        Assert.Contains(lines, line => line.Contains("is not a valid Name", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("blank Title", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Body", StringComparison.Ordinal) && line.Contains("---", StringComparison.Ordinal));
        Assert.Null(fixture.Proposals.Get(room.Id));
    }

    /// <summary>
    /// Spec §6.9 implementation notes: a Proposal carries one to four Candidates. Zero and five are
    /// both out of range and report the same text, whether the count is too small or too large.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task InvokeAsync_CandidateCountOutOfRange_ReturnsBetweenOneAndFourText(int count)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments(room.Id, MakeCandidates(count));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Propose between one and four Candidates.", result);
        Assert.Null(fixture.Proposals.Get(room.Id));
    }

    /// <summary>
    /// Spec §6.9 internal flow: five Personas already exist, the limit is 8, and four more
    /// Candidates are proposed - 5 + 4 exceeds 8, worded exactly as the Spec's own example, naming
    /// how many more would actually fit (3).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OverLimit_ReturnsLimitText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct, maxTeammates: 8);
        for (var i = 1; i <= 5; i++)
        {
            fixture.Personas.Add(new PersonaIdentity($"Existing{i}", "Role", $"existing{i}", []), "Body text.");
        }

        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments(room.Id, MakeCandidates(4));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("5 Teammates exist and the limit is 8; propose at most 3.", result);
        Assert.Null(fixture.Proposals.Get(room.Id));
    }

    /// <summary>Spec §7.3: <c>MaxTeammates</c> of zero or less disables the limit entirely.</summary>
    [Fact]
    public async Task InvokeAsync_MaxTeammatesZero_DisablesTheLimit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct, maxTeammates: 0);
        for (var i = 1; i <= 10; i++)
        {
            fixture.Personas.Add(new PersonaIdentity($"Existing{i}", "Role", $"existing{i}", []), "Body text.");
        }

        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments(room.Id, MakeCandidates(4));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.StartsWith("Proposed 4 Teammates", result, StringComparison.Ordinal);
        Assert.NotNull(fixture.Proposals.Get(room.Id));
    }

    /// <summary>
    /// Spec §6.9's own worked example: a valid Proposal of two Candidates succeeds, the success
    /// text starts exactly as the Spec shows it, and <see cref="ProposalStore.Get"/> now holds
    /// exactly those two Candidates, in order.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ValidProposal_ReturnsSuccessTextAndStoresIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var arguments = MakeArguments(room.Id, new JsonArray(MakeCandidateJson("Vera", "vee"), MakeCandidateJson("Quill", "quill")));

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.StartsWith("Proposed 2 Teammates (Vera, Quill) in Room", result, StringComparison.Ordinal);
        var stored = fixture.Proposals.Get(room.Id);
        Assert.NotNull(stored);
        Assert.Equal(["Vera", "Quill"], stored.Candidates.Select(c => c.Name));
        Assert.Equal(caller.Id, stored.ProposerAgentId);
        Assert.Equal("Nova", stored.ProposerName);
    }

    /// <summary>
    /// U3: the same Agent revises and proposes again in the same Room before the Human answers -
    /// the second call replaces the first, and the Room ends up holding only the new Candidates.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_SameCallerProposesAgain_Replaces()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var caller = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(caller);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, caller.Id], ct);
        var tool = fixture.CreateTool(caller.Id);
        var first = MakeArguments(room.Id, new JsonArray(MakeCandidateJson("Vera", "vee"), MakeCandidateJson("Quill", "quill")));
        _ = await tool.InvokeAsync(first, ct);

        var second = MakeArguments(room.Id, new JsonArray(MakeCandidateJson("Iris", "iris"), MakeCandidateJson("Sable", "sable")));
        var result = await tool.InvokeAsync(second, ct);

        Assert.StartsWith("Proposed 2 Teammates (Iris, Sable) in Room", result, StringComparison.Ordinal);
        var stored = fixture.Proposals.Get(room.Id);
        Assert.NotNull(stored);
        Assert.Equal(["Iris", "Sable"], stored.Candidates.Select(c => c.Name));
    }

    /// <summary>
    /// U6: another Agent tries to propose while Nova's Proposal is still pending in the same Room -
    /// refused, worded exactly as the Spec's own example, and Nova's Proposal is left untouched.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AnotherCallerProposesWhilePending_ReturnsRefusedNamingProposer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture fixture = await Fixture.CreateAsync(ct);
        var nova = await fixture.Directory.UpsertAgentUserAsync("Nova", null, ct);
        var sable = await fixture.Directory.UpsertAgentUserAsync("Sable", null, ct);
        Assert.NotNull(nova);
        Assert.NotNull(sable);
        var room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, nova.Id, sable.Id], ct);
        var novaTool = fixture.CreateTool(nova.Id);
        var first = MakeArguments(room.Id, new JsonArray(MakeCandidateJson("Vera", "vee"), MakeCandidateJson("Quill", "quill")));
        _ = await novaTool.InvokeAsync(first, ct);

        var sableTool = fixture.CreateTool(sable.Id);
        var second = MakeArguments(room.Id, new JsonArray(MakeCandidateJson("Iris", "iris")));
        var result = await sableTool.InvokeAsync(second, ct);

        Assert.Equal("A Proposal from Nova is already waiting in this Room.", result);
        var stored = fixture.Proposals.Get(room.Id);
        Assert.NotNull(stored);
        Assert.Equal("Nova", stored.ProposerName);
        Assert.Equal(["Vera", "Quill"], stored.Candidates.Select(c => c.Name));
    }

    /// <summary>Builds one valid Candidate JSON object with the given Name and Alias.</summary>
    /// <param name="name">The Candidate's Name.</param>
    /// <param name="alias">The Candidate's Alias.</param>
    private static JsonObject MakeCandidateJson(string name, string alias)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["alias"] = alias,
            ["title"] = "Researcher",
            ["body"] = "You research things.",
        };
    }

    /// <summary>Builds <paramref name="count"/> valid, mutually distinct Candidate JSON objects.</summary>
    /// <param name="count">How many Candidates to build.</param>
    private static JsonArray MakeCandidates(int count)
    {
        var array = new JsonArray();
        for (var i = 1; i <= count; i++)
        {
            array.Add(MakeCandidateJson($"Cand{i}", $"cand{i}"));
        }

        return array;
    }

    /// <summary>Builds a <c>propose_teammates</c> call's arguments for <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room to propose into.</param>
    /// <param name="candidates">The Candidates array to send.</param>
    private static JsonObject MakeArguments(string roomId, JsonArray candidates)
    {
        return new JsonObject
        {
            ["roomId"] = roomId,
            ["candidates"] = candidates,
        };
    }

    /// <summary>
    /// Bundles the real collaborators one <see cref="ProposeTeammatesTool"/> under test needs -
    /// a <see cref="SqliteTeamDirectory"/>, a <see cref="PersonaStore"/>, a
    /// <see cref="CandidateChecker"/> built on both, and a fresh <see cref="ProposalStore"/> - all
    /// sharing one <see cref="TempDataDir"/> and one configured <see cref="TeamOptions.Acp"/>'s
    /// <see cref="AcpOptions.MaxTeammates"/>, torn down together.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(
            TempDataDir dataDir,
            SqliteTeamDirectory directory,
            PersonaStore personas,
            CandidateChecker checker,
            ProposalStore proposals,
            IOptions<TeamOptions> options)
        {
            this.DataDir = dataDir;
            this.Directory = directory;
            this.Personas = personas;
            this.Checker = checker;
            this.Proposals = proposals;
            this.Options = options;
        }

        /// <summary>The temp directory backing this fixture's <see cref="TeamOptions.DataDir"/>.</summary>
        public TempDataDir DataDir { get; }

        /// <summary>The real <see cref="SqliteTeamDirectory"/> a test arranges Rooms and Members through.</summary>
        public SqliteTeamDirectory Directory { get; }

        /// <summary>The real <see cref="PersonaStore"/> a test seeds existing Teammates into.</summary>
        public PersonaStore Personas { get; }

        /// <summary>The real <see cref="CandidateChecker"/> the tool under test is built on.</summary>
        public CandidateChecker Checker { get; }

        /// <summary>The real <see cref="ProposalStore"/> a test reads back after calling the tool.</summary>
        public ProposalStore Proposals { get; }

        /// <summary>The <see cref="TeamOptions"/> this fixture's collaborators were all built from.</summary>
        public IOptions<TeamOptions> Options { get; }

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
            // (below) would otherwise bind first and fail with CS0120, since a static method cannot
            // reach an instance member through a simple name - TempDataDir.Options() sidesteps the
            // same hazard by never calling the factory from inside its own body.
            IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(new TeamOptions
            {
                DataDir = dataDir.Path,
                Acp = new AcpOptions { MaxTeammates = maxTeammates },
            });

            var directory = new SqliteTeamDirectory(options);
            await directory.InitializeAsync("You", ct);

            var personas = new PersonaStore(
                options, new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);
            var gateway = new FakeAgentGateway();
            var checker = new CandidateChecker(personas, directory, gateway);
            var events = new RoomEvents(NullLogger<RoomEvents>.Instance);
            var proposals = new ProposalStore(events);

            return new Fixture(dataDir, directory, personas, checker, proposals, options);
        }

        /// <summary>Builds a <see cref="ProposeTeammatesTool"/> calling as <paramref name="callerAgentId"/>, wired to this fixture's collaborators.</summary>
        /// <param name="callerAgentId">The Agent id the returned tool invokes as.</param>
        public ProposeTeammatesTool CreateTool(string callerAgentId)
        {
            return new ProposeTeammatesTool(
                this.Proposals, this.Checker, this.Personas, this.Directory, this.Options, TimeProvider.System, callerAgentId, new FakePromptSource());
        }

        /// <summary>Disposes the real <see cref="PersonaStore"/> and the underlying temp directory.</summary>
        public void Dispose()
        {
            this.Personas.Dispose();
            this.DataDir.Dispose();
        }
    }
}

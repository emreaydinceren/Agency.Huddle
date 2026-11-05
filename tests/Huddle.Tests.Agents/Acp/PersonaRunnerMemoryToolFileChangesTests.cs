using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Pipes;
using Agency.Huddle.Tests.Waiting;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins that a save through the <c>memory</c> App Tool counts as the Agent's own for File Changes (Instructions spec §6.1),
/// recorded by the Room Session from the tool-call event as <c>claude-agent-acp</c> reports an MCP tool: kind
/// <see cref="ToolKind.Other"/>, the tool's name as the title. Built like <see cref="PersonaRunnerFileChangesTests"/>: a real
/// pipe, the fixture's own <see cref="FileChangeTracker"/>, and a fake Adapter whose tool call the test answers by changing
/// the file itself, since the fake runs no tool.
/// </summary>
public sealed class PersonaRunnerMemoryToolFileChangesTests
{
    private const string PrefixedName = "mcp__team__memory";

    /// <summary>A memory <c>write</c> in Room A puts the resolved file in the Turn's touched set: the commit records Room A as its writer.</summary>
    [Fact]
    public async Task MemoryWriteInRoomA_RecordsTheResolvedFileAsWrittenFromRoomA()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync(PrefixedName, MemoryInput("write"), mutate: scenario.RewriteNotes, cts.Token);
        await scenario.NextTurnInRoomAAsync(cts.Token);

        FileState? state = scenario.Services.GetRequiredService<FileStateStore>().Load("nova");
        Assert.NotNull(state);
        Assert.True(state.Writers.TryGetValue("nova", out IReadOnlyDictionary<string, FileWriter>? byPath));
        Assert.True(byPath.TryGetValue(Path.Combine("memory", "notes.md"), out FileWriter? writer));
        Assert.Equal(scenario.RoomAId, writer.RoomId);
    }

    /// <summary>
    /// The two symptoms, end to end: after Joe saves through the tool in Room A, its next Turn in Room A lists no File Change
    /// for the file, and its next Turn in Room B lists it with <c>(by you, in Room 'A')</c>.
    /// </summary>
    [Fact]
    public async Task MemoryWriteInRoomA_UnlistedInA_ListedInBAsByYou()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: true, cts);
        await scenario.RunMemoryTurnInRoomAAsync(PrefixedName, MemoryInput("write"), mutate: scenario.RewriteNotes, cts.Token);
        string nextInA = await scenario.NextTurnInRoomAAsync(cts.Token);
        string nextInB = await scenario.NextTurnInRoomBAsync(cts.Token);

        string roomAName = await scenario.RoomANameAsync(cts.Token);
        string suffix = new FakePromptSource().Render("turn.fileByYouSuffix", new Dictionary<string, string>(StringComparer.Ordinal) { ["{{roomName}}"] = roomAName });

        // contains-ok: prompt text, not markup; the File Changes line for one file is under test, not the rest of the prompt.
        Assert.DoesNotContain(scenario.NotesPath, nextInA, StringComparison.Ordinal);

        // contains-ok: prompt text, not markup; the line's path and its by-you suffix are under test, not the rest of the prompt.
        Assert.Contains(scenario.NotesPath + suffix, nextInB, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only a mutating command of the memory tool is recorded, under its prefixed or its bare name. <c>read</c> and <c>list</c>,
    /// another <see cref="ToolKind.Other"/> tool whose input happens to carry <c>folder</c> and <c>file</c>, and input that does
    /// not parse record nothing, so the change is listed back in Room A like any other.
    /// </summary>
    /// <param name="title">The tool call's title, which carries the tool's name.</param>
    /// <param name="rawInput">The tool call's raw input.</param>
    /// <param name="recorded">Whether the change is the Agent's own, and so not listed back.</param>
    [Theory]
    [InlineData(PrefixedName, """{"command":"write","folder":"me","file":"notes.md","description":"d","text":"t"}""", true)]
    [InlineData("memory", """{"command":"write","folder":"me","file":"notes.md","description":"d","text":"t"}""", true)]
    [InlineData(PrefixedName, """{"command":"append","file":"notes.md","text":"t"}""", true)]
    [InlineData(PrefixedName, """{"command":"replace","folder":"me","file":"notes.md","old":"a","new":"b"}""", true)]
    [InlineData(PrefixedName, """{"command":"read","folder":"me","file":"notes.md"}""", false)]
    [InlineData(PrefixedName, """{"command":"list","folder":"me","file":"notes.md"}""", false)]
    [InlineData("mcp__team__notebook", """{"command":"write","folder":"me","file":"notes.md","description":"d","text":"t"}""", false)]
    [InlineData(PrefixedName, """{"command":"write","folder":"me","file":"notes.md",""", false)]
    public async Task MemoryToolCall_RecordsOnlyAMutationOfTheMemoryTool(string title, string rawInput, bool recorded)
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync(title, rawInput, mutate: scenario.RewriteNotes, cts.Token);
        string nextInA = await scenario.NextTurnInRoomAAsync(cts.Token);

        // contains-ok: prompt text, not markup; whether one file's File Changes line is present is under test.
        Assert.Equal(!recorded, nextInA.Contains(scenario.NotesPath, StringComparison.Ordinal));
    }

    /// <summary>
    /// The Adapter's tool name wins over its display title (Spec §5): a memory save titled with display text, whose
    /// <c>ToolName</c> is the memory tool's prefixed name, is still the Agent's own change and is not listed back in Room A.
    /// </summary>
    [Fact]
    public async Task MemoryCall_ToolNameSet_TitleIsDisplayText_StillAttributed()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync("Remember a fact", MemoryInput("write"), mutate: scenario.RewriteNotes, cts.Token, toolName: PrefixedName);
        string nextInA = await scenario.NextTurnInRoomAAsync(cts.Token);

        // contains-ok: prompt text, not markup; whether one file's File Changes line is present is under test.
        Assert.DoesNotContain(scenario.NotesPath, nextInA, StringComparison.Ordinal);
    }

    /// <summary>
    /// A set tool name is authoritative (Spec §5): a call titled like the memory tool but named <c>Bash</c> is not a memory
    /// save, so its file change is listed back in Room A.
    /// </summary>
    [Fact]
    public async Task MemoryCall_ToolNameOther_TitleLooksLikeMemory_NotAttributed()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync(PrefixedName, MemoryInput("write"), mutate: scenario.RewriteNotes, cts.Token, toolName: "Bash");
        string nextInA = await scenario.NextTurnInRoomAAsync(cts.Token);

        // contains-ok: prompt text, not markup; whether one file's File Changes line is present is under test.
        Assert.Contains(scenario.NotesPath, nextInA, StringComparison.Ordinal);
    }

    /// <summary>With no tool name (an Adapter that sends no <c>_meta</c>) the title still identifies the memory tool, as before.</summary>
    [Fact]
    public async Task MemoryCall_NoToolName_TitleFallbackStillWorks()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync(PrefixedName, MemoryInput("write"), mutate: scenario.RewriteNotes, cts.Token, toolName: null);
        string nextInA = await scenario.NextTurnInRoomAAsync(cts.Token);

        // contains-ok: prompt text, not markup; whether one file's File Changes line is present is under test.
        Assert.DoesNotContain(scenario.NotesPath, nextInA, StringComparison.Ordinal);
    }

    /// <summary>A memory <c>delete</c> records the path: the file's removal is not listed back in Room A as a deletion.</summary>
    [Fact]
    public async Task MemoryDeleteInRoomA_RecordsThePath_DeletionNotListedBack()
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync(PrefixedName, MemoryInput("delete"), mutate: () => File.Delete(scenario.NotesPath), cts.Token);
        string nextInA = await scenario.NextTurnInRoomAAsync(cts.Token);

        // contains-ok: prompt text, not markup; whether one file's File Changes line is present is under test.
        Assert.DoesNotContain(scenario.NotesPath, nextInA, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every memory call, a <c>read</c> as well as a <c>write</c>, carries the file it names into the lasting tool-call log. The
    /// Adapter reports no location for an App Tool, so the log used to show the call with empty arguments.
    /// </summary>
    /// <param name="command">The memory command.</param>
    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task MemoryToolCall_LogsTheFileItNames(string command)
    {
        using BehaviourBudget cts = new(TimeSpan.FromSeconds(30));

        await using Scenario scenario = await Scenario.StartAsync(alsoRoomB: false, cts);
        await scenario.RunMemoryTurnInRoomAAsync(PrefixedName, MemoryInput(command), mutate: static () => { }, cts.Token);

        Assert.NotNull(scenario.Services.GetRequiredService<ToolCallLogWriter>().FindLatest("nova", scenario.NotesPath));
    }

    /// <summary>A memory call's raw input for <paramref name="command"/> on <c>notes.md</c> in the caller's own folder.</summary>
    /// <param name="command">The command.</param>
    private static string MemoryInput(string command) =>
        $$"""{"command":"{{command}}","folder":"me","file":"notes.md","description":"Notes kept between Turns.","text":"v2","version":"abc"}""";

    /// <summary>
    /// One run: Nova with Room A (its direct Room) and optionally Room B, both seeded with a Turn so each has a baseline holding
    /// <c>memory/notes.md</c> as first written.
    /// </summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private readonly PipeHostFixture fixture;
        private readonly FakeAgentHostFactory factory;
        private readonly FakeTimeProvider clock;
        private readonly PersonaRunner runner;
        private int roomAMessages;
        private int roomBMessages;

        /// <summary>Initializes a new instance of the <see cref="Scenario"/> class.</summary>
        private Scenario(PipeHostFixture fixture, FakeAgentHostFactory factory, FakeTimeProvider clock, PersonaRunner runner, string notesPath)
        {
            this.fixture = fixture;
            this.factory = factory;
            this.clock = clock;
            this.runner = runner;
            this.NotesPath = notesPath;
        }

        /// <summary>The container of the running app.</summary>
        public IServiceProvider Services => this.fixture.Services;

        /// <summary>The full path of <c>notes.md</c> in Nova's own memory folder.</summary>
        public string NotesPath { get; }

        /// <summary>Nova's direct Room.</summary>
        public string RoomAId { get; private set; } = string.Empty;

        /// <summary>The second Room Nova is in, or empty when the run has none.</summary>
        public string RoomBId { get; private set; } = string.Empty;

        /// <summary>Starts the app and Nova, writes <c>notes.md</c>, and seeds each Room's baseline with one Turn.</summary>
        /// <param name="alsoRoomB">Whether to create and seed Room B.</param>
        /// <param name="budget">The test's behaviour budget, paused while the fixture starts and then bounding the rest of the start.</param>
        public static async Task<Scenario> StartAsync(bool alsoRoomB, BehaviourBudget budget)
        {
            PipeHostFixture fixture = await PipeHostFixture.StartAsync(budget);
            CancellationToken ct = budget.Token;
            IOptions<TeamOptions> options = fixture.Services.GetRequiredService<IOptions<TeamOptions>>();
            string memoryDir = Path.Combine(new AgentPaths(options).WorkDir("nova"), "memory");
            Directory.CreateDirectory(memoryDir);
            string notesPath = Path.Combine(memoryDir, "notes.md");
            File.WriteAllText(notesPath, "v1");

            FakeTimeProvider clock = new();
            FakeAgentHostFactory factory = new(clock);
            FileChangeTracker tracker = fixture.Services.GetRequiredService<FileChangeTracker>();
            ILogger<PersonaRunner> logger = fixture.Services.GetRequiredService<ILogger<PersonaRunner>>();
            FakePromptSource prompts = new();
            ILoggerFactory loggerFactory = fixture.Services.GetRequiredService<ILoggerFactory>();
            TurnHookPipeline hooks = TurnHookPipeline.ForPersona(
                new TurnHookScope("nova", ReadsFiles: true, FileChangesOn: true, DeclaredWatches: []),
                null,
                new FileChangesTurnHook(tracker, prompts, loggerFactory.CreateLogger<FileChangesTurnHook>()),
                loggerFactory.CreateLogger<TurnHookPipeline>());
            PersonaRunner runner = new(TestPersonas.Nova, options, factory, prompts, new RoomFollows(), logger, hooks: hooks, memoryPaths: tracker.MemoryPaths);

            Scenario scenario = new(fixture, factory, clock, runner, notesPath);
            await runner.StartAsync(ct);

            (string novaId, string roomAId) = await Eventually.DirectRoomAsync(fixture.Services, "nova", ct);
            scenario.RoomAId = roomAId;
            if (alsoRoomB)
            {
                Room roomB = await fixture.Services.GetRequiredService<ITeamDirectory>().CreateRoomAsync("Room B", [KnownIds.Human, novaId], ct);
                scenario.RoomBId = roomB.Id;
            }

            _ = await scenario.NextTurnInRoomAAsync(ct);
            if (alsoRoomB)
            {
                _ = await scenario.NextTurnInRoomBAsync(ct);
            }

            return scenario;
        }

        /// <summary>Rewrites <c>notes.md</c> with a different size, as a memory save would.</summary>
        public void RewriteNotes() => File.WriteAllText(this.NotesPath, "v2, saved through the tool");

        /// <summary>The current name of Room A.</summary>
        /// <param name="ct">Cancels the lookup.</param>
        public async Task<string> RoomANameAsync(CancellationToken ct)
        {
            Room? room = await this.Services.GetRequiredService<ITeamDirectory>().GetRoomAsync(this.RoomAId, ct);
            Assert.NotNull(room);
            return room.Name;
        }

        /// <summary>
        /// Runs a Turn in Room A whose Adapter reports one <see cref="ToolKind.Other"/> tool call titled <paramref name="title"/>
        /// with <paramref name="rawInput"/>, and changes the file mid-Turn while the call is reported complete.
        /// </summary>
        /// <param name="title">The tool call's title.</param>
        /// <param name="rawInput">The tool call's raw input.</param>
        /// <param name="mutate">What the call did to the file.</param>
        /// <param name="ct">Cancels the Turn.</param>
        /// <param name="toolName">The Adapter's own tool name carried on both events, or null when it sends none.</param>
        public async Task RunMemoryTurnInRoomAAsync(string title, string rawInput, Action mutate, CancellationToken ct, string? toolName = null)
        {
            string sessionId = this.factory.Session.SessionId;
            this.factory.Session.EnqueueToolActivity(
                new ToolCallStarted(sessionId, "m1", title, ToolKind.Other, ToolCallStatus.Pending, "{}", ToolName: toolName),
                new ToolCallUpdated(sessionId, "m1", title, ToolKind.Other, ToolCallStatus.Completed, null, rawInput, ToolName: toolName));
            this.factory.Session.EnqueueDripFedReply(TimeSpan.FromMilliseconds(300), "saved");

            Drafts drafts = this.Services.GetRequiredService<Drafts>();
            await this.Services.GetRequiredService<ChatService>().PostAsync(this.RoomAId, KnownIds.Human, "remember this", ct: ct);
            await Eventually.UntilAsync(() => drafts.ForRoom(this.RoomAId).Any(draft => draft.ToolStatus == ToolActivityStatus.Completed), ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(20));
            mutate();

            this.roomAMessages += 2;
            await Eventually.HistoryCountAsync(this.Services.GetRequiredService<IChatStore>(), this.RoomAId, this.roomAMessages, ct, onTick: () => this.clock.Advance(TimeSpan.FromSeconds(1)));
        }

        /// <summary>Runs an ordinary Turn in Room A and returns its prompt.</summary>
        /// <param name="ct">Cancels the Turn.</param>
        public Task<string> NextTurnInRoomAAsync(CancellationToken ct)
        {
            this.roomAMessages += 2;
            return this.OrdinaryTurnAsync(this.RoomAId, this.roomAMessages, ct);
        }

        /// <summary>Runs an ordinary Turn in Room B and returns its prompt.</summary>
        /// <param name="ct">Cancels the Turn.</param>
        public Task<string> NextTurnInRoomBAsync(CancellationToken ct)
        {
            this.roomBMessages += 2;
            return this.OrdinaryTurnAsync(this.RoomBId, this.roomBMessages, ct);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await this.runner.DisposeAsync();
            await this.fixture.DisposeAsync();
        }

        /// <summary>Posts to a Room, waits for the reply, and returns the prompt the Turn sent.</summary>
        /// <param name="roomId">The Room.</param>
        /// <param name="messages">How many Messages the Room holds once the reply is posted.</param>
        /// <param name="ct">Cancels the Turn.</param>
        private async Task<string> OrdinaryTurnAsync(string roomId, int messages, CancellationToken ct)
        {
            int before = this.factory.Session.Prompts.Count;
            this.factory.Session.EnqueueReply("ok");
            await this.Services.GetRequiredService<ChatService>().PostAsync(roomId, KnownIds.Human, "hi", ct: ct);
            await Eventually.HistoryCountAsync(this.Services.GetRequiredService<IChatStore>(), roomId, messages, ct);
            Assert.Equal(before + 1, this.factory.Session.Prompts.Count);
            return this.factory.Session.Prompts[^1];
        }
    }
}

using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;
using Microsoft.Extensions.Options;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// Pins the Referenced items header in a Turn's prompt (spec section 9.1, 9.3, AT-T17): where it sits, which text it
/// is built from, and that a Command, a Greeting, a switched-off option or a Message with no reference leaves the
/// prompt exactly as it was.
/// </summary>
public sealed class BuildPromptReferencesTests
{
    private static readonly Persona Nova = new("nova", "You are Nova.");

    private static TaskItem TaskFile(string id, string title) => Tasks.TestTasks.Make(
        id: id,
        title: title,
        status: TaskState.InProgress,
        assignee: "Nova",
        location: new TaskLocation("Platform", null, false));

    private static WorkItem MessageWith(string text, IReadOnlyList<CaughtUpMessage>? missed = null) =>
        new("room-1", "Room 1", "Human", text, missed ?? [], TriggerMessageId: "trig-1");

    private static ReferenceExpander ExpanderOver(ReferenceFixture fixture, Action<TeamOptions>? configure = null)
    {
        TeamOptions options = new();
        configure?.Invoke(options);
        return new ReferenceExpander(fixture.Resolver(), new FakePromptSource(), Options.Create(options));
    }

    private static async Task<AgentPrompt> RunTurnAsync(WorkItem item, ReferenceExpander? expander, bool usesPrefix = true, Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentSession session = new();
        session.EnqueueReply("ok");
        AdapterProfile profile = new("claude", "Claude", null, "node", null, null, usesPrefix);
        FakePersonaHost host = new(session, profile);
        using CancellationTokenSource runCts = new();
        await using RoomSession room = new(
            roomId: "room-1",
            open: _ => Task.FromResult<IAgentSession>(session),
            owner: new FakeRoomSessionOwner(),
            scheduler: new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            runToken: runCts.Token,
            persona: Nova,
            host: host,
            references: expander);

        room.Enqueue(new QueuedWork(1, item));
        await Eventually.UntilAsync(() => session.AgentPrompts.Count == 1, ct, timeout: TestBudgets.Slow);
        await runCts.CancelAsync();
        return Assert.Single(session.AgentPrompts);
    }

    private static string HintLine(string prompt) =>
        prompt.Split('\n').Single(line => line.StartsWith("To learn more", StringComparison.Ordinal));

    /// <summary>The header sits after the Library documents block and before the Message line.</summary>
    [Fact]
    public void Header_AfterLibraryDocs_BeforeMessage()
    {
        LibraryDocumentItem document = new("E:\\Data\\notes.md", "pinned root \"Docs\"", "1 KB", null, false, 1024, false, LibraryDocumentDelivery.Path, null);
        WorkItem item = BuildPromptReferencesTests.MessageWith("see PLAT-0001") with
        {
            LibraryDocuments = new LibraryDocumentsReport([document], NotListed: 0, MaxInlineBytes: 16384),
            ReferencedItems = "HEADER-TEXT",
        };

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        int library = prompt.IndexOf("E:\\Data\\notes.md", StringComparison.Ordinal);
        int header = prompt.IndexOf("HEADER-TEXT\n\n", StringComparison.Ordinal);
        int message = prompt.IndexOf("see PLAT-0001", StringComparison.Ordinal);
        Assert.True(library >= 0 && library < header && header < message, prompt);
    }

    /// <summary>Only the references in the triggering Message are expanded, not those in the catch-up lines, and a repeated one is listed once.</summary>
    [Fact]
    public async Task Header_OnlyTriggerReferences_NotCatchUpOrTranscript()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(
            TestContext.Current.CancellationToken, null, BuildPromptReferencesTests.TaskFile("PLAT-0001", "Fix login"), BuildPromptReferencesTests.TaskFile("PLAT-0002", "Other thing"));
        WorkItem item = BuildPromptReferencesTests.MessageWith("look at PLAT-0001 and PLAT-0001", [new CaughtUpMessage("Luna", "also PLAT-0002")]);

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, BuildPromptReferencesTests.ExpanderOver(fixture));

        Assert.Contains("\"Fix login\"", prompt.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.DoesNotContain("Other thing", prompt.Text, StringComparison.Ordinal);
        Assert.Equal(1, prompt.Text.Split("\"Fix login\"").Length - 1);
    }

    /// <summary>With Tasks off, a Task-shaped word and a Room link give the Room line only: no Task line and no false more line (findings 2 and 5).</summary>
    [Fact]
    public async Task TasksOff_LookalikeAndRoom_NoTaskLineNoMoreLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        ReferenceExpander expander = BuildPromptReferencesTests.ExpanderOver(fixture, static options => options.Tasks.Enabled = false);

        string? header = await expander.ExpandAsync($"use UTF-8 and [Launch](/rooms/{room.Id})", true, ct);

        Assert.NotNull(header);
        Assert.DoesNotContain("not listed", header, StringComparison.Ordinal);
        Assert.DoesNotContain("UTF", header, StringComparison.Ordinal);
    }

    /// <summary>The header never lists Library files, so building it never asks the Library resolver about one (no disk check per Turn), while the Room line still appears.</summary>
    [Fact]
    public async Task LibraryPathAndRoom_HeaderNeverTouchesTheLibraryResolver()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        int asked = 0;
        Library.FakeLibraryNoteResolver recording = new(resolvePath: _ =>
        {
            asked++;
            return null;
        });
        ReferenceExpander expander = new(fixture.Resolver(libraryOverride: recording), new FakePromptSource(), Options.Create(new TeamOptions()));

        string? header = await expander.ExpandAsync($"see `C:\\Docs\\plan.md` and [Launch](/rooms/{room.Id})", true, ct);

        Assert.NotNull(header);
        Assert.Equal(0, asked);
    }

    /// <summary>Ordinary tokens such as UTF-8 and a path outside the Library never reach the header, so a Message with only those gets none (findings 1 and 2).</summary>
    [Fact]
    public async Task LookalikesAndForeignPath_GiveNoHeader()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        ReferenceExpander expander = BuildPromptReferencesTests.ExpanderOver(fixture);

        string? header = await expander.ExpandAsync("UTF-8, SHA-256 and `C:\\Repos\\x.cs`", true, ct);

        Assert.Null(header);
    }

    /// <summary>A Command Turn gets the bare command and no header, even when the Message text carries a reference.</summary>
    [Fact]
    public async Task CommandTurn_NoHeader()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(
            TestContext.Current.CancellationToken, null, BuildPromptReferencesTests.TaskFile("PLAT-0001", "Fix login"));
        WorkItem item = new(
            "room-1",
            "Room 1",
            "Human",
            "@nova /compact PLAT-0001",
            [],
            WorkItemKind.Command,
            TriggerMessageId: "trig-1",
            Command: new AdapterCommandCall("compact", "PLAT-0001"));

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, BuildPromptReferencesTests.ExpanderOver(fixture));

        Assert.Equal("/compact PLAT-0001", prompt.Text);
    }

    /// <summary>A Command Turn never looks its text up: with a catalog that throws, the bare command still runs and no expansion is logged (spec section 9.1, traps.md).</summary>
    [Fact]
    public async Task CommandTurn_ReferenceIsNotLookedUp()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(TestContext.Current.CancellationToken);
        ReferenceExpander expander = new(fixture.Resolver(new ThrowingCatalog()), new FakePromptSource(), Options.Create(new TeamOptions()));
        WorkItem item = new(
            "room-1",
            "Room 1",
            "Human",
            "/compact [Growth](/teams/Growth)",
            [],
            WorkItemKind.Command,
            TriggerMessageId: "trig-1",
            Command: new AdapterCommandCall("compact", "[Growth](/teams/Growth)"));
        RecordingLogger logger = new();

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, expander, logger: logger);

        Assert.Equal("/compact [Growth](/teams/Growth)", prompt.Text);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("expand references", StringComparison.Ordinal));
    }

    /// <summary>A Greeting never looks its text up: with a catalog that throws, the greeting prompt runs and no expansion is logged (spec section 9.1, traps.md).</summary>
    [Fact]
    public async Task Greeting_ReferenceIsNotLookedUp()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(TestContext.Current.CancellationToken);
        ReferenceExpander expander = new(fixture.Resolver(new ThrowingCatalog()), new FakePromptSource(), Options.Create(new TeamOptions()));
        WorkItem item = new("room-1", "Room 1", string.Empty, "see [Growth](/teams/Growth)", [], WorkItemKind.Greeting);
        RecordingLogger logger = new();

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, expander, logger: logger);

        Assert.Equal(RoomSession.BuildPrompt(item, new FakePromptSource()), prompt.Text);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("expand references", StringComparison.Ordinal));
    }

    /// <summary>A Greeting carries no header.</summary>
    [Fact]
    public async Task Greeting_NoHeader()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(
            TestContext.Current.CancellationToken, null, BuildPromptReferencesTests.TaskFile("PLAT-0001", "Fix login"));
        WorkItem item = new("room-1", "Room 1", string.Empty, "PLAT-0001", [], WorkItemKind.Greeting);

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, BuildPromptReferencesTests.ExpanderOver(fixture));

        Assert.Equal(RoomSession.BuildPrompt(item, new FakePromptSource()), prompt.Text);
        Assert.DoesNotContain("Fix login", prompt.Text, StringComparison.Ordinal);
    }

    /// <summary>With <c>ExpandInPrompt</c> off the prompt is byte-for-byte the one without an expander.</summary>
    [Fact]
    public async Task ExpandInPromptFalse_ByteIdentical()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(
            TestContext.Current.CancellationToken, null, BuildPromptReferencesTests.TaskFile("PLAT-0001", "Fix login"));
        WorkItem item = BuildPromptReferencesTests.MessageWith("look at PLAT-0001");

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(
            item, BuildPromptReferencesTests.ExpanderOver(fixture, options => options.References.ExpandInPrompt = false));

        Assert.Equal(RoomSession.BuildPrompt(item, new FakePromptSource()), prompt.Text);
    }

    /// <summary>A Message with no reference gets today's prompt.</summary>
    [Fact]
    public async Task NoReferences_ByteIdentical()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(TestContext.Current.CancellationToken);
        WorkItem item = BuildPromptReferencesTests.MessageWith("hello there", [new CaughtUpMessage("Luna", "hi")]);

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, BuildPromptReferencesTests.ExpanderOver(fixture));

        Assert.Equal(RoomSession.BuildPrompt(item, new FakePromptSource()), prompt.Text);
    }

    /// <summary>An Adapter whose tools carry the prefix is told the prefixed <c>list_agents</c> name.</summary>
    [Fact]
    public async Task PrefixedToolNames()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(
            TestContext.Current.CancellationToken, null, BuildPromptReferencesTests.TaskFile("PLAT-0001", "Fix login"));

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(
            BuildPromptReferencesTests.MessageWith("PLAT-0001"), BuildPromptReferencesTests.ExpanderOver(fixture), usesPrefix: true);

        Assert.Equal("To learn more, list Teammates and their Teams with mcp__team__list_agents. You cannot read the Messages of a Room you are not a Member of.", BuildPromptReferencesTests.HintLine(prompt.Text)[..138]);
        Assert.Contains("with mcp__team__get_task", prompt.Text, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>An Adapter without the prefix is told the bare tool names.</summary>
    [Fact]
    public async Task UnprefixedToolNames()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(
            TestContext.Current.CancellationToken, null, BuildPromptReferencesTests.TaskFile("PLAT-0001", "Fix login"));

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(
            BuildPromptReferencesTests.MessageWith("PLAT-0001"), BuildPromptReferencesTests.ExpanderOver(fixture), usesPrefix: false);

        Assert.Equal("To learn more, list Teammates and their Teams with list_agents. You cannot read the Messages of a Room you are not a Member of.", BuildPromptReferencesTests.HintLine(prompt.Text)[..127]);
        Assert.DoesNotContain("mcp__team__", prompt.Text, StringComparison.Ordinal);
    }

    /// <summary>A lookup that throws is logged and the Turn still runs with today's prompt.</summary>
    [Fact]
    public async Task ExpanderThrows_TurnStillRuns()
    {
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(TestContext.Current.CancellationToken);
        ReferenceResolver resolver = fixture.Resolver(new ThrowingCatalog());
        ReferenceExpander expander = new(resolver, new FakePromptSource(), Options.Create(new TeamOptions()));
        WorkItem item = BuildPromptReferencesTests.MessageWith("see [Growth](/teams/Growth)");

        AgentPrompt prompt = await BuildPromptReferencesTests.RunTurnAsync(item, expander);

        Assert.Equal(RoomSession.BuildPrompt(item, new FakePromptSource()), prompt.Text);
    }

    /// <summary>A logger that keeps the text of every message, so a test can see that a failure was logged.</summary>
    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly Lock gate = new();
        private readonly List<string> messages = [];

        /// <summary>Every message logged so far, in order.</summary>
        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (this.gate)
                {
                    return [.. this.messages];
                }
            }
        }

        /// <summary>Scopes are not used, so none is returned.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Every level is enabled, so every message is kept.</summary>
        /// <param name="logLevel">The level.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        /// <summary>Keeps the formatted message.</summary>
        /// <typeparam name="TState">The message state type.</typeparam>
        /// <param name="logLevel">The level.</param>
        /// <param name="eventId">The event id.</param>
        /// <param name="state">The message state.</param>
        /// <param name="exception">The exception, if any.</param>
        /// <param name="formatter">Formats the state into text.</param>
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (this.gate)
            {
                this.messages.Add(formatter(state, exception));
            }
        }
    }

    private sealed class ThrowingCatalog : ITeamCatalog
    {
        public IReadOnlyList<TeamSummary> Teams => throw new InvalidOperationException("boom");

        public event Action? Changed
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }

        public TeamSummary? Find(string team) => throw new InvalidOperationException("boom");

        public bool ProjectExists(string team, string project) => throw new InvalidOperationException("boom");
    }
}

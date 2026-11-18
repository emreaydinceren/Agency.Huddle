using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Acp.Sessions.TurnHooks;

/// <summary>
/// Pins the happy path of <see cref="TurnHookPipeline"/> (Spec §6.10): hooks run in list order, a hook whose
/// <see cref="ITurnHook.AppliesTo"/> is false is skipped, a null text adds no block but still carries to the
/// After point, a null addition skips the After point, and the After point's return value becomes the state.
/// </summary>
public sealed class TurnHookPipelineTests
{
    private const string RoomId = "room-1";

    private static readonly TurnHookScope Scope = new("nova", true, true, []);

    /// <summary>Hooks handed over in reverse run in production order: AGENTS.md, then Huddle-wide Memory, then File Changes.</summary>
    [Fact]
    public void ForPersona_HooksInReverse_RunInProductionOrder()
    {
        ITurnHook[] reversed = [new NamedHook("FileChanges"), new NamedHook("HuddleMemory"), new NamedHook("AgentsMd")];

        TurnHookPipeline pipeline = TurnHookPipeline.ForPersona(Scope, reversed, NullLogger.Instance);

        Assert.Equal(["AgentsMd", "HuddleMemory", "FileChanges"], pipeline.HookNames);
    }

    /// <summary>The production order is TeamRenamed, AgentsMd, Playbooks, HuddleMemory, FileChanges: Playbooks rank below AGENTS.md and above Memory (Team Playbooks spec §6.4).</summary>
    [Fact]
    public void ProductionOrder_PlaybooksFollowAgentsMd()
    {
        Assert.Equal(["TeamRenamed", "AgentsMd", "Playbooks", "HuddleMemory", "FileChanges"], TurnHookPipeline.ProductionOrder);
    }

    /// <summary>Every production hook handed over in reverse runs in the production order, Playbooks between AgentsMd and HuddleMemory.</summary>
    [Fact]
    public void ForPersona_AllHooksInReverse_RunInProductionOrderWithPlaybooks()
    {
        ITurnHook[] reversed = [new NamedHook("FileChanges"), new NamedHook("HuddleMemory"), new NamedHook("Playbooks"), new NamedHook("AgentsMd"), new NamedHook("TeamRenamed")];

        TurnHookPipeline pipeline = TurnHookPipeline.ForPersona(Scope, reversed, NullLogger.Instance);

        Assert.Equal(["TeamRenamed", "AgentsMd", "Playbooks", "HuddleMemory", "FileChanges"], pipeline.HookNames);
    }

    /// <summary>A hook whose name is not in the production order is refused, and the message names it.</summary>
    [Fact]
    public void ForPersona_UnlistedHook_ThrowsNamingIt()
    {
        ITurnHook[] hooks = [new NamedHook("AgentsMd"), new NamedHook("Unlisted")];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => TurnHookPipeline.ForPersona(Scope, hooks, NullLogger.Instance));

        Assert.Equal(
            "Turn hook 'Unlisted' is not in the production order; add its name to TurnHookPipeline.ProductionOrder.",
            error.Message);
    }

    /// <summary>A listed hook that is not registered is skipped, and the rest keep their order.</summary>
    [Fact]
    public void ForPersona_ListedHookAbsent_IsSkipped()
    {
        ITurnHook[] hooks = [new NamedHook("FileChanges"), new NamedHook("AgentsMd")];

        TurnHookPipeline pipeline = TurnHookPipeline.ForPersona(Scope, hooks, NullLogger.Instance);

        Assert.Equal(["AgentsMd", "FileChanges"], pipeline.HookNames);
    }

    /// <summary>A hook that only has a name, to pin the order the pipeline sorts hooks into.</summary>
    private sealed class NamedHook(string name) : ITurnHook
    {
        public string Name => name;

        public bool AppliesTo(TurnHookScope scope) => true;

        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken) => Task.FromResult<TurnAddition?>(null);

        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    /// <summary>Two applicable hooks run Before in list order and their blocks come back in that order.</summary>
    [Fact]
    public async Task BeforeTurn_RunsHooksInListOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> log = [];
        RecordingHook first = new("first", log) { BeforeResult = new TurnAddition("first-text", "first-carry") };
        RecordingHook second = new("second", log) { BeforeResult = new TurnAddition("second-text", "second-carry") };
        TurnHookPipeline pipeline = new([first, second], Scope, NullLogger.Instance);

        TurnHookBeforeResult result = await pipeline.BeforeTurnAsync(
            RoomId, new Dictionary<string, string?>(), ct, ct);

        Assert.Equal(["Before:first", "Before:second"], log);
        Assert.Equal(["first-text", "second-text"], result.Blocks);
        Assert.Equal(["first", "second"], result.Additions.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>A hook whose AppliesTo is false never runs, and leaves no block and no addition.</summary>
    [Fact]
    public async Task BeforeTurn_AppliesToFalse_SkipsTheHook()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> log = [];
        RecordingHook applies = new("applies", log) { BeforeResult = new TurnAddition("applies-text", null) };
        RecordingHook skipped = new("skipped", log)
        {
            Applies = false,
            BeforeResult = new TurnAddition("skipped-text", null),
        };
        TurnHookPipeline pipeline = new([skipped, applies], Scope, NullLogger.Instance);

        TurnHookBeforeResult result = await pipeline.BeforeTurnAsync(
            RoomId, new Dictionary<string, string?>(), ct, ct);

        Assert.Equal(["Before:applies"], log);
        Assert.Equal(["applies-text"], result.Blocks);
        Assert.Equal(["applies"], result.Additions.Keys);
    }

    /// <summary>A null text adds no block, yet its carry is kept and reaches the hook's After point.</summary>
    [Fact]
    public async Task BeforeTurn_NullTextAddsNoBlock_AndAfterStillGetsTheCarry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> log = [];
        RecordingHook hook = new("quiet", log) { BeforeResult = new TurnAddition(null, "the-carry") };
        TurnHookPipeline pipeline = new([hook], Scope, NullLogger.Instance);
        Dictionary<string, string?> state = [];

        TurnHookBeforeResult before = await pipeline.BeforeTurnAsync(RoomId, state, ct, ct);
        await pipeline.AfterTurnAsync(RoomId, state, before, [], ct);

        Assert.Empty(before.Blocks);
        Assert.Equal("the-carry", before.Additions["quiet"].Carry);
        Assert.Equal(["Before:quiet", "After:quiet"], log);
        Assert.Equal("the-carry", hook.ReceivedAddition?.Carry);
    }

    /// <summary>A hook that returns a null addition has no entry, so its After point is not called.</summary>
    [Fact]
    public async Task BeforeTurn_NullAddition_AfterIsNotCalled()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> log = [];
        RecordingHook hook = new("idle", log) { BeforeResult = null };
        TurnHookPipeline pipeline = new([hook], Scope, NullLogger.Instance);
        Dictionary<string, string?> state = [];

        TurnHookBeforeResult before = await pipeline.BeforeTurnAsync(RoomId, state, ct, ct);
        await pipeline.AfterTurnAsync(RoomId, state, before, [], ct);

        Assert.Empty(before.Blocks);
        Assert.Empty(before.Additions);
        Assert.Equal(["Before:idle"], log);
    }

    /// <summary>The state string an After point returns replaces that hook's entry in the session state.</summary>
    [Fact]
    public async Task AfterTurn_ReturnedStateUpdatesTheSessionState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<string> log = [];
        RecordingHook hook = new("keeper", log)
        {
            BeforeResult = new TurnAddition("text", "carry"),
            AfterResult = "new-state",
        };
        TurnHookPipeline pipeline = new([hook], Scope, NullLogger.Instance);
        Dictionary<string, string?> state = new(StringComparer.Ordinal) { ["keeper"] = "old-state" };

        TurnHookBeforeResult before = await pipeline.BeforeTurnAsync(RoomId, state, ct, ct);
        await pipeline.AfterTurnAsync(RoomId, state, before, ["a.md"], ct);

        Assert.Equal("old-state", hook.ReceivedStarting?.State);
        Assert.Equal("old-state", hook.ReceivedEnded?.State);
        Assert.Equal(["a.md"], hook.ReceivedEnded?.Touched);
        Assert.Equal("new-state", state["keeper"]);
    }

    /// <summary>A hook that records what it was given and returns what the test sets.</summary>
    private sealed class RecordingHook(string name, List<string> log) : ITurnHook
    {
        /// <summary>The value <see cref="AppliesTo"/> returns.</summary>
        public bool Applies { get; init; } = true;

        /// <summary>What Before returns.</summary>
        public TurnAddition? BeforeResult { get; init; }

        /// <summary>What After returns as the new state.</summary>
        public string? AfterResult { get; init; }

        /// <summary>The Before input last seen.</summary>
        public TurnStarting? ReceivedStarting { get; private set; }

        /// <summary>The After input last seen.</summary>
        public TurnEnded? ReceivedEnded { get; private set; }

        /// <summary>The addition After last received.</summary>
        public TurnAddition? ReceivedAddition { get; private set; }

        /// <inheritdoc />
        public string Name => name;

        /// <inheritdoc />
        public bool AppliesTo(TurnHookScope scope) => this.Applies;

        /// <inheritdoc />
        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken) =>
            Task.FromResult(opening.State);

        /// <inheritdoc />
        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken)
        {
            log.Add("Before:" + name);
            this.ReceivedStarting = turn;
            return Task.FromResult(this.BeforeResult);
        }

        /// <inheritdoc />
        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken)
        {
            log.Add("After:" + name);
            this.ReceivedEnded = turn;
            this.ReceivedAddition = addition;
            return Task.FromResult(this.AfterResult);
        }
    }
}

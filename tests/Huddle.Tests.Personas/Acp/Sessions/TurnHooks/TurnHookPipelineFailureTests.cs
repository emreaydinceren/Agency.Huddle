using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Acp.Sessions.TurnHooks;

/// <summary>
/// Pins the failure and logging rows of Spec §6.10 "What <c>TurnHookPipeline</c> guarantees": a hook that throws
/// is logged by name and skipped with its state kept, a cancelled run token propagates, a cancelled Stop token
/// ends the Before hooks quietly, there is one Debug line per hook per Turn, and a long block warns but is not cut.
/// </summary>
public sealed class TurnHookPipelineFailureTests
{
    private const string RoomId = "room-1";

    private static readonly TurnHookScope Scope = new("nova", true, true, []);

    /// <summary>A hook that throws in Before is logged by its name and the next hook still runs.</summary>
    [Fact]
    public async Task BeforeTurn_HookThrows_LogsItsNameAndRunsTheNext()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<TurnHookPipeline> logger = new();
        ScriptedHook bad = new("bad") { Before = static (_, _) => throw new InvalidOperationException("boom") };
        ScriptedHook good = new("good") { Before = static (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition("good-text", null)) };
        TurnHookPipeline pipeline = new([bad, good], Scope, logger);

        TurnHookBeforeResult result = await pipeline.BeforeTurnAsync(RoomId, new Dictionary<string, string?>(), ct, ct);

        Assert.Equal(["good-text"], result.Blocks);
        // contains-ok: the hook's name is the identifier the log line must carry.
        Assert.Single(logger.Entries, entry => entry.Level >= LogLevel.Warning && entry.Message.Contains("bad", StringComparison.Ordinal));
    }

    /// <summary>A hook that throws in Before leaves no addition behind and its stored state untouched.</summary>
    [Fact]
    public async Task BeforeTurn_HookThrows_KeepsItsState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ScriptedHook bad = new("bad") { Before = static (_, _) => throw new InvalidOperationException("boom") };
        TurnHookPipeline pipeline = new([bad], Scope, new RecordingLogger<TurnHookPipeline>());
        Dictionary<string, string?> state = new(StringComparer.Ordinal) { ["bad"] = "kept" };

        TurnHookBeforeResult result = await pipeline.BeforeTurnAsync(RoomId, state, ct, ct);
        await pipeline.AfterTurnAsync(RoomId, state, result, [], ct);

        Assert.Empty(result.Additions);
        Assert.Equal("kept", state["bad"]);
    }

    /// <summary>A hook that throws in After keeps its old state, and a later hook still stores its new one.</summary>
    [Fact]
    public async Task AfterTurn_HookThrows_KeepsItsState()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ScriptedHook bad = new("bad")
        {
            Before = static (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition(null, null)),
            After = static (_, _, _) => throw new InvalidOperationException("boom"),
        };
        ScriptedHook good = new("good")
        {
            Before = static (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition(null, null)),
            After = static (_, _, _) => Task.FromResult<string?>("good-new"),
        };
        TurnHookPipeline pipeline = new([bad, good], Scope, new RecordingLogger<TurnHookPipeline>());
        Dictionary<string, string?> state = new(StringComparer.Ordinal) { ["bad"] = "kept", ["good"] = "good-old" };

        TurnHookBeforeResult before = await pipeline.BeforeTurnAsync(RoomId, state, ct, ct);
        await pipeline.AfterTurnAsync(RoomId, state, before, [], ct);

        Assert.Equal("kept", state["bad"]);
        Assert.Equal("good-new", state["good"]);
    }

    /// <summary>A cancelled run token is not swallowed as a hook failure: shutdown must end the loop.</summary>
    [Fact]
    public async Task BeforeTurn_RunTokenCancelled_Propagates()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource run = new();
        await run.CancelAsync();
        ScriptedHook hook = new("slow")
        {
            Before = static (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<TurnAddition?>(null);
            },
        };
        TurnHookPipeline pipeline = new([hook], Scope, new RecordingLogger<TurnHookPipeline>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pipeline.BeforeTurnAsync(RoomId, new Dictionary<string, string?>(), run.Token, ct));
    }

    /// <summary>A Stop that cancels mid-hook ends the Before hooks without throwing, and no later hook runs.</summary>
    [Fact]
    public async Task BeforeTurn_StopTokenCancelled_EndsWithoutThrowing_AndRunsNoMoreHooks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using CancellationTokenSource stop = new();
        List<string> ran = [];
        ScriptedHook first = new("first")
        {
            Before = (_, _) =>
            {
                ran.Add("first");
                stop.Cancel();
                throw new OperationCanceledException(stop.Token);
            },
        };
        ScriptedHook second = new("second")
        {
            Before = (_, _) =>
            {
                ran.Add("second");
                return Task.FromResult<TurnAddition?>(new TurnAddition("second-text", null));
            },
        };
        TurnHookPipeline pipeline = new([first, second], Scope, new RecordingLogger<TurnHookPipeline>());

        TurnHookBeforeResult result = await pipeline.BeforeTurnAsync(RoomId, new Dictionary<string, string?>(), ct, stop.Token);

        Assert.Equal(["first"], ran);
        Assert.Empty(result.Blocks);
    }

    /// <summary>Each hook logs one Debug line per Turn carrying its name and the characters it added.</summary>
    [Fact]
    public async Task BeforeTurn_LogsOneDebugLinePerHook_WithNameTimeAndChars()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<TurnHookPipeline> logger = new();
        ScriptedHook first = new("first") { Before = static (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition("1234567", null)) };
        ScriptedHook second = new("second") { Before = static (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition(new string('x', 13), null)) };
        TurnHookPipeline pipeline = new([first, second], Scope, logger);

        _ = await pipeline.BeforeTurnAsync(RoomId, new Dictionary<string, string?>(), ct, ct);

        IReadOnlyList<LogEntry> lines = logger.AtExactly(LogLevel.Debug);
        Assert.Equal(2, lines.Count);
        // contains-ok: the name and the character count are the two values the line must carry.
        Assert.Contains("first", lines[0].Message, StringComparison.Ordinal);
        Assert.Contains('7', lines[0].Message);
        // contains-ok: the second line must carry its hook name too.
        Assert.Contains("second", lines[1].Message, StringComparison.Ordinal);
        // contains-ok: the second line must carry its character count.
        Assert.Contains("13", lines[1].Message, StringComparison.Ordinal);
    }

    /// <summary>A block over 10,000 characters warns by hook name and comes back whole; one of exactly 10,000 does not warn.</summary>
    [Fact]
    public async Task BeforeTurn_BlockOver10000Chars_Warns_AndDoesNotCut()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<TurnHookPipeline> logger = new();
        string longText = new('y', 10_001);
        string edgeText = new('z', 10_000);
        ScriptedHook huge = new("huge") { Before = (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition(longText, null)) };
        ScriptedHook edge = new("edge") { Before = (_, _) => Task.FromResult<TurnAddition?>(new TurnAddition(edgeText, null)) };
        TurnHookPipeline pipeline = new([huge, edge], Scope, logger);

        TurnHookBeforeResult result = await pipeline.BeforeTurnAsync(RoomId, new Dictionary<string, string?>(), ct, ct);

        Assert.Equal([longText, edgeText], result.Blocks);
        IReadOnlyList<LogEntry> warnings = logger.AtExactly(LogLevel.Warning);
        // contains-ok: the warning must name the hook that added the long block, and only that one.
        Assert.Single(warnings);
        // contains-ok: the warning must name the hook that added the long block.
        Assert.Contains("huge", warnings[0].Message, StringComparison.Ordinal);
    }

    /// <summary>A hook whose points are the delegates a test sets; each defaults to doing nothing.</summary>
    private sealed class ScriptedHook(string name) : ITurnHook
    {
        /// <summary>Gets the Before behaviour.</summary>
        public Func<TurnStarting, CancellationToken, Task<TurnAddition?>> Before { get; init; } =
            static (_, _) => Task.FromResult<TurnAddition?>(null);

        /// <summary>Gets the After behaviour.</summary>
        public Func<TurnEnded, TurnAddition?, CancellationToken, Task<string?>> After { get; init; } =
            static (turn, _, _) => Task.FromResult(turn.State);

        /// <inheritdoc />
        public string Name => name;

        /// <inheritdoc />
        public bool AppliesTo(TurnHookScope scope) => true;

        /// <inheritdoc />
        public Task<string?> OnSessionOpeningAsync(SessionOpening opening, CancellationToken cancellationToken) =>
            Task.FromResult(opening.State);

        /// <inheritdoc />
        public Task<TurnAddition?> BeforeTurnAsync(TurnStarting turn, CancellationToken cancellationToken) =>
            this.Before(turn, cancellationToken);

        /// <inheritdoc />
        public Task<string?> AfterTurnAsync(TurnEnded turn, TurnAddition? addition, CancellationToken cancellationToken) =>
            this.After(turn, addition, cancellationToken);
    }
}

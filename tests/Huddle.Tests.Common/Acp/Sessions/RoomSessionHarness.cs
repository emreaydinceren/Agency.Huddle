using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Sessions.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// The knobs a test may turn when building a <see cref="RoomSession"/> through <see cref="RoomSessionHarness"/>.
/// Every member defaults to what the older per-class <c>CreateSession</c> helpers passed.
/// </summary>
internal sealed record RoomSessionHarnessOptions
{
    /// <summary>The Room the session serves. <c>room-1</c> by default; <see langword="null"/> for a session with no Room (as <c>RoomSessionTests</c> uses).</summary>
    public string? RoomId { get; init; } = "room-1";

    /// <summary>The Persona the session runs for, or <see langword="null"/> for none.</summary>
    public Persona? Persona { get; init; }

    /// <summary>The scheduler that admits the session; <see langword="null"/> uses <see cref="ImmediateTurnScheduler"/>.</summary>
    public ITurnScheduler? Scheduler { get; init; }

    /// <summary>The ACP options; <see langword="null"/> uses a default <see cref="AcpOptions"/>.</summary>
    public AcpOptions? Acp { get; init; }

    /// <summary>The agent id the session reports, or <see langword="null"/> for none.</summary>
    public string? AgentId { get; init; }

    /// <summary>The Turn activity tracker, or <see langword="null"/> for none.</summary>
    public TurnActivity? TurnActivity { get; init; }

    /// <summary>The context-fill store, or <see langword="null"/> for none.</summary>
    public ContextFill? ContextFill { get; init; }

    /// <summary>The library document collector, or <see langword="null"/> for none.</summary>
    public LibraryDocumentCollector? LibraryDocs { get; init; }

    /// <summary>Whether the agent may read files. <see langword="true"/> unless a test says otherwise.</summary>
    public bool ReadsFiles { get; init; } = true;

    /// <summary>The Session settings the first Turn's catch-up reads, or <see langword="null"/> for none (the <see cref="AcpOptions"/> values apply).</summary>
    public ISessionSettings? SessionSettings { get; init; }

    /// <summary>The store of resumable session entries, or <see langword="null"/> for none (the session then never stores or resumes).</summary>
    public RoomSessionStore? RoomSessions { get; init; }

    /// <summary>The Persona host the session consults for resume, or <see langword="null"/> for none.</summary>
    public IPersonaHost? Host { get; init; }

    /// <summary>The store the first Turn after a fresh start writes its whole prompt text to, or <see langword="null"/> for none (no file is written).</summary>
    public LastCatchUpStore? LastCatchUp { get; init; }

    /// <summary>The Turn hook pipeline the session runs around each Message Turn, or <see langword="null"/> for none.</summary>
    public TurnHookPipeline? Hooks { get; init; }

    /// <summary>The clock the session and its watchdog use, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? Time { get; init; }
}

/// <summary>
/// Owns a <see cref="RoomSession"/> and the run token its loops watch, and tears both down in the only
/// order that ends: cancel the token, dispose the session, dispose the token source. Replaces the
/// per-class <c>CreateSession</c>, <c>FixedOpen</c> and <c>DisposeSessionAsync</c> triplets, so a test
/// writes <c>await using</c> instead of a <c>try</c>/<c>finally</c>.
/// </summary>
internal sealed class RoomSessionHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource runCts;
    private bool disposed;

    private RoomSessionHarness(RoomSession room, CancellationTokenSource runCts)
    {
        this.Room = room;
        this.runCts = runCts;
        this.RunToken = runCts.Token;
    }

    /// <summary>The session under test.</summary>
    public RoomSession Room { get; }

    /// <summary>The run token the session's loops watch; cancelled first when the harness is disposed.</summary>
    public CancellationToken RunToken { get; }

    /// <summary>Builds a session with fakes for every collaborator and its own run token source.</summary>
    /// <param name="owner">The fake owner the session reports to.</param>
    /// <param name="open">Opens the agent session.</param>
    /// <param name="options">Optional overrides; <see langword="null"/> for the defaults.</param>
    /// <returns>The harness, to be disposed with <c>await using</c>.</returns>
    public static RoomSessionHarness Create(
        FakeRoomSessionOwner owner,
        Func<CancellationToken, Task<IAgentSession>> open,
        RoomSessionHarnessOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(open);

        RoomSessionHarnessOptions settings = options ?? new RoomSessionHarnessOptions();
        CancellationTokenSource runCts = new();
        RoomSession session = new(
            roomId: settings.RoomId,
            open: open,
            owner: owner,
            scheduler: settings.Scheduler ?? new ImmediateTurnScheduler(),
            prompts: new FakePromptSource(),
            options: settings.Acp ?? new AcpOptions(),
            fileChanges: null,
            declaredWatches: [],
            logger: NullLogger.Instance,
            runToken: runCts.Token,
            persona: settings.Persona,
            agentId: settings.AgentId,
            turnActivity: settings.TurnActivity,
            contextFill: settings.ContextFill,
            libraryDocs: settings.LibraryDocs,
            readsFiles: settings.ReadsFiles,
            roomSessions: settings.RoomSessions,
            host: settings.Host,
            sessionSettings: settings.SessionSettings,
            lastCatchUp: settings.LastCatchUp,
            time: settings.Time,
            hooks: settings.Hooks);
        return new RoomSessionHarness(session, runCts);
    }

    /// <summary>Takes over a session a test built by hand, so it is torn down in the same order.</summary>
    /// <param name="session">The session to own.</param>
    /// <param name="runCts">The token source whose token the session was built with.</param>
    /// <returns>The harness, to be disposed with <c>await using</c>.</returns>
    public static RoomSessionHarness Adopt(RoomSession session, CancellationTokenSource runCts)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(runCts);

        return new RoomSessionHarness(session, runCts);
    }

    /// <summary>An open delegate that always returns the same session.</summary>
    /// <param name="session">The session every call returns.</param>
    /// <returns>The delegate.</returns>
    public static Func<CancellationToken, Task<IAgentSession>> FixedOpen(IAgentSession session) =>
        _ => Task.FromResult(session);

    /// <summary>Cancels the run token, disposes the session, then disposes the token source. A second call does nothing.</summary>
    /// <returns>A task that completes when teardown has finished.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        await this.runCts.CancelAsync();
        await this.Room.DisposeAsync();
        this.runCts.Dispose();
    }
}

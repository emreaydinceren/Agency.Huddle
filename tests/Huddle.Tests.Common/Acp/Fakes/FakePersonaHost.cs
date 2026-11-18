using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A test double for <see cref="IPersonaHost"/> (RS §6.3). Its first <see cref="OpenAsync"/> call
/// returns the shared session <see cref="FakeAgentHostFactory"/> was constructed with, matching
/// every existing test's <c>factory.Session</c> - every later open returns a fresh
/// <see cref="FakeAgentSession"/>, recorded in <see cref="Sessions"/> in open order.
/// <see cref="ResumeAsync"/> is scripted per test through <see cref="ResumeHandler"/>.
/// </summary>
internal sealed class FakePersonaHost : IPersonaHost
{
    private readonly Lock gate = new();
    private readonly FakeAgentSession sharedSession;
    private readonly List<FakeAgentSession> sessions = [];
    private readonly List<string> resumeCalls = [];
    private readonly List<CleanSessionSpec> cleanSpecs = [];
    private readonly List<FakeAgentSession> cleanSessions = [];
    private bool firstOpenDone;
    private Exception? pendingOpenFailure;
    private int liveSessionCount;
    private int liveSessionHighWaterMark;

    /// <summary>Initializes a new instance of the <see cref="FakePersonaHost"/> class.</summary>
    /// <param name="sharedSession">The session <see cref="OpenAsync"/> returns the first time it is called.</param>
    /// <param name="profile">The <see cref="Profile"/> this host reports.</param>
    public FakePersonaHost(FakeAgentSession sharedSession, AdapterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(sharedSession);
        ArgumentNullException.ThrowIfNull(profile);

        this.sharedSession = sharedSession;
        this.Profile = profile;
    }

    /// <summary>The Adapter Profile this host reports (finding P-8). Settable so a test can script a different one.</summary>
    public AdapterProfile Profile { get; set; }

    /// <summary>Whether <see cref="ResumeAsync"/> is expected to find anything. Default <see langword="false"/>, matching a fresh Adapter with no resume capability.</summary>
    public bool CanResume { get; set; }

    /// <summary>What this host says its Adapter advertised a prompt may carry. Default <see cref="AgentPromptCapabilities.None"/>, like an Adapter that said nothing.</summary>
    public AgentPromptCapabilities PromptCapabilities { get; set; } = AgentPromptCapabilities.None;

    /// <summary>Every session <see cref="OpenAsync"/> has returned, in open order — the shared one first, then any fresh ones.</summary>
    public IReadOnlyList<FakeAgentSession> Sessions
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.sessions];
            }
        }
    }

    /// <summary>Every session id <see cref="ResumeAsync"/> was asked to resume, in call order.</summary>
    public IReadOnlyList<string> ResumeCalls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.resumeCalls];
            }
        }
    }

    /// <summary>Scripts what <see cref="ResumeAsync"/> returns for a given session id. Defaults to always returning <see langword="null"/> (not found).</summary>
    public Func<string, FakeAgentSession?> ResumeHandler { get; set; } = static _ => null;

    /// <summary>
    /// Runs, if set, just before <see cref="OpenAsync"/> returns its session - so a test can script
    /// that session (queue a reply, enqueue a fault) exactly when it is opened, rather than before
    /// the runner ever calls <see cref="OpenAsync"/>.
    /// </summary>
    public Action<FakeAgentSession>? OnOpen { get; set; }

    /// <summary>How long <see cref="OpenAsync"/> waits, with the caller's token, before returning. Zero by default.</summary>
    public TimeSpan OpenDelay { get; set; }

    /// <summary>
    /// When set, <see cref="OpenAsync"/> awaits this before returning its session - lets a test hold
    /// an open in flight (RS §9 E-4: "Stop while a session is Opening") and release it deliberately,
    /// rather than racing a fixed delay against the Stop it means to land mid-open.
    /// </summary>
    public TaskCompletionSource? OpenGate { get; set; }

    /// <summary>Whether <see cref="DisposeAsync"/> has been called.</summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Runs, if set, inside <see cref="DisposeAsync"/>, before it returns - lets a test simulate a
    /// runner's own final Turn-end write (for example a late <c>RoomSessionStore.Put</c>) landing
    /// concurrently with disposal, so a store-forgetting caller can be proven to forget AFTER
    /// disposal rather than racing it (D29 correction 16, RS §6.13 U7).
    /// </summary>
    public Action? OnDispose { get; set; }

    /// <summary>
    /// How long <see cref="DisposeAsync"/> waits, asynchronously, before it completes - stands in for
    /// the stdin grace a real Adapter host allows before it kills its process, so a test can prove
    /// several hosts are stopped together rather than one after another.
    /// </summary>
    public TimeSpan DisposeDelay { get; set; }

    /// <summary>
    /// When set, <see cref="DisposeAsync"/> awaits this after <see cref="OnDispose"/> ran and before it
    /// completes - lets a test hold every host's disposal open until all of them have <em>started</em>,
    /// which only a concurrent stop can reach, so concurrency is proved by an event rather than by a
    /// stopwatch that CPU load can stretch.
    /// </summary>
    public Task? DisposeGate { get; set; }

    /// <summary>
    /// Thrown by <see cref="DisposeAsync"/> after the delay, once <see cref="Disposed"/> is set - lets a
    /// test prove one host failing to stop never prevents the others from being stopped.
    /// </summary>
    public Exception? DisposeFailure { get; set; }

    /// <summary>
    /// The most sessions this host has ever had open (returned from <see cref="OpenAsync"/> but not
    /// yet disposed) at once - lets a test prove <c>RoomSessionPool</c>'s live cap (RS §6.14
    /// <c>MaxLiveSessions</c>) is actually honoured, not merely that the right sessions were
    /// eventually closed.
    /// </summary>
    public int LiveSessionHighWaterMark => Volatile.Read(ref this.liveSessionHighWaterMark);

    /// <summary>Configures the next <see cref="OpenAsync"/> call to fail with <paramref name="exception"/> instead of returning a session.</summary>
    /// <param name="exception">The exception <see cref="OpenAsync"/> throws once.</param>
    public void FailNextOpenWith(Exception exception)
    {
        lock (this.gate)
        {
            this.pendingOpenFailure = exception;
        }
    }

    /// <summary>Every <see cref="CleanSessionSpec"/> <see cref="OpenCleanAsync"/> was called with, in call order.</summary>
    public IReadOnlyList<CleanSessionSpec> CleanSpecs
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.cleanSpecs];
            }
        }
    }

    /// <summary>Every session <see cref="OpenCleanAsync"/> has returned, in open order. Each one is also in <see cref="Sessions"/>.</summary>
    public IReadOnlyList<FakeAgentSession> CleanSessions
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.cleanSessions];
            }
        }
    }

    public async Task<IAgentSession> OpenAsync(CancellationToken cancellationToken)
    {
        return await this.OpenCoreAsync(null, cancellationToken);
    }

    /// <summary>Opens a clean session the way <see cref="OpenAsync"/> does (delay, gate, failure injection, live-count bookkeeping), but always a fresh one, never the shared session.</summary>
    /// <param name="spec">What the session is opened for; recorded in <see cref="CleanSpecs"/>.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    public async Task<IAgentSession> OpenCleanAsync(CleanSessionSpec spec, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return await this.OpenCoreAsync(spec, cancellationToken);
    }

    /// <summary>The one open path: <paramref name="clean"/> is <see langword="null"/> for an ordinary open.</summary>
    /// <param name="clean">The clean spec, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    private async Task<IAgentSession> OpenCoreAsync(CleanSessionSpec? clean, CancellationToken cancellationToken)
    {
        if (this.OpenDelay > TimeSpan.Zero)
        {
            await Task.Delay(this.OpenDelay, cancellationToken).ConfigureAwait(false);
        }

        if (this.OpenGate is { } openGate)
        {
            await openGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Exception? failure;
        lock (this.gate)
        {
            failure = this.pendingOpenFailure;
            this.pendingOpenFailure = null;
        }

        if (failure is not null)
        {
            throw failure;
        }

        FakeAgentSession session;
        lock (this.gate)
        {
            if (clean is not null || this.firstOpenDone)
            {
                session = new FakeAgentSession();
            }
            else
            {
                this.firstOpenDone = true;
                session = this.sharedSession;
            }

            this.sessions.Add(session);
            if (clean is not null)
            {
                this.cleanSpecs.Add(clean);
                this.cleanSessions.Add(session);
            }
        }

        session.OnDisposed = () => Interlocked.Decrement(ref this.liveSessionCount);
        FakePersonaHost.RaiseToMax(ref this.liveSessionHighWaterMark, Interlocked.Increment(ref this.liveSessionCount));

        this.OnOpen?.Invoke(session);
        return session;
    }

    /// <summary>Atomically raises <paramref name="highWaterMark"/> to <paramref name="current"/> when it is higher.</summary>
    private static void RaiseToMax(ref int highWaterMark, int current)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref highWaterMark);
            if (current <= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref highWaterMark, current, observed) != observed);
    }

    public Task<IAgentSession?> ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (this.gate)
        {
            this.resumeCalls.Add(sessionId);
        }

        return Task.FromResult<IAgentSession?>(this.ResumeHandler(sessionId));
    }

    public async ValueTask DisposeAsync()
    {
        this.Disposed = true;
        this.OnDispose?.Invoke();

        if (this.DisposeDelay > TimeSpan.Zero)
        {
            await Task.Delay(this.DisposeDelay, CancellationToken.None);
        }

        if (this.DisposeGate is { } disposeGate)
        {
            await disposeGate;
        }

        if (this.DisposeFailure is not null)
        {
            throw this.DisposeFailure;
        }
    }
}

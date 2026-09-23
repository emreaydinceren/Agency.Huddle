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

    public async Task<IAgentSession> OpenAsync(CancellationToken cancellationToken)
    {
        if (this.OpenDelay > TimeSpan.Zero)
        {
            await Task.Delay(this.OpenDelay, cancellationToken).ConfigureAwait(false);
        }

        if (this.OpenGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            if (this.firstOpenDone)
            {
                session = new FakeAgentSession();
            }
            else
            {
                this.firstOpenDone = true;
                session = this.sharedSession;
            }

            this.sessions.Add(session);
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

    public ValueTask DisposeAsync()
    {
        this.Disposed = true;
        return ValueTask.CompletedTask;
    }
}

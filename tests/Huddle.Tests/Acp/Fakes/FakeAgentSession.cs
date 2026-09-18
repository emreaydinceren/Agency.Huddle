using System.Threading.Channels;
using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A test double for <see cref="IAgentSession"/> that records every prompt, lets a test push
/// arbitrary <see cref="AgentEvent"/>s onto its <see cref="Events"/> channel by queuing "turn plans",
/// and fails loudly (<see cref="OverlapDetected"/>, and by throwing) if two <see cref="PromptAsync"/>
/// calls ever overlap — the assertion behind the one-turn-at-a-time requirement on the Persona Agent host.
/// </summary>
internal sealed class FakeAgentSession : IAgentSession
{
    // Arbitrary: nothing under test reads Size, only the movement of Used.
    private const long ContextWindowSize = 200_000;

    private readonly Channel<AgentEvent> events = Channel.CreateUnbounded<AgentEvent>();
    private readonly Lock gate = new();
    private readonly Queue<TurnPlan> plannedTurns = new();
    private readonly Queue<IReadOnlyList<AgentEvent>> plannedToolEvents = new();
    private readonly List<string> prompts = [];

    private bool promptInFlight;
    private int cancelCallCount;
    private bool cancelObservedPromptInFlight;

    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    public ChannelReader<AgentEvent> Events => this.events.Reader;

    /// <summary>
    /// The models this fake advertises through <see cref="IAgentSession.Models"/>. Empty by default,
    /// matching the interface's own "empty means unknown" contract; a test proving the Model-not-in-
    /// catalog warning sets this before <see cref="Agency.Huddle.App.Acp.PersonaRunner.StartAsync"/>
    /// reads it.
    /// </summary>
    public IReadOnlyList<AgentModelOption> Models { get; set; } = [];

    public IReadOnlyList<AgentEffortOption> EffortLevels { get; } = [];

    public IReadOnlyList<string> Prompts
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.prompts];
            }
        }
    }

    public bool OverlapDetected { get; private set; }

    public string DefaultReplyText { get; set; } = "ok";

    /// <summary>How many times <see cref="CancelAsync"/> has been called, for a Stop test to assert against.</summary>
    public int CancelCallCount
    {
        get
        {
            lock (this.gate)
            {
                return this.cancelCallCount;
            }
        }
    }

    /// <summary>
    /// Whether a <see cref="CancelAsync"/> call has ever landed while a prompt was genuinely in
    /// flight. Counting calls to <see cref="CancelAsync"/> alone cannot prove an idle-timeout watchdog
    /// tells the far side BEFORE aborting locally (the obvious-but-wrong order a real
    /// <c>DotAcpAgentSession</c> silently no-ops on) - only observing that a prompt was still running
    /// when the cancel arrived can.
    /// </summary>
    public bool CancelObservedPromptInFlight
    {
        get
        {
            lock (this.gate)
            {
                return this.cancelObservedPromptInFlight;
            }
        }
    }

    public void EnqueueReply(params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, TimeSpan.Zero, []));
        }
    }

    public void EnqueueFailure(Exception exception)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan([], exception, TimeSpan.Zero, []));
        }
    }

    /// <summary>
    /// Completes <see cref="Events"/> with an exception, exactly as
    /// <c>DotAcpAgentSession.Fault</c> does when the underlying agent connection dies mid-turn. A
    /// channel completed this way stays completed: once called, no further event this fake writes
    /// (via a still-in-flight <see cref="PromptAsync"/>) will actually reach a reader.
    /// </summary>
    /// <param name="exception">The exception <see cref="Events"/>' reader should observe.</param>
    public void FaultEvents(Exception exception)
    {
        this.events.Writer.TryComplete(exception);
    }

    public void EnqueueDelayedReply(TimeSpan delay, params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, delay, []));
        }
    }

    /// <summary>
    /// Queues a turn whose reply chunks are published one at a time, waiting <paramref name="gap"/>
    /// before each - unlike <see cref="EnqueueDelayedReply"/>, which delays once before the whole
    /// reply arrives together. Lets a test prove a long streaming Turn survives an idle bound shorter
    /// than the Turn's total duration, because each chunk is itself a sign of life that restarts the
    /// bound before it can fire.
    /// </summary>
    /// <param name="gap">How long to wait before writing each chunk.</param>
    /// <param name="chunks">The reply text, delivered one chunk every <paramref name="gap"/>.</param>
    public void EnqueueDripFedReply(TimeSpan gap, params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, TimeSpan.Zero, [], DripGap: gap));
        }
    }

    /// <summary>
    /// Queues a turn that ends in a <see cref="StopReason"/> other than <see cref="StopReason.EndTurn"/> -
    /// a Refusal or a Cancellation - so a test can prove such a Turn is handled differently even when
    /// text arrived before the stop.
    /// </summary>
    /// <param name="reason">The <see cref="TurnCompleted"/> this turn reports.</param>
    /// <param name="chunks">The reply text, if any, published before the stop.</param>
    public void EnqueueReplyEndingIn(StopReason reason, params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, TimeSpan.Zero, [], reason));
        }
    }

    /// <summary>
    /// Queues a turn that reports context-window levels before it completes. These are LEVELS, not
    /// increments - the real adapter publishes how full the window is, so a level lower than the one
    /// before it is a compaction and must not be read as tokens spent.
    /// </summary>
    /// <param name="usageLevels">The <c>Used</c> values to publish, in order, before the reply.</param>
    /// <param name="chunks">The reply text.</param>
    public void EnqueueReplyWithUsage(IReadOnlyList<long> usageLevels, params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, TimeSpan.Zero, usageLevels));
        }
    }

    /// <summary>
    /// Queues zero or more ACP tool-call events to be published on the next <see cref="PromptAsync"/>
    /// call, before its reply chunks - so a test can prove <see cref="ToolCallStarted"/> and
    /// <see cref="ToolCallUpdated"/> events reach the wire as <c>ToolActivity</c> envelopes.
    /// </summary>
    /// <param name="events">The tool-call events to publish, in order.</param>
    public void EnqueueToolActivity(params AgentEvent[] events)
    {
        lock (this.gate)
        {
            this.plannedToolEvents.Enqueue(events);
        }
    }

    public Task<PromptResult> PromptAsync(string text, CancellationToken cancellationToken)
    {
        return this.RunPromptAsync(text, cancellationToken);
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.cancelCallCount++;
            if (this.promptInFlight)
            {
                this.cancelObservedPromptInFlight = true;
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<PromptResult> RunPromptAsync(string text, CancellationToken ct)
    {
        lock (this.gate)
        {
            if (this.promptInFlight)
            {
                this.OverlapDetected = true;
                throw new InvalidOperationException("A prompt is already in flight.");
            }

            this.promptInFlight = true;
            this.prompts.Add(text);
        }

        try
        {
            TurnPlan plan;
            lock (this.gate)
            {
                plan = this.plannedTurns.Count > 0
                    ? this.plannedTurns.Dequeue()
                    : new TurnPlan([this.DefaultReplyText], null, TimeSpan.Zero, []);
            }

            if (plan.Delay > TimeSpan.Zero)
            {
                await Task.Delay(plan.Delay, ct);
            }

            if (plan.Exception is not null)
            {
                throw plan.Exception;
            }

            IReadOnlyList<AgentEvent> toolEvents;
            lock (this.gate)
            {
                toolEvents = this.plannedToolEvents.Count > 0 ? this.plannedToolEvents.Dequeue() : [];
            }

            foreach (var toolEvent in toolEvents)
            {
                this.events.Writer.TryWrite(toolEvent);
            }

            foreach (var level in plan.UsageLevels)
            {
                this.events.Writer.TryWrite(new UsageUpdated(this.SessionId, ContextWindowSize, level));
            }

            if (plan.DripGap is { } dripGap)
            {
                foreach (var chunk in plan.Chunks)
                {
                    await Task.Delay(dripGap, ct);
                    this.events.Writer.TryWrite(new MessageChunk(this.SessionId, chunk));
                }
            }
            else
            {
                foreach (var chunk in plan.Chunks)
                {
                    this.events.Writer.TryWrite(new MessageChunk(this.SessionId, chunk));
                }
            }

            this.events.Writer.TryWrite(new TurnCompleted(this.SessionId, plan.Reason));

            return new PromptResult(plan.Reason);
        }
        finally
        {
            lock (this.gate)
            {
                this.promptInFlight = false;
            }
        }
    }

    private sealed record TurnPlan(
        IReadOnlyList<string> Chunks,
        Exception? Exception,
        TimeSpan Delay,
        IReadOnlyList<long> UsageLevels,
        StopReason Reason = StopReason.EndTurn,
        TimeSpan? DripGap = null);
}
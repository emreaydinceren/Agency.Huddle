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
    private readonly Channel<AgentEvent> events = Channel.CreateUnbounded<AgentEvent>();
    private readonly Lock gate = new();
    private readonly Queue<TurnPlan> plannedTurns = new();
    private readonly List<string> prompts = [];

    private bool promptInFlight;

    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    public ChannelReader<AgentEvent> Events => this.events.Reader;

    public IReadOnlyList<AgentModelOption> Models { get; } = [];

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

    public void EnqueueReply(params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, TimeSpan.Zero));
        }
    }

    public void EnqueueFailure(Exception exception)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan([], exception, TimeSpan.Zero));
        }
    }

    public void EnqueueDelayedReply(TimeSpan delay, params string[] chunks)
    {
        lock (this.gate)
        {
            this.plannedTurns.Enqueue(new TurnPlan(chunks, null, delay));
        }
    }

    public Task<PromptResult> PromptAsync(string text, CancellationToken cancellationToken)
    {
        return this.RunPromptAsync(text, cancellationToken);
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
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
                    : new TurnPlan([this.DefaultReplyText], null, TimeSpan.Zero);
            }

            if (plan.Delay > TimeSpan.Zero)
            {
                await Task.Delay(plan.Delay, ct);
            }

            if (plan.Exception is not null)
            {
                throw plan.Exception;
            }

            foreach (var chunk in plan.Chunks)
            {
                this.events.Writer.TryWrite(new MessageChunk(this.SessionId, chunk));
            }

            this.events.Writer.TryWrite(new TurnCompleted(this.SessionId, StopReason.EndTurn));

            return new PromptResult(StopReason.EndTurn);
        }
        finally
        {
            lock (this.gate)
            {
                this.promptInFlight = false;
            }
        }
    }

    private sealed record TurnPlan(IReadOnlyList<string> Chunks, Exception? Exception, TimeSpan Delay);
}
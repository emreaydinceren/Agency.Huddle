namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Agency.Huddle.Acp.Abstractions;

/// <summary>
/// A scripted <see cref="IAgentSession"/> whose event stream and prompt behaviour the test
/// controls. The test writes agent events to <see cref="Channel"/>; they are forwarded to
/// <see cref="Events"/>, the stream a <c>ConsoleRenderer</c> consumes.
/// </summary>
internal sealed class FakeAgentSession : IAgentSession
{
    private readonly Lock gate = new Lock();

    private readonly List<string> prompts = new List<string>();

    private readonly Channel<AgentEvent> outputChannel = System.Threading.Channels.Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private TaskCompletionSource<StopReason>? pendingTurnCompletion;

    private int cancelCount;

    internal FakeAgentSession(string sessionId = "sess-1")
    {
        this.SessionId = sessionId;
        _ = this.ForwardEventsAsync();
    }

    /// <summary>Called by <see cref="PromptAsync"/>. Default: completes once the test writes a <see cref="TurnCompleted"/> to <see cref="Channel"/>.</summary>
    internal Func<string, Task<PromptResult>>? OnPrompt { get; set; }

    /// <summary>The channel the test writes agent events to.</summary>
    internal Channel<AgentEvent> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<AgentEvent>();

    internal IReadOnlyList<string> Prompts
    {
        get
        {
            lock (this.gate)
            {
                return this.prompts.ToArray();
            }
        }
    }

    internal int CancelCount => Volatile.Read(ref this.cancelCount);

    internal bool Disposed { get; private set; }

    public string SessionId { get; }

    public ChannelReader<AgentEvent> Events => this.outputChannel.Reader;

    public IReadOnlyList<AgentModelOption> Models { get; } = [];

    public IReadOnlyList<AgentEffortOption> EffortLevels { get; } = [];

    public IReadOnlyList<AgentModeOption> ModeOptions { get; } = [];

    public string? CurrentModeId => null;

    public async Task<PromptResult> PromptAsync(string text, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            this.prompts.Add(text);
        }

        Func<string, Task<PromptResult>>? handler = this.OnPrompt;
        if (handler is not null)
        {
            return await handler(text).ConfigureAwait(false);
        }

        return await this.WaitForTurnCompletedAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task CancelAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref this.cancelCount);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        this.Disposed = true;
        this.Channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private async Task<PromptResult> WaitForTurnCompletedAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<StopReason> source = new TaskCompletionSource<StopReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (this.gate)
        {
            this.pendingTurnCompletion = source;
        }

        StopReason stopReason = await source.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new PromptResult(stopReason);
    }

    private async Task ForwardEventsAsync()
    {
        ChannelReader<AgentEvent> reader = this.Channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out AgentEvent? agentEvent))
                {
                    this.outputChannel.Writer.TryWrite(agentEvent);

                    if (agentEvent is TurnCompleted turnCompleted)
                    {
                        TaskCompletionSource<StopReason>? pending;
                        lock (this.gate)
                        {
                            pending = this.pendingTurnCompletion;
                            this.pendingTurnCompletion = null;
                        }

                        pending?.TrySetResult(turnCompleted.StopReason);
                    }
                }
            }

            this.outputChannel.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            this.outputChannel.Writer.TryComplete(ex);
        }
    }
}

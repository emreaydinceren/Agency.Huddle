namespace Agency.Huddle.Acp.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.Acp.DotAcp;

/// <summary>A scripted <see cref="ISessionSink"/> that records what it is told, for use by <c>DotAcpClientAdapter</c> tests.</summary>
internal sealed class FakeSessionSink : ISessionSink
{
    private readonly Lock gate = new Lock();

    private readonly List<AgentEvent> events = new List<AgentEvent>();

    internal FakeSessionSink(string sessionId)
    {
        this.SessionId = sessionId;
    }

    public string SessionId { get; }

    public IPermissionHandler PermissionHandler { get; set; } = new RecordingPermissionHandler();

    internal CancellationTokenSource PromptCts { get; } = new CancellationTokenSource();

    public CancellationToken PromptCancellation => this.PromptCts.Token;

    internal IReadOnlyList<AgentEvent> Events
    {
        get
        {
            lock (this.gate)
            {
                return this.events.ToArray();
            }
        }
    }

    internal Exception? FaultException { get; private set; }

    public bool TryPublish(AgentEvent agentEvent)
    {
        lock (this.gate)
        {
            this.events.Add(agentEvent);
        }

        return true;
    }

    public void Fault(Exception exception)
    {
        this.FaultException = exception;
    }
}

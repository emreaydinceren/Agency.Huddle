namespace Agency.Huddle.Acp.DotAcp;

using System;
using System.Threading;
using Agency.Huddle.Acp.Abstractions;

/// <summary>Routes agent-originated activity for one session to whatever is consuming it.</summary>
internal interface ISessionSink
{
    string SessionId { get; }

    IPermissionHandler PermissionHandler { get; }

    CancellationToken PromptCancellation { get; }

    bool TryPublish(AgentEvent agentEvent);

    void Fault(Exception exception);
}
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

    /// <summary>Gets the scope that answers this session's elicitation requests, or null while none is bound.</summary>
    IElicitationScope? ElicitationScope { get; }

    bool TryPublish(AgentEvent agentEvent);

    void Fault(Exception exception);
}
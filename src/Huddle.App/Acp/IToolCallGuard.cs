using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// One check <see cref="WorkDirPermissionHandler"/> makes on an Agent's tool call before it approves it. A guard returns the reason for
/// refusing, or <see langword="null"/> to pass. It does not catch: a guard that throws must not turn into an approval, so the exception
/// reaches the caller.
/// </summary>
internal interface IToolCallGuard
{
    /// <summary>Decides whether the tool call must be refused.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <param name="personaName">The Persona that made it.</param>
    /// <returns>The refusal reason, or <see langword="null"/> when this guard lets the call pass.</returns>
    string? RefusalFor(ToolCallInfo toolCall, string personaName);
}

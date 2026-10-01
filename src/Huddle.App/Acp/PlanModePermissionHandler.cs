using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Keeps a Persona whose Work Mode is <c>plan</c> in plan mode, by refusing the Adapter's request to
/// leave it (ADR-0033).
/// </summary>
/// <remarks>
/// <para>
/// When a model in plan mode finishes planning, <c>claude-agent-acp</c> asks permission to exit it: a
/// tool call of kind <see cref="ToolKind.SwitchMode"/> whose options include one that allows it once
/// (into Manual), others that raise the mode to <c>auto</c>, <c>bypassPermissions</c> or
/// <c>acceptEdits</c>, and "No, keep planning". The handler beneath prefers the one-time allow, so
/// without this guard a <c>plan</c> Persona would leave plan mode on its first request and the setting
/// would last only until the first plan was finished.
/// </para>
/// <para>
/// The id <c>"plan"</c> is Adapter-specific knowledge, and one of the two mode ids held in code (the
/// other is the hidden list, <see cref="WorkModePolicy"/>). If an Adapter's plan mode has another id the
/// guard is simply inert and the Persona behaves as it did before: a benign failure, so no per-Adapter
/// table is kept. Only a request of kind <see cref="ToolKind.SwitchMode"/> is touched; every other
/// request, including <see cref="ToolKind.Other"/>, goes to the handler beneath unchanged.
/// </para>
/// </remarks>
/// <param name="inner">The handler that decides every request this guard does not refuse.</param>
/// <param name="workMode">The Persona's effective Work Mode, or <see langword="null"/>.</param>
/// <param name="logger">Records each refusal; without a line a plan Persona that never leaves plan mode would be unexplained.</param>
internal sealed class PlanModePermissionHandler(IPermissionHandler inner, string? workMode, ILogger<PlanModePermissionHandler> logger)
    : IPermissionHandler
{
    /// <summary>The id <c>claude-agent-acp</c> gives its plan mode.</summary>
    internal const string PlanModeId = "plan";

    /// <inheritdoc />
    public Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(workMode, PlanModeId, StringComparison.Ordinal) || context.ToolCall.Kind != ToolKind.SwitchMode)
        {
            return inner.DecideAsync(context, cancellationToken);
        }

        logger.LogInformation("Refused a request to leave plan mode: this Persona's Work Mode is plan.");

        return Task.FromResult(PlanModePermissionHandler.Refuse(context.Options));
    }

    /// <summary>Picks a refusal option, preferring a one-time refusal, and cancels when none is offered.</summary>
    /// <param name="options">The options the Adapter offered.</param>
    /// <returns>The decision to send back.</returns>
    private static PermissionDecision Refuse(IReadOnlyList<PermissionOptionInfo> options)
    {
        PermissionOptionInfo? refusal = options.FirstOrDefault(option => option.Kind == PermissionOptionKind.RejectOnce)
            ?? options.FirstOrDefault(option => option.Kind == PermissionOptionKind.RejectAlways);

        return refusal is null ? PermissionDecision.Cancelled : new SelectedDecision(refusal.OptionId);
    }
}

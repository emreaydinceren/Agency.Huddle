using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Approves an Agent's permission requests the way <see cref="AutoApprovePermissionHandler"/> does,
/// with refusals: a tool call that writes inside the Agent's own home configuration directory
/// (<c>~/.claude</c>) is declined, and so is each call an <see cref="IToolCallGuard"/> in its list refuses.
/// </summary>
/// <remarks>
/// <para>
/// rules.md records that "the Work Dir is not a jail" — <c>Bash</c> and <c>Write</c> run agent-side
/// against the real disk — and that stays true. This is not a jail either. It closes one specific
/// blast radius that a Turn reached in practice: asked to remember a word, a Persona wrote a new file
/// into <c>~/.claude/projects/&lt;repo&gt;/memory/</c> and edited the <c>MEMORY.md</c> index beside it.
/// That directory belongs to the human's own coding agent, not to this application, and nothing in
/// the chat surface asked for it (<see cref="ProtectedDirectoryGuard"/>).
/// </para>
/// <para>
/// Two things made it worth a code change rather than a note. The write is invisible: tool activity
/// lives only in the Draft, which is discarded when the Turn ends, so on any Turn the human is not
/// watching live it leaves no trace in the app at all. And it silently falsifies tests about session
/// memory — a Persona that has written a word to disk can read it back after the restart that was
/// supposed to make it forget.
/// </para>
/// <para>
/// Deliberately narrow. The wider question the issue asks — what a Persona's tool surface should be
/// allowed to touch in general — is a product decision, not this type's to make: a Persona working on
/// a repository legitimately writes all over it, so a Work Dir jail would break the feature. What is
/// defensible without that decision is that an Agent has no business writing into the human's agent
/// configuration, whatever it was asked.
/// </para>
/// <para>
/// Only tool calls that name a path in a recognised argument are inspected. A <c>Bash</c> command
/// that redirects into the same directory is not caught, and pretending otherwise by pattern-matching
/// shell text would give a false sense of a boundary that is not there.
/// </para>
/// <para>
/// The one shell command it does read is <c>git merge</c> / <c>git push</c>, and only to ask the optional
/// <see cref="IMergeGate"/> whether the Team's <c>checks-before-merge</c> gate lets it run (PR-7); that is the
/// <see cref="MergeGateGuard"/>, which says what it does and does not catch.
/// The Human's own approval of a prompt is never intercepted: only the Agent's requests are answered here.
/// </para>
/// <para>
/// The further refusals protect files the Human owns, and all look at the tool call's kind, so a read is never refused
/// (Instructions spec §3.3, §5.6): an edit, delete or move of an <c>AGENTS.md</c> (<see cref="AgentsMdGuard"/>),
/// of a Raw original (<see cref="RawGuard"/>) and of a Team's Memory by anyone but its manager (<see cref="TeamMemoryWriteGuard"/>).
/// The guards are consulted in the order of the list given (see <see cref="PermissionGuardList"/>), and the first reason refuses.
/// The permission reply carries no reason text, so the reason is logged. A guard that throws does not approve: the exception propagates.
/// </para>
/// </remarks>
/// <param name="logger">Records each refusal; without a line here the refusal is as invisible as the write was.</param>
/// <param name="guards">The guards consulted in order; the first reason refuses. <see langword="null"/> leaves every call approved.</param>
/// <param name="personaName">The Persona this handler answers for, passed to each guard.</param>
/// <param name="workDir">The Agent's Work Dir, where a relative path in a tool call resolves for the log line; the process's current directory when <see langword="null"/>.</param>
internal sealed class WorkDirPermissionHandler(
    ILogger<WorkDirPermissionHandler> logger,
    IReadOnlyList<IToolCallGuard>? guards = null,
    string personaName = "",
    string? workDir = null)
    : IPermissionHandler
{
    /// <summary>The Agent's home configuration directory for the current user, or null when it cannot be resolved.</summary>
    /// <returns>The absolute path to <c>~/.claude</c>, or <see langword="null"/> when the profile directory is unknown.</returns>
    public static string? DefaultProtectedDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return profile.Length == 0 ? null : Path.Combine(profile, ".claude");
    }

    /// <inheritdoc />
    public async Task<PermissionDecision> DecideAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.Refusal(context.ToolCall) is { } refusal)
        {
            this.LogRefusal(context.ToolCall, refusal.Guard, refusal.Reason);
            return Refuse(context.Options);
        }

        // Last, after every refusal above, so a call a guard refuses never posts a card (Agent Connectors spec 8.4).
        return ConnectorCallClassifier.AsksHuman(context.ToolCall)
            ? await this.AskHumanAsync(context, cancellationToken).ConfigureAwait(false)
            : Approve(context.Options);
    }

    /// <summary>Asks each guard in order and stops at the first reason. A guard that throws is not caught here.</summary>
    /// <param name="toolCall">The call the Agent is asking permission for.</param>
    /// <returns>The first guard to refuse and its reason, or <see langword="null"/> when every guard passes.</returns>
    private (IToolCallGuard Guard, string Reason)? Refusal(ToolCallInfo toolCall)
    {
        if (guards is null)
        {
            return null;
        }

        foreach (IToolCallGuard guard in guards)
        {
            if (guard.RefusalFor(toolCall, personaName) is { } reason)
            {
                return (guard, reason);
            }
        }

        return null;
    }

    /// <summary>
    /// Logs one Warning for a refusal: the Persona, the paths the call names and the guard's reason, with the guard's type in a scope. The
    /// message is the one the file guards have always logged; a tool call's raw input is never logged beyond the paths and the reason.
    /// </summary>
    /// <param name="toolCall">The refused call.</param>
    /// <param name="guard">The guard that refused it.</param>
    /// <param name="reason">Why.</param>
    private void LogRefusal(ToolCallInfo toolCall, IToolCallGuard guard, string reason)
    {
        if (!logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        using IDisposable? scope = logger.BeginScope("Guard {Guard}", guard.GetType().Name);
        logger.LogWarning(
            "Refused a tool call for '{PersonaName}' on '{Path}': {Reason}",
            personaName,
            string.Join(", ", ToolCallPaths.From(toolCall, workDir ?? Directory.GetCurrentDirectory())),
            reason);
    }

    /// <summary>
    /// Stops a claude.ai connector call for the Human's answer. Only an accepted <c>allow_once</c> selects the one-time allow
    /// (never <see cref="Approve"/>, whose fallback is the standing allow); a missing scope, a Turn whose earlier question timed out,
    /// a decline, a cancellation, an unreadable answer and a failure all refuse.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="cancellationToken">Ends the wait: a Stop, the Turn ending, the session closing or the host disposing.</param>
    /// <returns>The one-time allow, or a refusal.</returns>
    private async Task<PermissionDecision> AskHumanAsync(PermissionRequestContext context, CancellationToken cancellationToken)
    {
        string? roomId = (context.Scope as IConnectorTurnLatch)?.ActiveRoomId;
        this.LogRequest(context, roomId);
        if (context.Scope is not { } scope || scope is IConnectorTurnLatch { ApprovalTimedOut: true })
        {
            logger.LogWarning(
                "Refused a connector call for '{PersonaName}' without asking: {Why}.",
                personaName,
                context.Scope is null ? "no Room to ask in" : "an earlier question on this Turn went unanswered");
            this.LogDecision(context, roomId, context.Scope is null ? "refused: no Room to ask in" : "refused: an earlier question on this Turn went unanswered");
            return Refuse(context.Options);
        }

        string outcome = "refused: no answer";
        try
        {
            ElicitationResult answer = await scope.ElicitAsync(ConnectorApprovalForm.Build(personaName, context), cancellationToken).ConfigureAwait(false);
            if (ConnectorApprovalForm.IsAllowOnce(answer) && Pick(context.Options, PermissionOptionKind.AllowOnce) is { } allow)
            {
                this.LogDecision(context, roomId, "allowed once");
                return allow;
            }

            outcome = answer is ElicitationAccepted ? "refused by the Human" : "refused: no answer";
        }
        catch (OperationCanceledException)
        {
            // A Stop, a closing session or the host's disposal: nobody answered, so the call is refused below.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Asking the Human about a connector call for '{PersonaName}' failed; the call is refused.", personaName);
            outcome = "refused: the question failed";
        }

        this.LogDecision(context, roomId, outcome);
        return Refuse(context.Options);
    }

    /// <summary>
    /// Records the wire facts of a request that reached the classifier and asks, before the question is posted: the tool, the MCP
    /// server and its source, the kind and the title. It is written before the wait so a call that never gets an answer still leaves
    /// a line, and what a real request looks like can be read from it. The arguments are never logged.
    /// </summary>
    private void LogRequest(PermissionRequestContext context, string? roomId)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        ToolCallInfo call = context.ToolCall;
        logger.LogInformation(
            "Connector call asks '{PersonaName}' in room {RoomId}: ToolName {ToolName}, McpServerName {McpServerName}, McpServerSource {McpServerSource}, Kind {Kind}, Title {Title}.",
            personaName,
            roomId ?? "(none)",
            ConnectorApprovalForm.LoggableName(call.ToolName),
            ConnectorApprovalForm.LoggableName(call.McpServerName),
            ConnectorApprovalForm.LoggableName(call.McpServerSource),
            Enum.GetName(call.Kind) ?? "(unknown)",
            ConnectorApprovalForm.LoggableName(call.Title));
    }

    /// <summary>
    /// Records one connector decision so a call that was allowed can be traced afterwards: the Agent, the tool, the Room and the
    /// answer. The arguments are never logged.
    /// </summary>
    private void LogDecision(PermissionRequestContext context, string? roomId, string answer)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        logger.LogInformation(
            "Connector call decision for '{PersonaName}' in room {RoomId}: tool {ToolName}, {Answer}.",
            personaName,
            roomId ?? "(none)",
            ConnectorApprovalForm.LoggableName(context.ToolCall.ToolName ?? context.ToolCall.Title),
            answer);
    }

    /// <summary>Picks a refusal option, preferring a one-time refusal, and cancels when none is offered.</summary>
    /// <param name="options">The options the Agent offered.</param>
    /// <returns>The decision to send back.</returns>
    private static PermissionDecision Refuse(IReadOnlyList<PermissionOptionInfo> options) =>
        Pick(options, PermissionOptionKind.RejectOnce) ?? Pick(options, PermissionOptionKind.RejectAlways) ?? PermissionDecision.Cancelled;

    /// <summary>
    /// Approves, preferring a one-time grant over a standing one — the behaviour this type inherits
    /// from <see cref="AutoApprovePermissionHandler"/>, kept identical so the only difference between
    /// them is the refusal above.
    /// </summary>
    /// <param name="options">The options the Agent offered.</param>
    /// <returns>The decision to send back.</returns>
    private static PermissionDecision Approve(IReadOnlyList<PermissionOptionInfo> options) =>
        Pick(options, PermissionOptionKind.AllowOnce) ?? Pick(options, PermissionOptionKind.AllowAlways) ?? PermissionDecision.Cancelled;

    /// <summary>Returns a decision selecting the first option of <paramref name="kind"/>, or null when there is none.</summary>
    /// <param name="options">The options the Agent offered.</param>
    /// <param name="kind">The kind to look for.</param>
    /// <returns>A <see cref="SelectedDecision"/>, or <see langword="null"/>.</returns>
    private static SelectedDecision? Pick(IReadOnlyList<PermissionOptionInfo> options, PermissionOptionKind kind)
    {
        foreach (var option in options)
        {
            if (option.Kind == kind)
            {
                return new SelectedDecision(option.OptionId);
            }
        }

        return null;
    }
}

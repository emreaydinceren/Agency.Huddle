namespace Agency.Huddle.Acp.Abstractions;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>Options used to start a new agent session.</summary>
public sealed class AgentSessionOptions
{
    public AgentSessionOptions(
        string cwd,
        IPermissionHandler permissionHandler,
        SystemPromptOptions? systemPrompt = null,
        ToolServerEndpoint? toolServer = null,
        string? model = null,
        string? effort = null,
        IReadOnlyDictionary<string, object>? meta = null,
        string? mode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cwd);
        ArgumentNullException.ThrowIfNull(permissionHandler);
        if (!Path.IsPathRooted(cwd))
        {
            throw new ArgumentException("Cwd must be an absolute path.", nameof(cwd));
        }

        this.Cwd = cwd;
        this.PermissionHandler = permissionHandler;
        this.SystemPrompt = systemPrompt;
        this.ToolServer = toolServer;

        // Normalise blank to null at the boundary: an empty-string model would never resolve
        // against a catalog and would warn on every session start.
        this.Model = string.IsNullOrWhiteSpace(model) ? null : model;

        // Same reasoning as Model above: a blank effort would never resolve against a catalog
        // either, and would warn on every session start for no reason.
        this.Effort = string.IsNullOrWhiteSpace(effort) ? null : effort;

        // An empty Meta counts as none supplied: the caller passed a dictionary but nothing to
        // merge, and this keeps "no meta at all" (session/new's _meta unset) reachable through
        // the same code path as a genuinely null one, rather than sending an empty object.
        this.Meta = meta is { Count: > 0 } ? meta : null;

        // Same reasoning as Effort above: a blank mode would never resolve against a catalog.
        this.Mode = string.IsNullOrWhiteSpace(mode) ? null : mode;
    }

    public string Cwd { get; }

    public IPermissionHandler PermissionHandler { get; }

    public SystemPromptOptions? SystemPrompt { get; }

    public ToolServerEndpoint? ToolServer { get; }

    /// <summary>
    /// The model id (an <see cref="AgentModelOption.Id"/>) the caller wants this session pinned
    /// to, or null to leave the agent on its own default. A value that does not match anything in
    /// the agent's advertised model catalog is not an error: the session starts anyway, on the
    /// agent's default, so a stale stored model never blocks a session from starting.
    /// </summary>
    public string? Model { get; }

    /// <summary>
    /// The effort id (an <see cref="AgentEffortOption.Id"/>) the caller wants this session pinned
    /// to, or null to send no <c>session/set_config_option</c> call for it at all. Applied AFTER
    /// <see cref="Model"/>, and resolved against the configOptions catalog the model switch
    /// produced - not the pre-switch one - because the adapter rebuilds its effort option for
    /// whichever model ends up current. A value matching nothing in that catalog is not an error:
    /// it is logged as a warning and the session starts on the model's default effort, exactly
    /// like an unmatched <see cref="Model"/>.
    /// </summary>
    public string? Effort { get; }

    /// <summary>
    /// Client-supplied entries merged into <c>session/new</c>'s <c>_meta</c>, beside
    /// <see cref="SystemPrompt"/>'s own <c>systemPrompt</c> key (RS §6.4, A-4). A key named
    /// <c>"systemPrompt"</c> here is overwritten by <see cref="SystemPrompt"/>'s own payload, never
    /// the other way round. Every value must be a plain CLR type - a dictionary, an array, or a
    /// primitive - never a <c>JsonNode</c>/<c>JsonElement</c>: <c>dotacp</c> serialises this
    /// dictionary with Newtonsoft, which does not know how to serialise
    /// <c>System.Text.Json</c>'s own node types. <see langword="null"/>, or an empty dictionary,
    /// sends no <c>claudeCode</c>-style entries at all - this type carries no Claude-specific word
    /// itself (RS §6.4).
    /// </summary>
    public IReadOnlyDictionary<string, object>? Meta { get; }

    /// <summary>
    /// The mode id (an <see cref="AgentModeOption.Id"/>) the caller wants this session started in,
    /// or null to send no <c>session/set_config_option</c> call for it at all. Applied AFTER
    /// <see cref="Model"/> and <see cref="Effort"/>, against the snapshot the previous call
    /// returned, because a model switch can itself change the mode. A value matching nothing in the
    /// advertised catalog is not an error: it is logged as a warning and the session starts in the
    /// agent's own default mode. The agent may also clamp the request; the session then reports
    /// the mode it really runs in.
    /// </summary>
    public string? Mode { get; }
}
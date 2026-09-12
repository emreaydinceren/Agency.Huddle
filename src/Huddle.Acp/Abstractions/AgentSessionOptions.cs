namespace Agency.Huddle.Acp.Abstractions;

using System;
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
        string? effort = null)
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
}
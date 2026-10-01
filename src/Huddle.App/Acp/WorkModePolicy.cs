using Agency.Huddle.Acp.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// Decides which Work Modes (ADR-0033) may be offered in the Teammate card and sent to an Adapter. By
/// default <c>bypassPermissions</c>, <c>auto</c> and <c>plan</c> are hidden. The first is advertised on
/// Windows and removes every prompt, and the second hands permission decisions to the model and silently
/// changes with the Model. <c>plan</c> is hidden because of what was measured live on 2026-09-30 (OQ-2):
/// when <see cref="PlanModePermissionHandler"/> refuses the Adapter's request to leave plan mode, the
/// Turn ends as cancelled and the plan sits in a tool call, not in text, so a plan Persona would go
/// silent. It can be offered again once a later phase captures the plan from the tool call. An operator
/// changes this with <c>Team:Acp:HiddenModes</c>.
/// </summary>
/// <remarks>
/// The default lives here rather than as an initialiser on <see cref="AcpOptions.HiddenModes"/>, because
/// <c>ConfigurationBinder</c> writes a bound array into a pre-populated one by index. Ids are compared
/// ordinally: they are protocol identifiers, not text.
/// </remarks>
internal sealed class WorkModePolicy
{
    private static readonly string[] DefaultHidden = ["bypassPermissions", "auto", "plan"];

    private readonly HashSet<string> hidden;

    /// <summary>Reads the hidden list once, from <see cref="AcpOptions.HiddenModes"/>.</summary>
    /// <param name="options">The bound <see cref="TeamOptions"/>.</param>
    public WorkModePolicy(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<string>? configured = options.Value.Acp.HiddenModes;
        this.hidden = new HashSet<string>(configured ?? WorkModePolicy.DefaultHidden, StringComparer.Ordinal);
    }

    /// <summary>Whether a mode may be offered and sent. A hidden mode never is, whatever is stored for a Persona.</summary>
    /// <param name="modeId">The mode id, as an Adapter advertises it.</param>
    /// <returns><see langword="false"/> when the id is on the hidden list.</returns>
    internal bool IsOffered(string modeId)
    {
        return !this.hidden.Contains(modeId);
    }

    /// <summary>The modes that may be offered, in the order the Adapter advertised them.</summary>
    /// <param name="modes">Every mode the Adapter advertised.</param>
    /// <returns>The advertised modes that are not hidden.</returns>
    internal IReadOnlyList<AgentModeOption> Filter(IReadOnlyList<AgentModeOption> modes)
    {
        return [.. modes.Where(mode => this.IsOffered(mode.Id))];
    }

    /// <summary>
    /// The Work Mode a session should actually be started in. This is the enforcement: a picker that hides
    /// a mode is only convenience, and a database row written by hand would still name it, so the hidden
    /// list is applied again here, where the options are built. Whether the Adapter advertises the mode
    /// is not checked here; that is the host's job, where the catalog is in hand.
    /// </summary>
    /// <param name="personaName">The Persona's name, for the log line.</param>
    /// <param name="workMode">The Persona's stored Work Mode, or <see langword="null"/>.</param>
    /// <param name="logger">Receives a warning when a stored mode is dropped.</param>
    /// <returns>The mode to send, or <see langword="null"/> to send none.</returns>
    internal string? EffectiveMode(string personaName, string? workMode, ILogger logger)
    {
        if (workMode is null || this.IsOffered(workMode))
        {
            return workMode;
        }

        logger.LogWarning(
            "Persona '{PersonaName}' has Work Mode '{WorkMode}', which is hidden; starting in the Adapter's own mode.",
            personaName,
            workMode);

        return null;
    }
}

using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.Acp.DotAcp;

/// <summary>
/// Reads the "mode" category entry out of a <c>session/new</c> or <c>session/set_config_option</c>
/// response's <c>configOptions</c>, and resolves a caller-requested mode id against it. A thin
/// façade over <see cref="SessionConfigSelects"/>, fixed to
/// <see cref="dotacp.protocol.SessionConfigOptionCategory.Mode"/>.
///
/// <see cref="Read"/> does NOT filter any id. In particular it keeps "default": for an effort
/// option that id is a sentinel meaning "send nothing", but for a mode it is a real mode (Manual,
/// "always ask before making changes") and the only way to choose it explicitly. Hiding modes
/// that are unsafe to offer is an app-layer decision owned elsewhere.
///
/// An absent entry is an answer, not a failure: an adapter may advertise no modes at all.
/// </summary>
internal static class ModeConfigOptions
{
    /// <summary>
    /// Flattens the mode selector's options, in wire order. Returns an empty list when the
    /// response carries no "mode" category option.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <returns>The advertised mode options, in wire order.</returns>
    internal static IReadOnlyList<AgentModeOption> Read(dotacp.protocol.SessionConfigOption[]? configOptions)
    {
        dotacp.protocol.SessionConfigSelect? select = SessionConfigSelects.Find(configOptions, dotacp.protocol.SessionConfigOptionCategory.Mode);
        List<AgentModeOption> result = [];
        foreach (dotacp.protocol.SessionConfigSelectOption option in SessionConfigSelects.Flatten(select))
        {
            result.Add(new AgentModeOption(option.Value, option.Name, option.Description));
        }

        return result;
    }

    /// <summary>
    /// The <c>currentValue</c> of the mode select: the mode the agent reports it is running in.
    /// Null when there is no mode option at all.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <returns>The current mode id, or null.</returns>
    internal static string? CurrentValue(dotacp.protocol.SessionConfigOption[]? configOptions)
    {
        dotacp.protocol.SessionConfigSelect? select = SessionConfigSelects.Find(configOptions, dotacp.protocol.SessionConfigOptionCategory.Mode);
        return select is null ? null : (string)select.CurrentValue;
    }

    /// <summary>
    /// Looks up <paramref name="mode"/> by exact match against an option's <c>value</c>. Returns
    /// false (never throws) when there is no mode option at all, or the id does not match any option.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <param name="mode">The requested mode id.</param>
    /// <param name="configId">The owning option's own id, when found.</param>
    /// <param name="value">The resolved value, when found.</param>
    /// <returns>True when <paramref name="mode"/> matched an option; otherwise false.</returns>
    internal static bool TryResolve(
        dotacp.protocol.SessionConfigOption[]? configOptions,
        string mode,
        out dotacp.protocol.SessionConfigId configId,
        out dotacp.protocol.SessionConfigValueId value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        return SessionConfigSelects.TryResolve(configOptions, dotacp.protocol.SessionConfigOptionCategory.Mode, mode, out configId, out value);
    }
}

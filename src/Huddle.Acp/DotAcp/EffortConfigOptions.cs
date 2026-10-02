namespace Agency.Huddle.Acp.DotAcp;

using System;
using System.Collections.Generic;
using Agency.Huddle.Acp.Abstractions;

/// <summary>
/// Reads the "thought_level" category entry out of a <c>session/new</c> or
/// <c>session/set_config_option</c> response's <c>configOptions</c>, and resolves a
/// caller-requested effort id against it. A thin façade over <see cref="SessionConfigSelects"/>,
/// fixed to <see cref="dotacp.protocol.SessionConfigOptionCategory.ThoughtLevel"/>.
///
/// Unlike <see cref="ModelConfigOptions"/>, an absent entry here is the NORMAL, expected case, not
/// "unknown": the adapter emits this option only while the CURRENT model reports effort support,
/// and rebuilds it on every model switch, silently clamping an unsupported pin back to "default"
/// rather than erroring. This must therefore be read against the configOptions snapshot true AFTER
/// any model switch, never a pre-switch one; an empty result means "this model offers no effort
/// choice" - an answer, not a failure.
///
/// <see cref="Read"/> does NOT filter the "default" sentinel the adapter always advertises first: a
/// protocol reader that silently drops an advertised option is exactly the failure class
/// docs/engineering/traps.md exists for. Filtering it, where wanted, is an app-layer concern owned
/// elsewhere.
/// </summary>
internal static class EffortConfigOptions
{
    /// <summary>
    /// Flattens the effort selector's options, in wire order. Returns an empty list when the
    /// response carries no "thought_level" category option.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <returns>The advertised effort options, in wire order.</returns>
    internal static IReadOnlyList<AgentEffortOption> Read(dotacp.protocol.SessionConfigOption[]? configOptions)
    {
        dotacp.protocol.SessionConfigSelect? select = SessionConfigSelects.Find(configOptions, dotacp.protocol.SessionConfigOptionCategory.ThoughtLevel);
        List<AgentEffortOption> result = new List<AgentEffortOption>();
        foreach (dotacp.protocol.SessionConfigSelectOption option in SessionConfigSelects.Flatten(select))
        {
            result.Add(new AgentEffortOption(option.Value, option.Name, option.Description));
        }

        return result;
    }

    /// <summary>
    /// Looks up <paramref name="effort"/> by exact match against an option's <c>value</c>. Returns
    /// false (never throws) when there is no effort option at all, or the id does not match any
    /// option.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <param name="effort">The requested effort id.</param>
    /// <param name="configId">The owning option's own id, when found.</param>
    /// <param name="value">The resolved value, when found.</param>
    /// <returns>True when <paramref name="effort"/> matched an option; otherwise false.</returns>
    internal static bool TryResolve(
        dotacp.protocol.SessionConfigOption[]? configOptions,
        string effort,
        out dotacp.protocol.SessionConfigId configId,
        out dotacp.protocol.SessionConfigValueId value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effort);

        return SessionConfigSelects.TryResolve(configOptions, dotacp.protocol.SessionConfigOptionCategory.ThoughtLevel, effort, out configId, out value);
    }
}

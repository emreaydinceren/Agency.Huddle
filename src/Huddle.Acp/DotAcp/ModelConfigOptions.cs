namespace Agency.Huddle.Acp.DotAcp;

using System;
using System.Collections.Generic;
using Agency.Huddle.Acp.Abstractions;

/// <summary>
/// Reads the "model" category entry out of a <c>session/new</c> response's <c>configOptions</c>,
/// and resolves a caller-requested model id against it for <c>session/set_config_option</c>. A
/// thin façade over <see cref="SessionConfigSelects"/>, which carries the protocol quirks shared by
/// every select-shaped config option; this class only fixes the category to
/// <see cref="dotacp.protocol.SessionConfigOptionCategory.Model"/> and projects the raw protocol
/// options into <see cref="AgentModelOption"/>.
/// </summary>
internal static class ModelConfigOptions
{
    /// <summary>
    /// Flattens the model selector's options, in wire order. Returns an empty list when the
    /// response carries no "model" category option - this is the UNKNOWN case, not "no models".
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <returns>The advertised model options, in wire order.</returns>
    internal static IReadOnlyList<AgentModelOption> Read(dotacp.protocol.SessionConfigOption[]? configOptions)
    {
        dotacp.protocol.SessionConfigSelect? select = SessionConfigSelects.Find(configOptions, dotacp.protocol.SessionConfigOptionCategory.Model);
        List<AgentModelOption> result = new List<AgentModelOption>();
        foreach (dotacp.protocol.SessionConfigSelectOption option in SessionConfigSelects.Flatten(select))
        {
            result.Add(new AgentModelOption(option.Value, option.Name, option.Description));
        }

        return result;
    }

    /// <summary>
    /// Looks up <paramref name="model"/> by exact match against an option's <c>value</c>. Returns
    /// false (never throws) when there is no model option at all, or the id does not match any
    /// option - a stale stored model must not prevent a session from starting on the agent's
    /// default.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <param name="model">The requested model id.</param>
    /// <param name="configId">The owning option's own id, when found.</param>
    /// <param name="value">The resolved value, when found.</param>
    /// <returns>True when <paramref name="model"/> matched an option; otherwise false.</returns>
    internal static bool TryResolve(
        dotacp.protocol.SessionConfigOption[]? configOptions,
        string model,
        out dotacp.protocol.SessionConfigId configId,
        out dotacp.protocol.SessionConfigValueId value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return SessionConfigSelects.TryResolve(configOptions, dotacp.protocol.SessionConfigOptionCategory.Model, model, out configId, out value);
    }
}

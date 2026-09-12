namespace Agency.Huddle.Acp.DotAcp;

using System;
using System.Collections.Generic;

/// <summary>
/// Carries the three protocol quirks shared by every select-shaped <c>session/new</c>
/// <c>configOptions</c> entry, regardless of which category it advertises (model, effort, mode,
/// ...): the category match must use struct equality, not a string compare, so an absent or
/// default category is a genuine miss rather than something that happens to coerce into matching;
/// a select's option values arrive as a flat list OR as named groups (ACP allows either shape, and
/// a reader that only understands the flat shape sees a grouped catalog as legitimately empty,
/// with no error anywhere); and the id to write back on <c>session/set_config_option</c> is always
/// the owning option's own <c>configId</c> - it is adapter-defined and must never be hardcoded.
/// </summary>
internal static class SessionConfigSelects
{
    /// <summary>
    /// Finds the select-shaped option for <paramref name="category"/> in a <c>session/new</c> (or
    /// <c>session/set_config_option</c>) response's <c>configOptions</c>, or null when there is
    /// none - an absent category, not a mismatch, so callers must not treat this as an error.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <param name="category">The category to find the select-shaped option for.</param>
    /// <returns>The matching select, or null when none is present.</returns>
    internal static dotacp.protocol.SessionConfigSelect? Find(
        dotacp.protocol.SessionConfigOption[]? configOptions,
        dotacp.protocol.SessionConfigOptionCategory category)
    {
        if (configOptions is null)
        {
            return null;
        }

        foreach (dotacp.protocol.SessionConfigOption option in configOptions)
        {
            // Struct equality, not a string compare: an absent/default category must be a miss,
            // never something that happens to coerce into matching.
            if (option is dotacp.protocol.SessionConfigSelect select && select.Category.Equals(category))
            {
                return select;
            }
        }

        return null;
    }

    /// <summary>
    /// Flattens a select's options, in wire order, walking both union branches - a flat list OR
    /// named groups. Returns the RAW protocol option, not a projected record, so a caller that
    /// needs to match against <c>option.Value</c> (see <see cref="TryResolve"/>) needs only this
    /// one walk. Returns an empty list for a null select.
    /// </summary>
    /// <param name="select">The select to flatten, or null.</param>
    /// <returns>The select's options, in wire order.</returns>
    internal static IReadOnlyList<dotacp.protocol.SessionConfigSelectOption> Flatten(dotacp.protocol.SessionConfigSelect? select)
    {
        if (select is null)
        {
            return Array.Empty<dotacp.protocol.SessionConfigSelectOption>();
        }

        List<dotacp.protocol.SessionConfigSelectOption> result = new List<dotacp.protocol.SessionConfigSelectOption>();
        if (select.Options.TryGetSessionConfigSelectOption(out dotacp.protocol.SessionConfigSelectOption[]? flatOptions))
        {
            result.AddRange(flatOptions);
        }
        else if (select.Options.TryGetSessionConfigSelectGroup(out dotacp.protocol.SessionConfigSelectGroup[]? groups))
        {
            foreach (dotacp.protocol.SessionConfigSelectGroup group in groups)
            {
                result.AddRange(group.Options);
            }
        }

        return result;
    }

    /// <summary>
    /// Looks up <paramref name="requestedValue"/> by exact match against an option's <c>value</c>
    /// within the select for <paramref name="category"/>. Returns false (never throws) when there
    /// is no such select at all, or the value does not match any option - a stale stored value must
    /// not block a session from starting.
    /// </summary>
    /// <param name="configOptions">The response's <c>configOptions</c> array, or null.</param>
    /// <param name="category">The category to resolve <paramref name="requestedValue"/> against.</param>
    /// <param name="requestedValue">The value to look up.</param>
    /// <param name="configId">The owning option's own id, when found.</param>
    /// <param name="value">The resolved value, when found.</param>
    /// <returns>True when <paramref name="requestedValue"/> matched an option; otherwise false.</returns>
    internal static bool TryResolve(
        dotacp.protocol.SessionConfigOption[]? configOptions,
        dotacp.protocol.SessionConfigOptionCategory category,
        string requestedValue,
        out dotacp.protocol.SessionConfigId configId,
        out dotacp.protocol.SessionConfigValueId value)
    {
        dotacp.protocol.SessionConfigSelect? select = SessionConfigSelects.Find(configOptions, category);
        if (select is not null)
        {
            foreach (dotacp.protocol.SessionConfigSelectOption option in SessionConfigSelects.Flatten(select))
            {
                if (string.Equals(option.Value, requestedValue, StringComparison.Ordinal))
                {
                    configId = select.Id;
                    value = requestedValue;
                    return true;
                }
            }
        }

        configId = default;
        value = default;
        return false;
    }
}

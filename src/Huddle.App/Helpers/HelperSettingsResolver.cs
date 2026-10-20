using Agency.Huddle.App.Data;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Works out a Helper's effective <see cref="HelperSettings"/> at each fire (Helpers Spec §6.6): the Human's row if
/// there is one, else <c>Team:Helpers:&lt;Id&gt;</c> over the Helper's code defaults. Nothing is cached, so a change on
/// the card applies to the next call.
/// </summary>
/// <param name="store">The Human's rows.</param>
/// <param name="options">The bound options.</param>
/// <param name="logger">Warns about a daily cap below 1.</param>
internal sealed class HelperSettingsResolver(HelperSettingsStore store, IOptions<TeamOptions> options, ILogger<HelperSettingsResolver> logger)
{
    /// <summary>The effective settings of <paramref name="helper"/>.</summary>
    /// <param name="helper">The Helper.</param>
    public HelperSettings For(IHelperAgent helper)
    {
        ArgumentNullException.ThrowIfNull(helper);
        HelperDefaults defaults = helper.Defaults;
        options.Value.Helpers.TryGetValue(helper.Id.Value, out HelperOptions? configured);
        configured ??= new HelperOptions();

        HelperSettings baseline = new(
            configured.Enabled ?? defaults.Enabled,
            configured.Adapter ?? defaults.Adapter,
            configured.Model ?? defaults.Model,
            configured.Effort ?? defaults.Effort,
            this.ValidCap(helper, configured.DailyCap, defaults.DailyCap),
            configured.ContinueGrant is > 0 ? configured.ContinueGrant.Value : defaults.ContinueGrant,
            configured.WaitSeconds is > 0 ? configured.WaitSeconds.Value : defaults.WaitSeconds,
            configured.TimeoutSeconds is > 0 ? configured.TimeoutSeconds.Value : defaults.TimeoutSeconds);

        HelperSettingsRow? row = store.Get(helper.Id);
        if (row is null)
        {
            return baseline;
        }

        // A row is all or nothing: its NULL Model means the Adapter's own default, never the configured Model, because the
        // configured Model belongs to the configured Adapter (ADR-0015).
        return baseline with
        {
            Enabled = row.Enabled,
            Adapter = row.Adapter,
            Model = row.Model,
            Effort = row.Effort,
            DailyCap = this.ValidCap(helper, row.DailyCap, defaults.DailyCap),
        };
    }

    /// <summary>The cap, or the code default with a warning when the value is below 1.</summary>
    private int ValidCap(IHelperAgent helper, int? value, int fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value.Value >= 1)
        {
            return value.Value;
        }

#pragma warning disable HAA0601 // Warning path: runs once per bad configured value, and the logger boxes its arguments.
        logger.LogWarning("Helper {HelperId} has a daily cap of {DailyCap}; using the default of {Default}.", helper.Id.Value, value.Value, fallback);
#pragma warning restore HAA0601
        return fallback;
    }
}

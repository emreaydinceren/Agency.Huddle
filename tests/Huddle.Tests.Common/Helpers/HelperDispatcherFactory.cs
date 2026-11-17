using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// Builds a <see cref="HelperDispatcher"/> over its one <see cref="HelperJudge"/>, as the container does, so the tests that
/// need a dispatcher name only the stores and the clock they care about.
/// </summary>
internal static class HelperDispatcherFactory
{
    /// <summary>Builds the judge and a dispatcher over it.</summary>
    /// <param name="invokers">One per registered Helper.</param>
    /// <param name="settings">Resolves each Helper's settings.</param>
    /// <param name="status">Where the card's last run and last error live.</param>
    /// <param name="options">The bound options.</param>
    /// <param name="timeProvider">The clock both use.</param>
    /// <param name="feed">The Feed's Helper health reporter, or <see langword="null"/>.</param>
    /// <param name="judge">The judge the dispatcher calls, for a test that judges directly.</param>
    /// <returns>The dispatcher.</returns>
    public static HelperDispatcher Create(
        IEnumerable<IHelperInvoker> invokers,
        HelperSettingsResolver settings,
        HelperStatusStore status,
        IOptions<TeamOptions> options,
        TimeProvider? timeProvider,
        HelperFeed? feed,
        out HelperJudge judge)
    {
        IHelperInvoker[] all = [.. invokers];
        judge = new HelperJudge(all, settings, status, options, NullLogger<HelperJudge>.Instance, timeProvider, feed);
        return new HelperDispatcher(all, settings, judge, NullLogger<HelperDispatcher>.Instance, timeProvider);
    }

    /// <summary>Builds a dispatcher over a judge the caller does not need.</summary>
    /// <param name="invokers">One per registered Helper.</param>
    /// <param name="settings">Resolves each Helper's settings.</param>
    /// <param name="status">Where the card's last run and last error live.</param>
    /// <param name="options">The bound options.</param>
    /// <param name="timeProvider">The clock both use.</param>
    /// <param name="feed">The Feed's Helper health reporter, or <see langword="null"/>.</param>
    /// <returns>The dispatcher.</returns>
    public static HelperDispatcher Create(
        IEnumerable<IHelperInvoker> invokers,
        HelperSettingsResolver settings,
        HelperStatusStore status,
        IOptions<TeamOptions> options,
        TimeProvider? timeProvider = null,
        HelperFeed? feed = null) =>
        Create(invokers, settings, status, options, timeProvider, feed, out _);
}

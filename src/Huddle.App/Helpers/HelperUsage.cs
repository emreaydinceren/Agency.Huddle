namespace Agency.Huddle.App.Helpers;

/// <summary>One Helper's spending on one local day.</summary>
/// <param name="Calls">Adapter processes started for the Helper today.</param>
/// <param name="Granted">Calls the Human added today with Continue.</param>
/// <param name="Skipped">Inputs dropped today because the Helper was paused.</param>
/// <param name="Cost">The sum of costs the adapter reported, when it reports them.</param>
/// <param name="Currency">The currency of <paramref name="Cost"/>.</param>
internal sealed record HelperUsage(int Calls, int Granted, int Skipped, decimal? Cost, string? Currency)
{
    /// <summary>A day with nothing recorded.</summary>
    public static HelperUsage None { get; } = new(0, 0, 0, null, null);
}

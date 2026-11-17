namespace Agency.Huddle.App.Scheduling;

/// <summary>How a scheduler treats lateness and failure.</summary>
/// <param name="Grace">Up to this late, a run is on time.</param>
/// <param name="RunLate">Whether a run later than Grace runs once (true) or is skipped (false).</param>
/// <param name="MaxLateness">A one-off later than this is skipped and given up; null for no limit.</param>
/// <param name="MergeWindow">A late run whose next regular run is closer than this is skipped in its favour.</param>
/// <param name="MaxFailedTries">After this many failed tries of one due time, give up on it.</param>
internal sealed record DuePolicy(TimeSpan Grace, bool RunLate, TimeSpan? MaxLateness, TimeSpan MergeWindow, int MaxFailedTries);

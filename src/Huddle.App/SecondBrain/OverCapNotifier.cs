using System.Collections.Concurrent;
using System.Globalization;
using Agency.Huddle.App.Feed;

namespace Agency.Huddle.App.SecondBrain;

/// <summary>
/// Tells the Human, once per folder per UTC day, that a Memory or outputs folder holds more files than Agents are shown (Spec §6.8, SB-R13).
/// The dedup key <c>(folder, UTC date)</c> lives in memory, so a restart may repeat one Notification.
/// </summary>
/// <param name="feed">Where the Notification goes.</param>
/// <param name="timeProvider">The clock that decides the UTC day; defaults to the system clock.</param>
internal sealed class OverCapNotifier(IFeedPublisher feed, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    private readonly ConcurrentDictionary<(string Folder, DateOnly Day), byte> reported = new();

    /// <summary>Posts the over-cap Notification for <paramref name="folder"/> unless it was already posted today (UTC).</summary>
    /// <param name="folder">The folder, as the Human knows it, for example <c>Teams/Home/memory</c>.</param>
    /// <param name="listed">How many files the index lists.</param>
    /// <param name="cap">The cap that left files out.</param>
    internal void Report(string folder, int listed, int cap)
    {
        DateOnly today = DateOnly.FromDateTime(this.clock.GetUtcNow().UtcDateTime);
        foreach ((string Folder, DateOnly Day) key in this.reported.Keys)
        {
            if (key.Day < today)
            {
                _ = this.reported.TryRemove(key, out _);
            }
        }

        if (!this.reported.TryAdd((folder, today), 0))
        {
            return;
        }

        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"{folder} has more Memory than Agents are shown ({listed} of {cap} listed). Ask the Team's manager to tidy it.");
        try
        {
            feed.Post(new FeedMessage(FeedSourceType.SecondBrainOverCap, folder, FeedActor.Huddle, true, new SecondBrainOverCapPayload(folder, message)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Feed is a report, never part of building a prompt: a failing Feed lets the next report retry and the session carry on.
            _ = this.reported.TryRemove((folder, today), out _);
        }
    }
}

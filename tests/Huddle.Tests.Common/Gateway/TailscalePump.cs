using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Drives a <see cref="FakeTimeProvider"/> in steps while background work runs, so tests never sleep for real time beyond a few milliseconds.</summary>
internal static class TailscalePump
{
    /// <summary>
    /// Repeats: wait a few real milliseconds (so continuations and timers register), return when <paramref name="done"/> holds, else advance
    /// <paramref name="clock"/> by <paramref name="step"/>. Fails when more than <paramref name="limit"/> of fake time passes.
    /// </summary>
    /// <param name="clock">The fake clock.</param>
    /// <param name="done">The condition awaited.</param>
    /// <param name="step">How far each round advances the clock.</param>
    /// <param name="limit">The most fake time to spend.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public static async Task UntilAsync(FakeTimeProvider clock, Func<bool> done, TimeSpan step, TimeSpan limit, CancellationToken cancellationToken)
    {
        TimeSpan spent = TimeSpan.Zero;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            if (done())
            {
                return;
            }

            if (spent >= limit)
            {
                throw new InvalidOperationException($"Condition not met after {limit} of fake time.");
            }

            clock.Advance(step);
            spent += step;
        }
    }

    /// <summary>Lets queued continuations run without moving the clock.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public static Task SettleAsync(CancellationToken cancellationToken) => Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
}

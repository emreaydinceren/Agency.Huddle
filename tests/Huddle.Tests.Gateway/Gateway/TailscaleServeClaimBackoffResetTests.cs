using System.Globalization;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Waiting;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Coordinator rule for Stage D2: the restart backoff resets to 1 s once the claim has been Running for 60 s continuously.</summary>
public sealed class TailscaleServeClaimBackoffResetTests
{
    private const string ServeStatus = "serve status --json";
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    private static string Claimed()
        => """{"TCP":{"5101":{"HTTPS":true}},"Web":{"desktop-a.tail1234.ts.net:5101":{"Handlers":{"/":{"Proxy":"http://127.0.0.1:41233"}}}}}""";

    /// <summary>
    /// Exits after short runs back off 1 s then 2 s; after a run of a full 60 s the next exit waits 1 s again, not 4 s.
    /// Each backoff is measured by moving the fake clock only while the claim is parked in it: a pump that keeps
    /// stepping the clock while the supervisor is still on its way to the delay (or from it to the next start)
    /// stretches the gap by a step on a loaded runner, which is how this test failed CI with 3 s for a 1 s backoff.
    /// </summary>
    [Fact]
    public async Task Restart_RunningForSixtySeconds_ResetsTheBackoffToOneSecond()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).Respond(ServeStatus, "{}");
        cli.OnChildStarted = _ => cli.Respond(ServeStatus, Claimed());
        await using TailscaleServeClaim claim = new(cli, clock);
        Task<TailscaleServeClaim.ClaimStatus> start = claim.StartAsync(new TailscaleServeClaim.ClaimRequest(false, 5101, 41233, GatewayAuthMode.Token), ct);
        await TailscalePump.UntilAsync(clock, () => start.IsCompleted, Step, Limit, ct);

        await ExitAndRestartAsync(clock, cli, claim, TimeSpan.FromSeconds(1), ct);
        await ExitAndRestartAsync(clock, cli, claim, TimeSpan.FromSeconds(2), ct);
        clock.Advance(TimeSpan.FromSeconds(60));
        await ExitAndRestartAsync(clock, cli, claim, TimeSpan.FromSeconds(1), ct);

        IReadOnlyList<FakeTailscaleChild> children = cli.Children;
        int[] expected = [1, 2, 1];
        for (int index = 0; index < expected.Length; index++)
        {
            DateTimeOffset? exited = children[index].ExitedAt;
            Assert.NotNull(exited);
            Assert.Equal(TimeSpan.FromSeconds(expected[index]), children[index + 1].StartedAt - exited.Value);
        }

        Assert.Equal("1", expected[0].ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Exits the newest child and proves the claim waits exactly <paramref name="backoff"/> before starting the next:
    /// nothing has started one tick short of it, and a child starts, and reaches Running, once the clock reaches it.
    /// The clock is still while the supervisor travels between states, so real-time scheduling cannot stretch the gap.
    /// </summary>
    /// <param name="clock">The fake clock.</param>
    /// <param name="cli">The fake CLI whose newest child is exited.</param>
    /// <param name="claim">The claim under test, Running on entry.</param>
    /// <param name="backoff">The wait the claim should make before restarting.</param>
    /// <param name="ct">The test's token.</param>
    private static async Task ExitAndRestartAsync(FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim, TimeSpan backoff, CancellationToken ct)
    {
        int before = cli.Children.Count;
        cli.Children[^1].Exit(1);

        // Restarting is set on the line before the backoff's Task.Delay; the settle covers that last step.
        await Eventually.UntilAsync(() => claim.Status.State == TailscaleServeClaim.ClaimState.Restarting, ct, timeout: TestBudgets.Slow);
        await TailscalePump.SettleAsync(ct);
        clock.Advance(backoff - TimeSpan.FromTicks(1));
        await TailscalePump.SettleAsync(ct);
        Assert.Equal(before, cli.Children.Count);

        clock.Advance(TimeSpan.FromTicks(1));
        await Eventually.UntilAsync(() => cli.Children.Count > before && claim.Status.State == TailscaleServeClaim.ClaimState.Running, ct, timeout: TestBudgets.Slow);
    }
}

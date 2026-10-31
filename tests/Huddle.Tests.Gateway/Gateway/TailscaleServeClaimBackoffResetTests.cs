using System.Globalization;
using Agency.Huddle.App.Gateway;
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

    /// <summary>Exits after short runs back off 1 s then 2 s; after a run of a full 60 s the next exit waits 1 s again, not 4 s.</summary>
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

        cli.Children[0].Exit(1);
        await TailscalePump.UntilAsync(clock, () => cli.Children.Count >= 2 && claim.Status.State == TailscaleServeClaim.ClaimState.Running, Step, Limit, ct);
        cli.Children[1].Exit(1);
        await TailscalePump.UntilAsync(clock, () => cli.Children.Count >= 3 && claim.Status.State == TailscaleServeClaim.ClaimState.Running, Step, Limit, ct);
        DateTimeOffset running = clock.GetUtcNow();
        await TailscalePump.UntilAsync(clock, () => clock.GetUtcNow() - running >= TimeSpan.FromSeconds(60), Step, Limit, ct);
        cli.Children[2].Exit(1);
        await TailscalePump.UntilAsync(clock, () => cli.Children.Count >= 4, Step, Limit, ct);

        IReadOnlyList<FakeTailscaleChild> children = cli.Children;
        int[] expected = [1, 2, 1];
        for (int index = 0; index < expected.Length; index++)
        {
            DateTimeOffset? exited = children[index].ExitedAt;
            Assert.NotNull(exited);
            TimeSpan gap = children[index + 1].StartedAt - exited.Value;
            Assert.InRange(gap, TimeSpan.FromSeconds(expected[index]), TimeSpan.FromSeconds(expected[index] + 1));
        }

        Assert.Equal("1", expected[0].ToString(CultureInfo.InvariantCulture));
    }
}

using System.Globalization;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 6.3 Claim, Release and Restart, 6.11, 8.1, E-2 and D-11 / D-13: the foreground <c>tailscale serve</c> child, its success test (the route shows
/// <c>tsPort to the managed listener</c>), its failures, the 1 to 60 s restart backoff and the release without <c>serve reset</c>. All with a fake CLI and a fake clock.
/// </summary>
public sealed class TailscaleServeClaimTests
{
    private const string ServeStatus = "serve status --json";
    private const int Managed = 41233;
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    private static TailscaleServeClaim.ClaimRequest Request(bool funnel = false, int tailscalePort = 5101, GatewayAuthMode mode = GatewayAuthMode.Token)
        => new(funnel, tailscalePort, Managed, mode);

    private static string Claimed(int tailscalePort)
        => """{"TCP":{"PORT":{"HTTPS":true}},"Web":{"desktop-a.tail1234.ts.net:PORT":{"Handlers":{"/":{"Proxy":"http://127.0.0.1:41233"}}}}}"""
            .Replace("PORT", tailscalePort.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static async Task<TailscaleServeClaim.ClaimStatus> StartAsync(TailscaleServeClaim claim, FakeTimeProvider clock, TailscaleServeClaim.ClaimRequest request, CancellationToken ct)
    {
        Task<TailscaleServeClaim.ClaimStatus> start = claim.StartAsync(request, ct);
        await TailscalePump.UntilAsync(clock, () => start.IsCompleted, Step, Limit, ct);
        return await start;
    }

    private static (FakeTimeProvider Clock, FakeTailscaleCli Cli, TailscaleServeClaim Claim) Create()
    {
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).Respond(ServeStatus, "{}");
        return (clock, cli, new TailscaleServeClaim(cli, clock));
    }

    /// <summary>Serve starts exactly <c>serve --bg=false --yes --https={tsPort} http://127.0.0.1:{managed}</c> and is Running once the route shows.</summary>
    [Fact]
    public async Task Start_Serve_RunsTheExactCommandAndIsRunningWhenTheRouteShows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.OnChildStarted = _ => cli.Respond(ServeStatus, Claimed(5101));

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.Running, status.State);
            Assert.Equal(TailscaleServeClaim.ClaimState.Running, claim.Status.State);
            Assert.Equal(Request(), claim.Request);
            TailscaleCall start = Assert.Single(cli.Starts);
            Assert.Equal(["serve", "--bg=false", "--yes", "--https=5101", "http://127.0.0.1:41233"], start.Args.ToArray());
        }
    }

    /// <summary>Funnel starts <c>funnel --bg=false --yes --https={tsPort} http://127.0.0.1:{managed}</c>.</summary>
    [Fact]
    public async Task Start_Funnel_UsesTheFunnelVerb()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.OnChildStarted = _ => cli.Respond(ServeStatus, Claimed(8443));

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(funnel: true, tailscalePort: 8443, mode: GatewayAuthMode.Password), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.Running, status.State);
            Assert.Equal(["funnel", "--bg=false", "--yes", "--https=8443", "http://127.0.0.1:41233"], Assert.Single(cli.Starts).Args.ToArray());
        }
    }

    /// <summary>A route that shows up a few seconds late still counts, inside the 15 s window.</summary>
    [Fact]
    public async Task Start_RouteShowsLate_IsRunningWithinFifteenSeconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            DateTimeOffset begun = clock.GetUtcNow();

            Task<TailscaleServeClaim.ClaimStatus> start = claim.StartAsync(Request(), ct);
            await TailscalePump.UntilAsync(clock, () => clock.GetUtcNow() - begun >= TimeSpan.FromSeconds(5), Step, Limit, ct);
            cli.Respond(ServeStatus, Claimed(5101));
            await TailscalePump.UntilAsync(clock, () => start.IsCompleted, Step, Limit, ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.Running, (await start).State);
            Assert.True(clock.GetUtcNow() - begun <= TimeSpan.FromSeconds(15));
        }
    }

    /// <summary>When the route never shows, the claim gives up at 15 s and kills the child.</summary>
    [Fact]
    public async Task Start_RouteNeverShows_TimesOutAtFifteenSecondsAndKillsTheChild()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            DateTimeOffset begun = clock.GetUtcNow();

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.StartTimedOut, status.State);
            Assert.InRange(clock.GetUtcNow() - begun, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(17));
            Assert.Equal(1, Assert.Single(cli.Children).KillCount);
        }
    }

    /// <summary>"listener already exists for port N" is the ownership conflict: the claim reports the port as taken and never retries.</summary>
    [Fact]
    public async Task Start_ListenerAlreadyExists_IsPortTakenAndNeverRetried()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.OnChildStarted = child =>
            {
                child.Emit("Error: listener already exists for port 5101");
                child.Exit(1);
            };

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(), ct);
            clock.Advance(TimeSpan.FromMinutes(5));
            await TailscalePump.SettleAsync(ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.TailscalePortTaken, status.State);
            Assert.Equal(TailscaleServeClaim.ClaimState.TailscalePortTaken, claim.Status.State);
            Assert.Single(cli.Starts);
        }
    }

    /// <summary>E-2: a route on the port that points elsewhere is somebody else's; the claim reports it without starting a child.</summary>
    [Fact]
    public async Task Start_ForeignRouteOnThePort_IsPortTakenWithoutStartingAChild()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.Respond(ServeStatus, TailscaleSamples.ServeForeignAndStray);

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.TailscalePortTaken, status.State);
            Assert.Empty(cli.Starts);
        }
    }

    /// <summary>The enable-HTTPS link in the child's output becomes <c>ServeNeedsEnabling(url)</c>.</summary>
    [Fact]
    public async Task Start_EnableLink_IsServeNeedsEnablingWithTheUrl()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.OnChildStarted = child =>
            {
                child.Emit("Serve is not enabled on your tailnet.");
                child.Emit("To enable, visit:");
                child.Emit(string.Empty);
                child.Emit("\t https://login.tailscale.com/f/serve?node=n123");
                child.Exit(1);
            };

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.ServeNeedsEnabling, status.State);
            Assert.Equal("https://login.tailscale.com/f/serve?node=n123", status.EnableUrl);
        }
    }

    /// <summary>A CLI that is not installed is reported as missing.</summary>
    [Fact]
    public async Task Start_CliMissing_IsTailscaleMissing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.MissingCli();

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.TailscaleMissing, status.State);
        }
    }

    /// <summary>After the route has run, unexpected exits restart the child with backoff 1, 2, 4, 8, 16, 32 s then capped at 60 s.</summary>
    [Fact]
    public async Task Restart_AfterUnexpectedExits_BacksOffFromOneSecondToSixty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.OnChildStarted = _ => cli.Respond(ServeStatus, Claimed(5101));
            _ = await StartAsync(claim, clock, Request(), ct);
            cli.Respond(ServeStatus, "{}");
            cli.OnChildStarted = child => child.Exit(1);

            cli.Children[0].Exit(1);
            await TailscalePump.UntilAsync(clock, () => cli.Children.Count >= 9, Step, Limit, ct);

            IReadOnlyList<FakeTailscaleChild> children = cli.Children;
            int[] expected = [1, 2, 4, 8, 16, 32, 60, 60];
            for (int index = 0; index < expected.Length; index++)
            {
                DateTimeOffset? exited = children[index].ExitedAt;
                Assert.NotNull(exited);
                TimeSpan gap = children[index + 1].StartedAt - exited.Value;
                Assert.InRange(gap, TimeSpan.FromSeconds(expected[index]), TimeSpan.FromSeconds(expected[index] + 1));
            }
        }
    }

    /// <summary>Shutdown kills the child exactly once (also when disposed twice), never runs <c>serve reset</c>, and stops restarting.</summary>
    [Fact]
    public async Task Dispose_KillsTheChildOnceAndNeverResets()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        cli.OnChildStarted = _ => cli.Respond(ServeStatus, Claimed(5101));
        _ = await StartAsync(claim, clock, Request(), ct);

        await claim.DisposeAsync();
        await claim.DisposeAsync();
        clock.Advance(TimeSpan.FromMinutes(5));
        await TailscalePump.SettleAsync(ct);

        Assert.Equal(1, Assert.Single(cli.Children).KillCount);
        Assert.Single(cli.Starts);
        Assert.Equal(TailscaleServeClaim.ClaimState.Stopped, claim.Status.State);
        Assert.DoesNotContain(cli.Runs, call => call.Text.Contains("reset", StringComparison.Ordinal));
        Assert.DoesNotContain(cli.Starts, call => call.Text.Contains("reset", StringComparison.Ordinal));
    }

    /// <summary>D-13: Funnel with any auth mode but password is refused and reported; no child starts and nothing else runs.</summary>
    [Theory]
    [InlineData((int)GatewayAuthMode.Token)]
    [InlineData((int)GatewayAuthMode.TrustedProxy)]
    [InlineData((int)GatewayAuthMode.None)]
    public async Task Start_FunnelWithoutPasswordMode_IsRefusedAndStartsNothing(int mode)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(funnel: true, tailscalePort: 443, mode: (GatewayAuthMode)mode), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.FunnelNeedsPassword, status.State);
            Assert.Empty(cli.Starts);
        }
    }

    /// <summary>Funnel accepts 443, 8443 and 10000.</summary>
    [Theory]
    [InlineData(443)]
    [InlineData(8443)]
    [InlineData(10000)]
    public async Task Start_FunnelOnAnAllowedPort_Runs(int port)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            cli.OnChildStarted = _ => cli.Respond(ServeStatus, Claimed(port));

            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(funnel: true, tailscalePort: port, mode: GatewayAuthMode.Password), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.Running, status.State);
        }
    }

    /// <summary>Funnel on any other port is refused and reported.</summary>
    [Theory]
    [InlineData(5101)]
    [InlineData(444)]
    public async Task Start_FunnelOnAnotherPort_IsRefusedAndStartsNothing(int port)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (FakeTimeProvider clock, FakeTailscaleCli cli, TailscaleServeClaim claim) = Create();
        await using (claim)
        {
            TailscaleServeClaim.ClaimStatus status = await StartAsync(claim, clock, Request(funnel: true, tailscalePort: port, mode: GatewayAuthMode.Password), ct);

            Assert.Equal(TailscaleServeClaim.ClaimState.FunnelPortNotAllowed, status.State);
            Assert.Empty(cli.Starts);
        }
    }
}

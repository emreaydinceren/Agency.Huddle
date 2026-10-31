using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 10 <c>TailscaleStatusPoller</c> and 6.3 / D-7: every 30 s status, MagicDNS name, owner login and stray routes; while the claim runs the name (and
/// <c>name:port</c>) is fed to <see cref="GatewayDynamicHosts"/> so a Serve request addressed to it passes host filtering.
/// </summary>
public sealed class TailscaleStatusPollerTests
{
    private const string ServeStatus = "serve status --json";
    private const string Status = "status --json";
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    private sealed class Rig : IAsyncDisposable
    {
        public Rig()
        {
            this.Clock = new FakeTimeProvider();
            this.Cli = new FakeTailscaleCli(this.Clock).Respond(Status, TailscaleSamples.StatusRunning).Respond(ServeStatus, "{}");
            this.Hosts = new GatewayDynamicHosts();
            this.Claim = new TailscaleServeClaim(this.Cli, this.Clock);
            this.Poller = new TailscaleStatusPoller(this.Cli, this.Hosts, this.Claim, 5101, this.Clock);
        }

        public FakeTimeProvider Clock { get; }

        public FakeTailscaleCli Cli { get; }

        public GatewayDynamicHosts Hosts { get; }

        public TailscaleServeClaim Claim { get; }

        public TailscaleStatusPoller Poller { get; }

        public async Task StartClaimAsync(CancellationToken ct)
        {
            this.Cli.OnChildStarted = _ => this.Cli.Respond(ServeStatus, TailscaleSamples.ServeClaimed);
            Task<TailscaleServeClaim.ClaimStatus> start = this.Claim.StartAsync(new TailscaleServeClaim.ClaimRequest(false, 5101, 41233, GatewayAuthMode.Token), ct);
            await TailscalePump.UntilAsync(this.Clock, () => start.IsCompleted, Step, TimeSpan.FromMinutes(1), ct);
            Assert.Equal(TailscaleServeClaim.ClaimState.Running, (await start).State);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Claim.DisposeAsync();
            this.Hosts.Dispose();
        }
    }

    /// <summary>A refresh records the status (name without the trailing dot, owner login) and the stray routes that point at the app port.</summary>
    [Fact]
    public async Task Refresh_RecordsStatusOwnerAndStrayRoutes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = new();
        rig.Cli.Respond(ServeStatus, TailscaleSamples.ServeForeignAndStray);

        await rig.Poller.RefreshAsync(ct);

        TailscaleStatusPoller.Snapshot? snapshot = rig.Poller.Current;
        Assert.NotNull(snapshot);
        Assert.Equal(TailscaleState.Running, snapshot.Status.State);
        Assert.Equal(TailscaleSamples.DnsName, snapshot.Status.DnsName);
        Assert.Equal("Ada@example.com", snapshot.Status.OwnerLogin);
        TailscaleServeStatus.Route stray = Assert.Single(snapshot.StrayRoutes);
        Assert.Equal(443, stray.Port);
    }

    /// <summary>While the claim is Running the MagicDNS name and <c>name:port</c> are added to the dynamic hosts and the change token fires.</summary>
    [Fact]
    public async Task Refresh_ClaimRunning_FeedsTheMagicDnsNameToHostFiltering()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = new();
        await rig.StartClaimAsync(ct);
        IChangeToken token = rig.Hosts.GetChangeToken();

        await rig.Poller.RefreshAsync(ct);

        Assert.True(token.HasChanged);
        Assert.Contains("desktop-a.tail1234.ts.net", rig.Hosts.Current);
        Assert.Contains("desktop-a.tail1234.ts.net:5101", rig.Hosts.Current);
    }

    /// <summary>Without a running claim nothing is fed: Huddle did not create a route, so it admits no Tailscale name.</summary>
    [Fact]
    public async Task Refresh_ClaimNotRunning_FeedsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = new();

        await rig.Poller.RefreshAsync(ct);

        Assert.DoesNotContain(rig.Hosts.Current, host => host.Contains(".ts.net", StringComparison.Ordinal));
    }

    /// <summary>A refresh that sees the same name again does not fire the token.</summary>
    [Fact]
    public async Task Refresh_SameName_DoesNotFireTheTokenAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = new();
        await rig.StartClaimAsync(ct);
        await rig.Poller.RefreshAsync(ct);
        IChangeToken token = rig.Hosts.GetChangeToken();

        await rig.Poller.RefreshAsync(ct);

        Assert.False(token.HasChanged);
        Assert.Contains("desktop-a.tail1234.ts.net", rig.Hosts.Current);
    }

    /// <summary>When Tailscale stops answering, the Tailscale hosts are withdrawn and the snapshot says so.</summary>
    [Fact]
    public async Task Refresh_CliGoesMissing_WithdrawsTheHosts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = new();
        await rig.StartClaimAsync(ct);
        await rig.Poller.RefreshAsync(ct);

        rig.Cli.MissingCli();
        await rig.Poller.RefreshAsync(ct);

        Assert.Equal(TailscaleState.Missing, rig.Poller.Current?.Status.State);
        Assert.DoesNotContain("desktop-a.tail1234.ts.net", rig.Hosts.Current);
    }

    /// <summary>The loop refreshes at once and then every 30 s.</summary>
    [Fact]
    public async Task Run_RefreshesAtOnceThenEveryThirtySeconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using Rig rig = new();
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task run = rig.Poller.RunAsync(stop.Token);
        await TailscalePump.UntilAsync(rig.Clock, () => rig.Cli.Runs.Count(call => call.Text == Status) >= 2, Step, TimeSpan.FromSeconds(40), ct);
        await stop.CancelAsync();
        Exception? ended = await Record.ExceptionAsync(() => run);

        IReadOnlyList<TailscaleCall> polls = [.. rig.Cli.Runs.Where(call => call.Text == Status)];
        Assert.InRange(polls[1].At - polls[0].At, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(31));
        Assert.True(ended is null or OperationCanceledException);
    }
}

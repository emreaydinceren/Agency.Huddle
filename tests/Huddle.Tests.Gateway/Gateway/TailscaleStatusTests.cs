using System.Net;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.3 Status: <c>tailscale status --json</c> parsed from a captured sample, and read with 3 attempts 500 ms apart through <see cref="ITailscaleCli"/>.</summary>
public sealed class TailscaleStatusTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>A running node yields its MagicDNS name without the trailing dot, its addresses and the owner login from the <c>User</c> map.</summary>
    [Fact]
    public void Parse_Running_ReadsNameAddressesAndOwner()
    {
        TailscaleStatus status = TailscaleStatus.Parse(TailscaleSamples.StatusRunning);

        Assert.Equal(TailscaleState.Running, status.State);
        Assert.Equal(TailscaleSamples.DnsName, status.DnsName);
        Assert.Contains(IPAddress.Parse("100.101.102.103"), status.Addresses);
        Assert.Equal("Ada@example.com", status.OwnerLogin);
    }

    /// <summary><c>NeedsLogin</c> is its own state and carries no name.</summary>
    [Fact]
    public void Parse_NeedsLogin_IsNeedsLogin()
    {
        TailscaleStatus status = TailscaleStatus.Parse(TailscaleSamples.StatusNeedsLogin);

        Assert.Equal(TailscaleState.NeedsLogin, status.State);
        Assert.Null(status.DnsName);
        Assert.Null(status.OwnerLogin);
    }

    /// <summary><c>Stopped</c> is its own state.</summary>
    [Fact]
    public void Parse_Stopped_IsStopped()
    {
        TailscaleStatus status = TailscaleStatus.Parse(TailscaleSamples.StatusStopped);

        Assert.Equal(TailscaleState.Stopped, status.State);
    }

    /// <summary>Output that is not a status document means Tailscale is not running.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Parse_Unreadable_IsNotRunning(string json)
    {
        TailscaleStatus status = TailscaleStatus.Parse(json);

        Assert.Equal(TailscaleState.NotRunning, status.State);
    }

    /// <summary>A CLI that cannot be started is reported as missing after one call, without retries.</summary>
    [Fact]
    public async Task ReadAsync_CliMissing_IsMissingAfterOneCall()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).MissingCli();

        TailscaleStatus status = await TailscaleStatus.ReadAsync(cli, clock, ct);

        Assert.Equal(TailscaleState.Missing, status.State);
        Assert.Single(cli.Runs);
    }

    /// <summary>A readable answer ends the read at once: <c>NeedsLogin</c> is not retried.</summary>
    [Fact]
    public async Task ReadAsync_NeedsLogin_IsNotRetried()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).Respond("status --json", TailscaleSamples.StatusNeedsLogin);

        TailscaleStatus status = await TailscaleStatus.ReadAsync(cli, clock, ct);

        Assert.Equal(TailscaleState.NeedsLogin, status.State);
        Assert.Single(cli.Runs);
    }

    /// <summary>Every call is <c>status --json</c> with a 10 s timeout; when each one times out there are 3 attempts 500 ms apart and the node counts as not running.</summary>
    [Fact]
    public async Task ReadAsync_TimesOutEveryTime_IsNotRunningAfterThreeAttemptsHalfASecondApart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).TimeOut("status --json");

        Task<TailscaleStatus> read = TailscaleStatus.ReadAsync(cli, clock, ct);
        await TailscalePump.UntilAsync(clock, () => read.IsCompleted, Step, Limit, ct);
        TailscaleStatus status = await read;

        IReadOnlyList<TailscaleCall> runs = cli.Runs;
        Assert.Equal(TailscaleState.NotRunning, status.State);
        Assert.Equal(3, runs.Count);
        Assert.All(runs, run => Assert.Equal("status --json", run.Text));
        Assert.All(runs, run => Assert.Equal(TimeSpan.FromSeconds(10), run.Timeout));
        Assert.InRange(runs[1].At - runs[0].At, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(600));
        Assert.InRange(runs[2].At - runs[1].At, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(600));
    }

    /// <summary>A non-zero exit is a failed attempt and is retried, up to 3 attempts.</summary>
    [Fact]
    public async Task ReadAsync_NonZeroExitEveryTime_IsNotRunningAfterThreeAttempts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).Respond("status --json", string.Empty, exitCode: 1, stderr: "failed to connect to local tailscaled");

        Task<TailscaleStatus> read = TailscaleStatus.ReadAsync(cli, clock, ct);
        await TailscalePump.UntilAsync(clock, () => read.IsCompleted, Step, Limit, ct);
        TailscaleStatus status = await read;

        Assert.Equal(TailscaleState.NotRunning, status.State);
        Assert.Equal(3, cli.Runs.Count);
    }

    /// <summary>Two failures followed by a good answer give the good answer.</summary>
    [Fact]
    public async Task ReadAsync_SucceedsOnTheThirdAttempt_ReturnsRunning()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new FakeTailscaleCli(clock).TimeOut("status --json");

        Task<TailscaleStatus> read = TailscaleStatus.ReadAsync(cli, clock, ct);
        await TailscalePump.UntilAsync(clock, () => cli.Runs.Count >= 2, Step, Limit, ct);
        cli.Respond("status --json", TailscaleSamples.StatusRunning);
        await TailscalePump.UntilAsync(clock, () => read.IsCompleted, Step, Limit, ct);
        TailscaleStatus status = await read;

        Assert.Equal(TailscaleState.Running, status.State);
        Assert.Equal(3, cli.Runs.Count);
    }
}

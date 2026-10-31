using System.Net;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.3 Who: <c>tailscale whois --json {ip}</c>, the login from <c>UserProfile</c>, no identity for a tagged node, cached 60 s per address and failures 5 s.</summary>
public sealed class TailscaleWhoisTests
{
    private static readonly IPAddress Phone = IPAddress.Parse("100.101.102.104");
    private static readonly IPAddress Laptop = IPAddress.Parse("100.101.102.105");
    private static readonly string[] PhoneArgs = ["whois", "--json", "100.101.102.104"];

    private static (TailscaleWhois Whois, FakeTailscaleCli Cli, FakeTimeProvider Clock) Create()
    {
        FakeTimeProvider clock = new();
        FakeTailscaleCli cli = new(clock);
        return (new TailscaleWhois(cli, clock), cli, clock);
    }

    /// <summary>The login comes from <c>UserProfile.LoginName</c>.</summary>
    [Fact]
    public void ParseLogin_UserNode_ReturnsTheLoginName()
    {
        Assert.Equal("ada@example.com", TailscaleWhois.ParseLogin(TailscaleSamples.WhoisUser));
    }

    /// <summary>A node with tags has no user identity, whatever <c>UserProfile</c> says.</summary>
    [Fact]
    public void ParseLogin_TaggedNode_HasNoIdentity()
    {
        Assert.Null(TailscaleWhois.ParseLogin(TailscaleSamples.WhoisTagged));
    }

    /// <summary>Unreadable answers have no identity.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not json")]
    public void ParseLogin_Unreadable_HasNoIdentity(string json)
    {
        Assert.Null(TailscaleWhois.ParseLogin(json));
    }

    /// <summary>A lookup runs <c>whois --json {ip}</c> and returns the login; it is the seam the gate already uses.</summary>
    [Fact]
    public async Task LookupLoginAsync_RunsWhoisAndReturnsTheLogin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TailscaleWhois whois, FakeTailscaleCli cli, _) = Create();
        cli.Respond("whois --json 100.101.102.104", TailscaleSamples.WhoisUser);

        string? login = await whois.LookupLoginAsync(Phone, ct);

        Assert.IsAssignableFrom<IIngressWhois>(whois);
        Assert.Equal("ada@example.com", login);
        Assert.Equal(PhoneArgs, cli.Runs[0].Args.ToArray());
    }

    /// <summary>A second lookup of the same address inside 60 s is answered from the cache; after 60 s it asks again.</summary>
    [Fact]
    public async Task LookupLoginAsync_CachesPerAddressForSixtySeconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TailscaleWhois whois, FakeTailscaleCli cli, FakeTimeProvider clock) = Create();
        cli.Respond("whois --json 100.101.102.104", TailscaleSamples.WhoisUser);

        _ = await whois.LookupLoginAsync(Phone, ct);
        clock.Advance(TimeSpan.FromSeconds(59));
        string? cached = await whois.LookupLoginAsync(Phone, ct);
        int afterCached = cli.Runs.Count;
        clock.Advance(TimeSpan.FromSeconds(2));
        string? refreshed = await whois.LookupLoginAsync(Phone, ct);

        Assert.Equal("ada@example.com", cached);
        Assert.Equal(1, afterCached);
        Assert.Equal("ada@example.com", refreshed);
        Assert.Equal(2, cli.Runs.Count);
    }

    /// <summary>The cache is per <see cref="IPAddress"/>: another address is a separate lookup.</summary>
    [Fact]
    public async Task LookupLoginAsync_DifferentAddresses_AreSeparateLookups()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TailscaleWhois whois, FakeTailscaleCli cli, _) = Create();
        cli.Respond("whois --json 100.101.102.104", TailscaleSamples.WhoisUser);
        cli.Respond("whois --json 100.101.102.105", TailscaleSamples.WhoisTagged);

        string? phone = await whois.LookupLoginAsync(Phone, ct);
        string? laptop = await whois.LookupLoginAsync(Laptop, ct);

        Assert.Equal("ada@example.com", phone);
        Assert.Null(laptop);
        Assert.Equal(2, cli.Runs.Count);
    }

    /// <summary>A failed lookup (non-zero exit) gives no identity and is cached for 5 s only.</summary>
    [Fact]
    public async Task LookupLoginAsync_Failure_IsCachedForFiveSeconds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TailscaleWhois whois, FakeTailscaleCli cli, FakeTimeProvider clock) = Create();
        cli.Respond("whois --json 100.101.102.104", string.Empty, exitCode: 1, stderr: "no match");

        string? first = await whois.LookupLoginAsync(Phone, ct);
        clock.Advance(TimeSpan.FromSeconds(4));
        string? second = await whois.LookupLoginAsync(Phone, ct);
        int withinWindow = cli.Runs.Count;
        cli.Respond("whois --json 100.101.102.104", TailscaleSamples.WhoisUser);
        clock.Advance(TimeSpan.FromSeconds(2));
        string? third = await whois.LookupLoginAsync(Phone, ct);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, withinWindow);
        Assert.Equal("ada@example.com", third);
        Assert.Equal(2, cli.Runs.Count);
    }

    /// <summary>A timeout and a missing CLI are failures too: no identity, no exception.</summary>
    [Fact]
    public async Task LookupLoginAsync_TimeoutOrMissingCli_HasNoIdentity()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TailscaleWhois slow, FakeTailscaleCli slowCli, _) = Create();
        slowCli.TimeOut("whois --json 100.101.102.104");
        (TailscaleWhois absent, FakeTailscaleCli absentCli, _) = Create();
        absentCli.MissingCli();

        string? timedOut = await slow.LookupLoginAsync(Phone, ct);
        string? missing = await absent.LookupLoginAsync(Phone, ct);

        Assert.Null(timedOut);
        Assert.Null(missing);
    }
}

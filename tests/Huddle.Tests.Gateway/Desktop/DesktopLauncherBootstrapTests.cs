using System.Text.RegularExpressions;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Spec 6.9, 6.2 and C-9: the desktop launcher opens the bootstrap URL on <c>/auth</c>, never the bare address, and maps an any-address to 127.0.0.1.</summary>
public sealed partial class DesktopLauncherBootstrapTests
{
    [GeneratedRegex(@"^http://127\.0\.0\.1:5123/auth\?return=%2F#bootstrapToken=(?<token>[A-Za-z0-9_-]{43})$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpectedUrl();

    /// <summary>The opened URL is <c>http://127.0.0.1:{port}/auth?return=%2F#bootstrapToken=...</c> and its token redeems as admin.</summary>
    [Fact]
    public async Task Publish_OpensTheAuthUrlWithAnAdminBootstrapToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        BootstrapTokenStore store = new();
        DesktopLifetimeService service = Create(dir, lifetime, opener, store, "http://127.0.0.1:5123");

        await service.StartAsync(ct);
        lifetime.FireStarted();

        string opened = Assert.Single(opener.Opened);
        Match match = ExpectedUrl().Match(opened);
        Assert.True(match.Success, opened);
        BootstrapTokenStore.Redemption redemption = store.Redeem(match.Groups["token"].Value, "dev-a");
        Assert.True(redemption.Valid);
        Assert.Equal(BootstrapTokenStore.Kind.Admin, redemption.TokenKind);
    }

    /// <summary>An any-address the server reports is opened, and recorded, as 127.0.0.1.</summary>
    /// <param name="reported">The address Kestrel reports.</param>
    [Theory]
    [InlineData("http://[::]:5123")]
    [InlineData("http://0.0.0.0:5123")]
    public async Task Publish_AnyAddress_IsMappedToLoopbackInTheUrlAndTheRecord(string reported)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = Create(dir, lifetime, opener, new BootstrapTokenStore(), reported);

        await service.StartAsync(ct);
        lifetime.FireStarted();

        string opened = Assert.Single(opener.Opened);
        Assert.Matches(ExpectedUrl(), opened);
        Assert.Equal("http://127.0.0.1:5123", InstanceFile.TryRead(dir.Path)?.Url);
    }

    /// <summary>The instance record holds the bare address: no token ever reaches <c>instance.json</c>.</summary>
    [Fact]
    public async Task Publish_InstanceRecordHoldsNoToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        DesktopLifetimeService service = Create(dir, lifetime, new RecordingBrowserOpener(), new BootstrapTokenStore(), "http://127.0.0.1:5123");

        await service.StartAsync(ct);
        lifetime.FireStarted();

        Assert.Equal("http://127.0.0.1:5123", InstanceFile.TryRead(dir.Path)?.Url);
        Assert.DoesNotContain("bootstrapToken", await File.ReadAllTextAsync(Path.Combine(dir.Path, "instance.json"), ct), StringComparison.Ordinal);
    }

    private static DesktopLifetimeService Create(TempDataDir dir, FakeLifetime lifetime, RecordingBrowserOpener opener, BootstrapTokenStore store, string address)
    {
        return new DesktopLifetimeService(
            new FakeServer(address),
            lifetime,
            Options.Create(new DesktopOptions { OpenBrowser = true }),
            dir.Options(),
            new DesktopSession(),
            opener,
            NullLogger<DesktopLifetimeService>.Instance,
            new BootstrapLinks(store));
    }

    /// <summary>An <see cref="IServer"/> that never listens and exposes one address.</summary>
    private sealed class FakeServer : IServer
    {
        public FakeServer(string address)
        {
            FakeAddresses feature = new();
            feature.Addresses.Add(address);
            FeatureCollection features = new();
            features.Set<IServerAddressesFeature>(feature);
            this.Features = features;
        }

        /// <inheritdoc />
        public IFeatureCollection Features { get; }

        /// <inheritdoc />
        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    private sealed class FakeAddresses : IServerAddressesFeature
    {
        /// <inheritdoc />
        public ICollection<string> Addresses { get; } = [];

        /// <inheritdoc />
        public bool PreferHostingUrls { get; set; }
    }
}

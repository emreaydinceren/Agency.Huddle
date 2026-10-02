using Agency.Huddle.App.Desktop;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins what a running desktop instance publishes on start and removes on stop.</summary>
public sealed class DesktopLifetimeServiceTests
{
    private const string FirstUrl = "http://127.0.0.1:5123";

    private const string SecondUrl = "http://localhost:5124";

    /// <summary>Once the application has started, the record holds the first bound address, the session's stop token and this process id.</summary>
    [Fact]
    public async Task ApplicationStarted_WithAddresses_WritesInstanceRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        DesktopSession session = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = Create(dir, lifetime, session, opener, openBrowser: true, FirstUrl, SecondUrl);

        await service.StartAsync(ct);
        lifetime.FireStarted();

        InstanceInfo? info = InstanceFile.TryRead(dir.Path);
        Assert.NotNull(info);
        Assert.Equal(FirstUrl, info.Url);
        Assert.Equal(session.StopToken, info.StopToken);
        Assert.Equal(Environment.ProcessId, info.Pid);
    }

    /// <summary>The record is not written by <c>StartAsync</c> itself: nothing is published before the server is listening.</summary>
    [Fact]
    public async Task StartAsync_BeforeApplicationStarted_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = Create(dir, lifetime, new DesktopSession(), opener, openBrowser: true, FirstUrl);

        await service.StartAsync(ct);

        Assert.Null(InstanceFile.TryRead(dir.Path));
        Assert.Empty(opener.Opened);
    }

    /// <summary>With <c>OpenBrowser</c> on, the browser is opened once with the first bound address.</summary>
    [Fact]
    public async Task ApplicationStarted_OpenBrowserTrue_OpensFirstAddressOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = Create(dir, lifetime, new DesktopSession(), opener, openBrowser: true, FirstUrl, SecondUrl);

        await service.StartAsync(ct);
        lifetime.FireStarted();

        Assert.Equal([FirstUrl], opener.Opened);
    }

    /// <summary>With <c>OpenBrowser</c> off, the browser is not opened but the record is still written.</summary>
    [Fact]
    public async Task ApplicationStarted_OpenBrowserFalse_DoesNotOpenButWritesRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = Create(dir, lifetime, new DesktopSession(), opener, openBrowser: false, FirstUrl);

        await service.StartAsync(ct);
        lifetime.FireStarted();

        Assert.Empty(opener.Opened);
        Assert.NotNull(InstanceFile.TryRead(dir.Path));
    }

    /// <summary>A server that reports no addresses leaves no record and opens nothing.</summary>
    [Fact]
    public async Task ApplicationStarted_NoAddresses_WritesNothingAndOpensNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = Create(dir, lifetime, new DesktopSession(), opener, openBrowser: true);

        await service.StartAsync(ct);
        lifetime.FireStarted();

        Assert.Null(InstanceFile.TryRead(dir.Path));
        Assert.Empty(opener.Opened);
    }

    /// <summary>A server with no addresses feature at all (the in-memory test server) is treated like one with no addresses.</summary>
    [Fact]
    public async Task ApplicationStarted_NoAddressesFeature_WritesNothingAndOpensNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new();
        DesktopLifetimeService service = new(
            new FakeServer(addresses: null),
            lifetime,
            Options.Create(new DesktopOptions { OpenBrowser = true }),
            dir.Options(),
            new DesktopSession(),
            opener,
            NullLogger<DesktopLifetimeService>.Instance);

        await service.StartAsync(ct);
        lifetime.FireStarted();

        Assert.Null(InstanceFile.TryRead(dir.Path));
        Assert.Empty(opener.Opened);
    }

    /// <summary>An opener that rejects the URL does not fail startup, and the record is still written.</summary>
    [Fact]
    public async Task ApplicationStarted_OpenerThrowsArgumentException_StillWritesRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        RecordingBrowserOpener opener = new() { ThrowOnOpen = new ArgumentException("not loopback") };
        DesktopLifetimeService service = Create(dir, lifetime, new DesktopSession(), opener, openBrowser: true, FirstUrl);

        await service.StartAsync(ct);
        Exception? thrown = Record.Exception(lifetime.FireStarted);

        Assert.Null(thrown);
        Assert.Equal([FirstUrl], opener.Opened);
        Assert.NotNull(InstanceFile.TryRead(dir.Path));
    }

    /// <summary>Stopping the service deletes the record.</summary>
    [Fact]
    public async Task StopAsync_AfterStart_DeletesInstanceRecord()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        DesktopLifetimeService service = Create(dir, lifetime, new DesktopSession(), new RecordingBrowserOpener(), openBrowser: false, FirstUrl);
        await service.StartAsync(ct);
        lifetime.FireStarted();
        Assert.NotNull(InstanceFile.TryRead(dir.Path));

        await service.StopAsync(ct);

        Assert.Null(InstanceFile.TryRead(dir.Path));
    }

    /// <summary>Builds a service over a fake server that reports <paramref name="addresses"/>.</summary>
    private static DesktopLifetimeService Create(
        TempDataDir dir,
        FakeLifetime lifetime,
        DesktopSession session,
        RecordingBrowserOpener opener,
        bool openBrowser,
        params string[] addresses)
    {
        return new DesktopLifetimeService(
            new FakeServer(addresses),
            lifetime,
            Options.Create(new DesktopOptions { OpenBrowser = openBrowser }),
            dir.Options(),
            session,
            opener,
            NullLogger<DesktopLifetimeService>.Instance);
    }

    /// <summary>An <see cref="IServer"/> that never listens and only exposes the addresses it was given.</summary>
    private sealed class FakeServer : IServer
    {
        /// <summary>Initializes the server; <paramref name="addresses"/> of <see langword="null"/> registers no addresses feature.</summary>
        public FakeServer(IEnumerable<string>? addresses)
        {
            FeatureCollection features = new();

            if (addresses is not null)
            {
                FakeAddresses feature = new();

                foreach (string address in addresses)
                {
                    feature.Addresses.Add(address);
                }

                features.Set<IServerAddressesFeature>(feature);
            }

            this.Features = features;
        }

        /// <inheritdoc />
        public IFeatureCollection Features { get; }

        /// <inheritdoc />
        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }

    /// <summary>A plain, mutable <see cref="IServerAddressesFeature"/>.</summary>
    private sealed class FakeAddresses : IServerAddressesFeature
    {
        /// <inheritdoc />
        public ICollection<string> Addresses { get; } = [];

        /// <inheritdoc />
        public bool PreferHostingUrls { get; set; }
    }
}

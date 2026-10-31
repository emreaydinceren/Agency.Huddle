using System.ComponentModel;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Review fix R-3: the bootstrap token carried by the opened URL never reaches a log entry.</summary>
public sealed class DesktopLifetimeServiceLogTests
{
    private const string Address = "http://127.0.0.1:5123";

    /// <summary>An opener that rejects the URL with an exception whose message repeats it still logs nothing containing the token.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Publish_OpenerThrows_NoLogEntryContainsTheBootstrapToken(int kind)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        using FakeLifetime lifetime = new();
        CapturingLogger logger = new();
        UrlEchoingOpener opener = new(kind);
        DesktopLifetimeService service = new(
            new FakeServer(Address),
            lifetime,
            Options.Create(new DesktopOptions { OpenBrowser = true }),
            dir.Options(),
            new DesktopSession(),
            opener,
            logger,
            new BootstrapLinks(new BootstrapTokenStore()));

        await service.StartAsync(ct);
        lifetime.FireStarted();

        string opened = Assert.Single(opener.Opened);
        Assert.Equal(BootstrapLinks.Suffix, opened[Address.Length..(opened.LastIndexOf('=') + 1)]);
        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("bootstrapToken", entry, StringComparison.Ordinal));
    }

    private sealed class UrlEchoingOpener(int kind) : IBrowserOpener
    {
        private readonly List<string> opened = [];

        public IReadOnlyList<string> Opened => this.opened;

        public void Open(string url)
        {
            this.opened.Add(url);
            throw kind switch
            {
                0 => new ArgumentException("Bad url " + url),
                1 => new UriFormatException("Bad url " + url),
                _ => new Win32Exception("Cannot start " + url),
            };
        }
    }

    private sealed class CapturingLogger : ILogger<DesktopLifetimeService>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => this.Entries.Add(formatter(state, exception) + " | " + state + " | " + exception);
    }

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

        public IFeatureCollection Features { get; }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeAddresses : IServerAddressesFeature
    {
        public ICollection<string> Addresses { get; } = [];

        public bool PreferHostingUrls { get; set; }
    }
}

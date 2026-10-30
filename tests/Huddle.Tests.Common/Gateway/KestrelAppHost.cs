using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// The real application on real sockets (D-6): <c>UseKestrel()</c> with no port and <c>urls = http://127.0.0.1:0</c>, so the listener plan is the only
/// endpoint source. It builds its own <see cref="HttpClient"/> from <see cref="GatewayListener.AppUrl"/>; every port is 0 unless a test says otherwise.
/// </summary>
internal sealed class KestrelAppHost : IDisposable
{
    /// <summary>The configured gateway token (token mode is the default).</summary>
    public const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly TeamWebApplicationFactory root = new();
    private readonly WebApplicationFactory<Program> app;

    /// <summary>Starts the app. Throws when <see cref="GatewayListener.AppUrl"/> is not set after start.</summary>
    /// <param name="settings">Configuration overrides (<c>Team:Gateway:*</c>, <c>Team:Desktop:*</c>); a <see langword="null"/> value is skipped.</param>
    /// <param name="beforeStart">Runs with the data directory before the app starts (to seed <c>team.db</c>).</param>
    public KestrelAppHost(IReadOnlyDictionary<string, string?>? settings = null, Action<string>? beforeStart = null)
    {
        beforeStart?.Invoke(this.root.DataDirPath);
        this.app = this.root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("urls", "http://127.0.0.1:0");
            builder.UseSetting("Team:Gateway:Auth:Mode", "token");
            builder.UseSetting("Team:Gateway:Auth:Token", Token);
            foreach ((string key, string? value) in settings ?? new Dictionary<string, string?>())
            {
                if (value is not null)
                {
                    builder.UseSetting(key, value);
                }
            }
        });
        this.app.UseKestrel();
        this.app.StartServer();

        this.Listener = this.app.Services.GetRequiredService<GatewayListener>();
        string url = this.Listener.AppUrl ?? throw new InvalidOperationException("GatewayListener.AppUrl was not set after the server started.");
        this.AppUrl = new Uri(url);
        this.Client = new HttpClient { BaseAddress = this.AppUrl };
    }

    /// <summary>The recorded listeners.</summary>
    public GatewayListener Listener { get; }

    /// <summary>A client for the app listener.</summary>
    public HttpClient Client { get; }

    /// <summary>The app URL.</summary>
    public Uri AppUrl { get; }

    /// <summary>The data directory holding <c>team.db</c> and <c>instance.json</c>.</summary>
    public string DataDir => this.root.DataDirPath;

    /// <inheritdoc />
    public void Dispose()
    {
        this.Client.Dispose();
        this.app.Dispose();
        this.root.Dispose();
    }
}

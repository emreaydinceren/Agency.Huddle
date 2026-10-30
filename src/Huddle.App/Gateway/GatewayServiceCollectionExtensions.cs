using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Gateway;

/// <summary>Registers and maps the gateway services of Stage B: the handshake, pairing and the <c>/auth</c> pages.</summary>
internal static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registers the settings (read once, from configuration and <c>team.db</c>), the in-memory stores, the device registry and the connect service.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    internal static IServiceCollection AddGateway(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(static provider => GatewaySettings.ReadForStartup(
            provider.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir,
            provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton(static provider => new AuthRateLimiter(provider.GetRequiredService<GatewaySettings>().Options.Auth.RateLimit));
        services.AddSingleton(static provider => new GatewayFeed(provider.GetRequiredService<Feed.IFeedPublisher>()));
        services.AddSingleton(static provider => new DeviceRegistry(
            provider.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir,
            provider.GetService<TimeProvider>(),
            provider.GetRequiredService<GatewayFeed>()));
        services.AddSingleton<IRemoteAccessState>(static provider => new RemoteAccessState(provider.GetRequiredService<GatewaySettings>().Options));
        services.AddSingleton<HostActions>();
        services.AddSingleton<ChallengeNonceStore>();
        services.AddSingleton<BootstrapTokenStore>();
        services.AddSingleton<BootstrapLinks>();
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<PairingService>();
        services.AddSingleton<GatewayConnectService>();
        services.AddSingleton<PreAuthConnectionLimiter>();
        services.AddSingleton<ITailscaleCli>(static provider => new ProcessTailscaleCli(provider.GetRequiredService<GatewaySettings>().Options.Tailscale.Command));
        services.AddSingleton<TailscaleServeClaim>(static provider => new TailscaleServeClaim(provider.GetRequiredService<ITailscaleCli>()));
        services.TryAddSingleton<IIngressWhois>(static provider => TailscaleRuntime.IsOn(provider.GetRequiredService<GatewaySettings>().Options)
            ? new TailscaleWhois(provider.GetRequiredService<ITailscaleCli>())
            : new NullIngressWhois());
        services.AddSingleton<IGatewaySessionResolver, CookieSessionResolver>();
        services.AddSingleton<GatewayConnectionGuard>();
        services.AddSingleton<GatewayListener>();
        services.AddSingleton<IPostConfigureOptions<KestrelServerOptions>, GatewayKestrelConfigurator>();
        services.AddHostedService<GatewayListenerStartup>();
        services.AddSingleton<TailscaleRuntime>();
        services.AddHostedService(static provider => provider.GetRequiredService<TailscaleRuntime>());
        services.AddSingleton(static provider => new GatewaySettingsStore(provider.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir));
        services.AddSingleton<IQrPngEncoder, QrCoderPngEncoder>();
        services.AddSingleton<IGatewayRuntimeView, GatewayRuntimeView>();
        services.AddSingleton<GatewayAdminService>();
        services.AddSingleton<GatewayDynamicHosts>();
        services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(static provider => provider.GetRequiredService<GatewayDynamicHosts>());
        services.AddOptions<HostFilteringOptions>().PostConfigure<GatewayDynamicHosts>(static (options, dynamic) =>
        {
            if (options.AllowedHosts.Count > 0)
            {
                options.AllowedHosts = [.. options.AllowedHosts, .. dynamic.Current];
            }
        });
        services.AddSignalR(options => options.AddFilter<GatewayConnectionGuard>());
        services.AddAuthentication(GatewayClaims.AuthenticationType)
            .AddScheme<AuthenticationSchemeOptions, GatewayAuthenticationHandler>(GatewayClaims.AuthenticationType, null);
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        return services;
    }

    /// <summary>
    /// Maps <c>/auth</c> (the connect page) and the five <c>/auth/*</c> endpoints. Mapping only: nothing here gates other requests (Stage C).
    /// Resolves the settings now, so a blank or placeholder secret fails at start-up and not at the first handshake.
    /// </summary>
    /// <param name="app">The application whose endpoint table is extended.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    internal static WebApplication MapGatewayAuthEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.Services.GetRequiredService<GatewaySettings>();
        _ = app.MapGet("/auth", static (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Content(ConnectPage.Html, "text/html", System.Text.Encoding.UTF8);
        }).DisableAntiforgery();
        _ = app.MapConnectEndpoints();
        return app;
    }
}

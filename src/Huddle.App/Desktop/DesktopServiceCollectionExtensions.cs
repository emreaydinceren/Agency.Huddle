using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Desktop;

/// <summary>
/// Registers and maps desktop host mode. Every member does nothing unless <c>Team:Desktop:Enabled</c> is
/// <see langword="true"/>, so tests and <c>run.ps1</c> see exactly the application they always did.
/// </summary>
internal static class DesktopServiceCollectionExtensions
{
    /// <summary>
    /// Registers the desktop services (options, per-instance session, browser opener, shutdown seam and the
    /// lifetime service that publishes <c>instance.json</c>) when desktop mode is enabled.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that carries the <c>Team:Desktop</c> section.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    internal static IServiceCollection AddDesktopHost(this IServiceCollection services, IConfiguration configuration)
    {
        if (!IsEnabled(configuration))
        {
            return services;
        }

        services.Configure<DesktopOptions>(configuration.GetSection(DesktopOptions.SectionName));
        services.AddSingleton<DesktopSession>();
        services.AddSingleton<IDesktopShutdown, HostDesktopShutdown>();
        services.AddSingleton<IBrowserOpener>(static provider =>
            provider.GetRequiredService<IOptions<DesktopOptions>>().Value.OpenBrowser
                ? new SystemBrowserOpener()
                : new NoopBrowserOpener());
        services.AddHostedService<DesktopLifetimeService>();
        return services;
    }

    /// <summary>Maps <c>POST /_desktop/stop</c> and <c>POST /_desktop/bootstrap</c> when desktop mode is enabled.</summary>
    /// <param name="app">The application whose endpoint table is extended.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    internal static WebApplication MapDesktopEndpoints(this WebApplication app)
    {
        if (!IsEnabled(app.Configuration))
        {
            return app;
        }

        app.MapPost(DesktopStopEndpoint.Route, DesktopStopEndpoint.HandleAsync).DisableAntiforgery();
        app.MapPost(DesktopBootstrapEndpoint.Route, DesktopBootstrapEndpoint.HandleAsync).DisableAntiforgery();
        return app;
    }

    /// <summary>Reads <c>Team:Desktop:Enabled</c>; a missing or unparsable value is off.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns><see langword="true"/> when desktop mode is switched on.</returns>
    private static bool IsEnabled(IConfiguration configuration)
    {
        return configuration.GetSection(DesktopOptions.SectionName).GetValue<bool>(nameof(DesktopOptions.Enabled));
    }
}

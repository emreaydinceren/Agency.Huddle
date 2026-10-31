using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// Applies the <see cref="GatewayListenerPlan"/> to Kestrel after every other option setup (D-3, D-4): the only place that decides which endpoints Kestrel binds.
/// It adds nothing when Bind is loopback, Tailscale is off and desktop mode is off.
/// </summary>
/// <param name="listener">Keeps the listen options to read the bound ports after start.</param>
/// <param name="settings">The gateway settings.</param>
/// <param name="configuration">Supplies <c>urls</c>, <c>http_ports</c>, <c>https_ports</c> and the desktop switch.</param>
/// <param name="team">Where <c>team.db</c> lives, for the stored desktop port.</param>
internal sealed class GatewayKestrelConfigurator(
    GatewayListener listener,
    GatewaySettings settings,
    IConfiguration configuration,
    IOptions<TeamOptions> team) : IPostConfigureOptions<KestrelServerOptions>
{
    /// <summary>Reads the configuration, plans, and applies the plan.</summary>
    /// <param name="name">Not used.</param>
    /// <param name="options">The Kestrel options.</param>
    public void PostConfigure(string? name, KestrelServerOptions options)
    {
        GatewayOptions gateway = settings.Options;
        bool desktop = GatewayListenerStartup.IsDesktop(configuration);
        int? stored = desktop ? new GatewaySettingsStore(team.Value.DataDir).DesktopPort : null;
        bool container = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
        GatewayListenerPlan.Input input = new(
            configuration["urls"],
            configuration["http_ports"],
            configuration["https_ports"],
            gateway.Bind,
            gateway.CustomBindHost,
            gateway.Port,
            gateway.Auth.Mode,
            GatewayListenerStartup.UsesManagedListener(gateway),
            desktop,
            stored,
            container);
        listener.Apply(options, GatewayListenerPlan.Create(input, SocketProbe.Instance));
    }
}

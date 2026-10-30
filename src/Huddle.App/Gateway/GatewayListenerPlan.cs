using System.Net;

namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The pure decision of which Kestrel listeners the gateway adds (Spec 6.2, 8.1; corrections D-3, D-4). It reads the configured URLs and the gateway
/// settings and returns a flat list of endpoints; <see cref="GatewayListener"/> turns each into one <c>Listen*</c> call. An empty list means Kestrel
/// binds exactly as configured. STAGE D1 STUB: every member throws until implemented.
/// </summary>
internal static class GatewayListenerPlan
{
    /// <summary>Which listener an endpoint is.</summary>
    internal enum Role
    {
        /// <summary>An app listener (the browser's address).</summary>
        App,

        /// <summary>The loopback listener Tailscale Serve or Funnel proxies to, on an ephemeral port.</summary>
        Managed,
    }

    /// <summary>The kind of <c>Listen*</c> call.</summary>
    internal enum Bind
    {
        /// <summary><c>ListenLocalhost(port)</c>; never emitted with port 0.</summary>
        Localhost,

        /// <summary><c>Listen(address, port)</c>.</summary>
        Address,

        /// <summary><c>ListenAnyIP(port)</c> (dual-mode).</summary>
        AnyAddress,
    }

    /// <summary>The seam over the machine: port probing and the tailnet address.</summary>
    internal interface IProbe
    {
        /// <summary>Whether <paramref name="port"/> can be bound exclusively now.</summary>
        /// <param name="port">A non-zero port.</param>
        bool IsPortFree(int port);

        /// <summary>The first unicast address in <c>100.64.0.0/10</c>, or <see langword="null"/>.</summary>
        IPAddress? TailnetAddress();
    }

    /// <summary>The configuration the plan reads.</summary>
    /// <param name="Urls"><c>Configuration["urls"]</c> (semicolon-separated), or <see langword="null"/>.</param>
    /// <param name="HttpPorts"><c>Configuration["http_ports"]</c>, or <see langword="null"/>.</param>
    /// <param name="HttpsPorts"><c>Configuration["https_ports"]</c>, or <see langword="null"/>.</param>
    /// <param name="Bind"><c>Team:Gateway:Bind</c> (<c>loopback</c>, <c>tailnet</c>, <c>lan</c>, <c>auto</c>, <c>custom</c>).</param>
    /// <param name="CustomBindHost"><c>Team:Gateway:CustomBindHost</c>, an IP literal.</param>
    /// <param name="ConfiguredPort"><c>Team:Gateway:Port</c>.</param>
    /// <param name="AuthMode">The gateway auth mode.</param>
    /// <param name="ManagedListener">Whether <c>Tailscale:Mode</c> is <c>serve</c> or <c>funnel</c>.</param>
    /// <param name="DesktopMode">Whether desktop mode is on.</param>
    /// <param name="StoredDesktopPort">The port stored by the last desktop start.</param>
    /// <param name="InContainer">Whether the process runs in a container (<c>auto</c> binds any address there).</param>
    internal sealed record Input(
        string? Urls,
        string? HttpPorts,
        string? HttpsPorts,
        string Bind,
        string? CustomBindHost,
        int? ConfiguredPort,
        GatewayAuthMode AuthMode,
        bool ManagedListener,
        bool DesktopMode,
        int? StoredDesktopPort,
        bool InContainer);

    /// <summary>One <c>Listen*</c> call.</summary>
    /// <param name="Role">App or managed.</param>
    /// <param name="Bind">The call.</param>
    /// <param name="Address">The address for <see cref="Bind.Address"/>; otherwise <see langword="null"/>.</param>
    /// <param name="Port">The port; 0 only for <see cref="Bind.Address"/>.</param>
    /// <param name="Https">Whether to call <c>UseHttps()</c>.</param>
    internal sealed record Endpoint(Role Role, Bind Bind, IPAddress? Address, int Port, bool Https);

    /// <summary>The plan.</summary>
    /// <param name="Endpoints">The listeners to add in code; empty when Kestrel should bind as configured.</param>
    /// <param name="AppPort">The app port of Spec 8.1: <c>Port ?? the single non-zero configured port ?? null</c>.</param>
    /// <param name="Reason">Why the app starts loopback-only or remote access is unavailable; <see langword="null"/> when nothing was refused.</param>
    internal sealed record Result(IReadOnlyList<Endpoint> Endpoints, int? AppPort, string? Reason);

    /// <summary>Plans the listeners.</summary>
    /// <param name="input">The configuration.</param>
    /// <param name="probe">The machine seam.</param>
    public static Result Create(Input input, IProbe probe)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(probe);

        List<Endpoint>? app = ParseUrls(input, out string? unavailable);
        if (app is null)
        {
            return new Result([], null, unavailable);
        }

        int? appPort = input.ConfiguredPort ?? SingleNonZeroPort(app);
        bool remote = IsRemote(input);
        if (!remote && !input.ManagedListener && !input.DesktopMode)
        {
            return new Result([], appPort, null);
        }

        if (input.DesktopMode)
        {
            app = ReplacePortZero(app, input.ConfiguredPort ?? DesktopPortChoice.Choose(input.StoredDesktopPort, probe.IsPortFree));
        }

        string? reason = null;
        Endpoint? added = null;
        if (remote)
        {
            (added, reason) = PlanRemote(input, appPort, app, probe);
            if (added is not null)
            {
                app = ReplacePortZero(app, appPort ?? 0);
                app = DropSamePortLoopback(app, added);
            }
        }

        List<Endpoint> all = [.. app.Distinct()];
        if (added is not null)
        {
            all.Add(added);
        }

        if (input.ManagedListener)
        {
            all.Add(new Endpoint(Role.Managed, Bind.Address, IPAddress.Loopback, 0, false));
        }

        return new Result(all, appPort, reason);
    }

    /// <summary>Whether the bind mode asks for a non-loopback listener.</summary>
    private static bool IsRemote(Input input) =>
        string.Equals(input.Bind, "lan", StringComparison.OrdinalIgnoreCase)
        || string.Equals(input.Bind, "tailnet", StringComparison.OrdinalIgnoreCase)
        || string.Equals(input.Bind, "custom", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(input.Bind, "auto", StringComparison.OrdinalIgnoreCase) && input.InContainer);

    /// <summary>Spec 8.1: the single non-zero configured port, or <see langword="null"/>.</summary>
    private static int? SingleNonZeroPort(List<Endpoint> app)
    {
        List<int> ports = [.. app.Select(static endpoint => endpoint.Port).Where(static port => port != 0).Distinct()];
        return ports.Count == 1 ? ports[0] : null;
    }

    /// <summary>Replaces every port-0 app endpoint with <paramref name="port"/> (a zero <paramref name="port"/> changes nothing).</summary>
    private static List<Endpoint> ReplacePortZero(List<Endpoint> app, int port) =>
        port == 0 ? app : [.. app.Select(endpoint => endpoint.Port == 0 ? endpoint with { Port = port } : endpoint)];

    /// <summary>An any-address bind makes a loopback endpoint on the same port redundant (and a second bind on it would fail).</summary>
    private static List<Endpoint> DropSamePortLoopback(List<Endpoint> app, Endpoint added) =>
        added.Bind != Bind.AnyAddress
            ? app
            : [.. app.Where(endpoint => !(endpoint.Port == added.Port && (endpoint.Bind == Bind.Localhost || (endpoint.Address is { } address && IPAddress.IsLoopback(address)))))];

    /// <summary>Decides the extra non-loopback listener, or why there is none (the app then stays loopback-only).</summary>
    private static (Endpoint? Endpoint, string? Reason) PlanRemote(Input input, int? appPort, List<Endpoint> app, IProbe probe)
    {
        if (input.AuthMode == GatewayAuthMode.None)
        {
            return (null, "A non-loopback bind needs gateway authentication, and Auth:Mode is none; the app stays loopback-only.");
        }

        if (appPort is not { } port || port == 0)
        {
            return (null, "A non-loopback bind needs one fixed port; set Team:Gateway:Port. The app stays loopback-only.");
        }

        if (!probe.IsPortFree(port))
        {
            return (null, $"Port {port} is already in use; the app stays loopback-only.");
        }

        bool https = app.Any(endpoint => endpoint.Port == port && endpoint.Https);
        if (string.Equals(input.Bind, "tailnet", StringComparison.OrdinalIgnoreCase))
        {
            return probe.TailnetAddress() is { } tailnet
                ? (new Endpoint(Role.App, Bind.Address, tailnet, port, https), null)
                : (null, "No Tailscale address (100.64.0.0/10) was found on this machine; the app stays loopback-only.");
        }

        if (string.Equals(input.Bind, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.TryParse(input.CustomBindHost, out IPAddress? custom)
                ? (new Endpoint(Role.App, Bind.Address, custom, port, https), null)
                : (null, "Team:Gateway:CustomBindHost is not an IP address; the app stays loopback-only.");
        }

        return (new Endpoint(Role.App, Bind.AnyAddress, null, port, https), null);
    }

    /// <summary>Reads <c>urls</c>, else <c>http_ports</c> and <c>https_ports</c>, else the Kestrel default; <see langword="null"/> for a unix or pipe URL.</summary>
    private static List<Endpoint>? ParseUrls(Input input, out string? unavailable)
    {
        unavailable = null;
        List<Endpoint> app = [];
        if (!string.IsNullOrWhiteSpace(input.Urls))
        {
            foreach (string url in input.Urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (url.Contains("://unix:", StringComparison.OrdinalIgnoreCase) || url.Contains("://pipe:", StringComparison.OrdinalIgnoreCase))
                {
                    unavailable = "A unix socket or pipe URL is configured; remote access is unavailable and the app stays loopback-only.";
                    return null;
                }

                app.Add(FromAddress(BindingAddress.Parse(url)));
            }

            return app;
        }

        AddPorts(app, input.HttpPorts, https: false);
        AddPorts(app, input.HttpsPorts, https: true);
        if (app.Count == 0)
        {
            app.Add(new Endpoint(Role.App, Bind.Localhost, null, DefaultPort, false));
        }

        return app;
    }

    /// <summary>The port of Kestrel's built-in default address <c>http://localhost:5000</c>.</summary>
    private const int DefaultPort = 5000;

    private static void AddPorts(List<Endpoint> app, string? ports, bool https)
    {
        foreach (string text in (ports ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int port))
            {
                app.Add(new Endpoint(Role.App, Bind.AnyAddress, null, port, https));
            }
        }
    }

    /// <summary>Maps one binding address to the Kestrel call it needs; <c>ListenLocalhost(0)</c> is never emitted.</summary>
    private static Endpoint FromAddress(BindingAddress address)
    {
        bool https = string.Equals(address.Scheme, "https", StringComparison.OrdinalIgnoreCase);
        string host = address.Host.Trim('[', ']');
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return address.Port == 0
                ? new Endpoint(Role.App, Bind.Address, IPAddress.Loopback, 0, https)
                : new Endpoint(Role.App, Bind.Localhost, null, address.Port, https);
        }

        return IPAddress.TryParse(host, out IPAddress? ip)
            ? new Endpoint(Role.App, Bind.Address, ip, address.Port, https)
            : new Endpoint(Role.App, Bind.AnyAddress, null, address.Port, https);
    }
}

using System.Globalization;
using System.Text.Json;

namespace Agency.Huddle.App.Gateway;

/// <summary><c>tailscale serve status --json</c>: the proxy routes configured on this node (Spec 6.3, E-2, E-14).</summary>
internal sealed class TailscaleServeStatus
{
    private TailscaleServeStatus(IReadOnlyList<Route> routes) => this.Routes = routes;

    /// <summary>One proxy handler.</summary>
    /// <param name="Port">The HTTPS port of the <c>Web</c> key (<c>host:port</c>).</param>
    /// <param name="Path">The handler path (<c>/</c>).</param>
    /// <param name="Target">The proxy target, for example <c>http://127.0.0.1:41233</c>.</param>
    internal sealed record Route(int Port, string Path, string Target);

    /// <summary>Every proxy handler found.</summary>
    public IReadOnlyList<Route> Routes { get; }

    /// <summary>Parses the output; empty, unreadable or unexpected input yields no routes.</summary>
    /// <param name="json">The command output.</param>
    public static TailscaleServeStatus Parse(string json)
    {
        List<Route> routes = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TailscaleServeStatus(routes);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("Web", out JsonElement web)
                && web.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty site in web.EnumerateObject())
                {
                    AddSite(routes, site);
                }
            }
        }
        catch (JsonException)
        {
            routes.Clear();
        }

        return new TailscaleServeStatus(routes);
    }

    /// <summary>The first route on <paramref name="port"/>, or <see langword="null"/>.</summary>
    /// <param name="port">The Tailscale HTTPS port.</param>
    public Route? RouteOn(int port) => this.Routes.FirstOrDefault(route => route.Port == port);

    /// <summary>The routes that proxy to Huddle's app port (host <c>127.0.0.1</c> or <c>localhost</c>), set up by hand.</summary>
    /// <param name="appPort">The app's own port.</param>
    public IReadOnlyList<Route> StrayRoutes(int appPort)
        => [.. this.Routes.Where(route => Uri.TryCreate(route.Target, UriKind.Absolute, out Uri? target)
            && target.Port == appPort
            && (string.Equals(target.Host, "127.0.0.1", StringComparison.Ordinal) || string.Equals(target.Host, "localhost", StringComparison.OrdinalIgnoreCase)))];

    private static void AddSite(List<Route> routes, JsonProperty site)
    {
        int colon = site.Name.LastIndexOf(':');
        if (colon < 0
            || !int.TryParse(site.Name.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int port)
            || site.Value.ValueKind != JsonValueKind.Object
            || !site.Value.TryGetProperty("Handlers", out JsonElement handlers)
            || handlers.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty handler in handlers.EnumerateObject())
        {
            if (handler.Value.ValueKind == JsonValueKind.Object
                && handler.Value.TryGetProperty("Proxy", out JsonElement proxy)
                && proxy.ValueKind == JsonValueKind.String
                && proxy.GetString() is { Length: > 0 } target)
            {
                routes.Add(new Route(port, handler.Name, target));
            }
        }
    }
}

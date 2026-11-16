using System.Net;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.3 / D-7 through the real pipeline: the Tailscale host feed admits a Serve request addressed to the MagicDNS name and port, and withdraws it.</summary>
public sealed class GatewayTailscaleHostFilteringTests
{
    private static async Task<HttpStatusCode> HealthAsync(HttpClient client, string authority, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"http://{authority}/health"));
        using HttpResponseMessage response = await client.SendAsync(request, ct);
        return response.StatusCode;
    }

    /// <summary>A request with <c>Host: desktop-a.tail1234.ts.net:5101</c> is refused until the Tailscale feed adds the name, survives an automatic-hosts refresh, and is refused again once withdrawn.</summary>
    [Fact]
    public async Task ServeRequest_WithTheMagicDnsHostAndPort_PassesHostFilteringOnlyWhileFed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamWebApplicationFactory root = new();
        using Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app = root.WithWebHostBuilder(builder => builder.UseSetting("AllowedHosts", "localhost;127.0.0.1"));
        using HttpClient client = app.CreateClient();
        GatewayDynamicHosts dynamic = app.Services.GetRequiredService<GatewayDynamicHosts>();
        const string Authority = "desktop-a.tail1234.ts.net:5101";

        HttpStatusCode before = await HealthAsync(client, Authority, ct);
        dynamic.SetTailscale(["desktop-a.tail1234.ts.net", Authority]);
        HttpStatusCode fed = await HealthAsync(client, Authority, ct);
        dynamic.Set(["192.168.1.20"]);
        HttpStatusCode afterAutomaticRefresh = await HealthAsync(client, Authority, ct);
        dynamic.SetTailscale([]);
        HttpStatusCode withdrawn = await HealthAsync(client, Authority, ct);

        Assert.Equal(HttpStatusCode.BadRequest, before);
        Assert.Equal(HttpStatusCode.OK, fed);
        Assert.Equal(HttpStatusCode.OK, afterAutomaticRefresh);
        Assert.Equal(HttpStatusCode.BadRequest, withdrawn);
    }
}

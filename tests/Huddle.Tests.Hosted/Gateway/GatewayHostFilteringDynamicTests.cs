using System.Net;
using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Host filtering with a dynamic list (Spec 6.4; D-7): the configured <c>AllowedHosts</c> (env-style) stays, the dynamic entries are added by
/// <c>PostConfigure</c>, and a change-token signal is observed by the host-filtering middleware. Pins the "unverified" claims of D-7.
/// </summary>
public sealed class GatewayHostFilteringDynamicTests
{
    private static async Task<HttpStatusCode> HealthAsync(HttpClient client, string authority, CancellationToken ct)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"http://{authority}/health"));
        using HttpResponseMessage response = await client.SendAsync(request, ct);
        return response.StatusCode;
    }

    /// <summary>A dynamic host is refused until it is set, then accepted (with or without a port) without a restart; the configured hosts stay; clearing it refuses it again.</summary>
    [Fact]
    public async Task DynamicHosts_AreAddedAndRemovedLiveAndConfiguredHostsStay()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamWebApplicationFactory root = new();
        using Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app = root.WithWebHostBuilder(builder => builder.UseSetting("AllowedHosts", "localhost;127.0.0.1"));
        using HttpClient client = app.CreateClient();
        GatewayDynamicHosts dynamic = app.Services.GetRequiredService<GatewayDynamicHosts>();

        HttpStatusCode before = await HealthAsync(client, "desk.tail1.ts.net", ct);
        dynamic.Set(["desk.tail1.ts.net"]);
        HttpStatusCode added = await HealthAsync(client, "desk.tail1.ts.net", ct);
        HttpStatusCode addedWithPort = await HealthAsync(client, "desk.tail1.ts.net:5101", ct);
        HttpStatusCode configured = await HealthAsync(client, "localhost", ct);
        HttpStatusCode other = await HealthAsync(client, "evil.example", ct);
        dynamic.Set([]);
        HttpStatusCode removed = await HealthAsync(client, "desk.tail1.ts.net", ct);

        Assert.Equal(HttpStatusCode.BadRequest, before);
        Assert.Equal(HttpStatusCode.OK, added);
        Assert.Equal(HttpStatusCode.OK, addedWithPort);
        Assert.Equal(HttpStatusCode.OK, configured);
        Assert.Equal(HttpStatusCode.BadRequest, other);
        Assert.Equal(HttpStatusCode.BadRequest, removed);
    }
}

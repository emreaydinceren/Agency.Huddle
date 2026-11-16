using System.Net.Http.Headers;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Manual-run fix (bug 7): <c>GET /_gateway/devices</c> says which devices are revoked.</summary>
public sealed class GatewayAdminEndpointsRevokedTests
{
    /// <summary>A revoked device is listed with <c>revoked: true</c> and an active one with <c>revoked: false</c>.</summary>
    [Fact]
    public async Task List_RevokedDevice_CarriesRevokedTrue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        DeviceRegistry registry = host.Services.GetRequiredService<DeviceRegistry>();
        TestDevice active = new();
        TestDevice revoked = new();
        foreach (TestDevice device in new[] { active, revoked })
        {
            _ = registry.Pair(new DeviceRegistry.Registration(
                device.DeviceId, device.PublicKey, "Phone", [GatewayScopes.Read], "approval", null, "127.0.0.1", "ua/1"));
        }

        Assert.True(registry.Revoke(revoked.DeviceId));
        HttpRequestMessage request = GatewayGateHost.Request(HttpMethod.Get, "/_gateway/devices");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GatewayGateHost.Token);

        using HttpResponseMessage response = await host.Client.SendAsync(request, ct);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Dictionary<string, bool> flags = json.RootElement.GetProperty("devices").EnumerateArray()
            .ToDictionary(device => device.GetProperty("deviceId").GetString() ?? string.Empty, device => device.GetProperty("revoked").GetBoolean(), StringComparer.Ordinal);
        Assert.False(flags[active.DeviceId]);
        Assert.True(flags[revoked.DeviceId]);
    }
}

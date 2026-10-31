using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Spec 6.13, C-13: the <c>/_gateway/*</c> verbs on the real pipeline: loopback only, the gateway token as a bearer, acting as a host admin.</summary>
public sealed class GatewayAdminEndpointsTests
{
    private static HttpRequestMessage Call(HttpMethod method, string path, string? bearer = GatewayGateHost.Token, string? ip = null, string? json = null)
    {
        HttpRequestMessage request = GatewayGateHost.Request(method, path, ip: ip);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static PairingRequest OpenRequest(GatewayGateHost host, TestDevice device, params string[] scopes)
    {
        PairingService.Candidate candidate = new(
            device.DeviceId, device.PublicKey, scopes.Length == 0 ? [GatewayScopes.Read, GatewayScopes.Write] : scopes, "Work Laptop", "203.0.113.9", "ua/1", "Lan", null);
        PairingRequest? request = host.Services.GetRequiredService<PairingService>().Open(candidate);
        Assert.NotNull(request);
        return request;
    }

    private static string PairDevice(GatewayGateHost host, TestDevice device)
    {
        string? token = host.Services.GetRequiredService<DeviceRegistry>().Pair(new DeviceRegistry.Registration(
            device.DeviceId, device.PublicKey, "Pixel Tablet", [GatewayScopes.Read, GatewayScopes.Write], "approval", null, "127.0.0.1", "ua/1"));
        Assert.NotNull(token);
        return token;
    }

    /// <summary><c>GET /_gateway/devices</c> returns the paired devices and the pending requests as JSON.</summary>
    [Fact]
    public async Task List_Bearer_ReturnsDevicesAndPendingRequests()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice paired = new();
        TestDevice asking = new();
        _ = PairDevice(host, paired);
        PairingRequest pending = OpenRequest(host, asking);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Get, "/_gateway/devices"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        JsonElement device = Assert.Single(json.RootElement.GetProperty("devices").EnumerateArray());
        Assert.Equal(paired.DeviceId, device.GetProperty("deviceId").GetString());
        Assert.Equal("Pixel Tablet", device.GetProperty("name").GetString());
        JsonElement request = Assert.Single(json.RootElement.GetProperty("requests").EnumerateArray());
        Assert.Equal(pending.RequestId, request.GetProperty("requestId").GetString());
        Assert.Equal(asking.DeviceId, request.GetProperty("deviceId").GetString());
    }

    /// <summary>Approving with no body pairs the device with the scopes it asked for.</summary>
    [Fact]
    public async Task Approve_NoScope_PairsWithTheRequestedScopes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        PairingRequest pending = OpenRequest(host, device);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/requests/{pending.RequestId}/approve"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DeviceRecord? record = host.Services.GetRequiredService<DeviceRegistry>().Find(device.DeviceId);
        Assert.NotNull(record);
        Assert.Equal([GatewayScopes.Read, GatewayScopes.Write], record.Scopes);
    }

    /// <summary>A <c>scope</c> list in the body narrows what is granted.</summary>
    [Fact]
    public async Task Approve_WithScopeList_GrantsOnlyThose()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        PairingRequest pending = OpenRequest(host, device);

        using HttpResponseMessage response = await host.Client.SendAsync(
            Call(HttpMethod.Post, $"/_gateway/devices/requests/{pending.RequestId}/approve", json: """{"scope":["operator.read"]}"""), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DeviceRecord? record = host.Services.GetRequiredService<DeviceRegistry>().Find(device.DeviceId);
        Assert.NotNull(record);
        Assert.Equal([GatewayScopes.Read], record.Scopes);
    }

    /// <summary>The caller is a host admin (C-13): it can approve a request for the admin scope.</summary>
    [Fact]
    public async Task Approve_AdminRequest_ActsAsHostAdmin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        PairingRequest pending = OpenRequest(host, device, GatewayScopes.Admin);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/requests/{pending.RequestId}/approve"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DeviceRecord? record = host.Services.GetRequiredService<DeviceRegistry>().Find(device.DeviceId);
        Assert.NotNull(record);
        Assert.Contains(GatewayScopes.Admin, record.Scopes);
    }

    /// <summary>An unknown request id is 404 and pairs nothing.</summary>
    [Fact]
    public async Task Approve_UnknownRequest_Returns404()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, "/_gateway/devices/requests/nope/approve"), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(host.Services.GetRequiredService<DeviceRegistry>().List());
    }

    /// <summary>Rejecting closes the request and pairs nothing.</summary>
    [Fact]
    public async Task Reject_ClosesTheRequestWithoutPairing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        PairingRequest pending = OpenRequest(host, device);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/requests/{pending.RequestId}/reject"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(host.Services.GetRequiredService<PairingService>().Pending());
        Assert.Null(host.Services.GetRequiredService<DeviceRegistry>().Find(device.DeviceId));
    }

    /// <summary>Revoke removes the device's token but keeps it listed.</summary>
    [Fact]
    public async Task Revoke_RemovesTheTokenButKeepsTheDevice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        _ = PairDevice(host, device);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/{device.DeviceId}/revoke"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        DeviceRecord? record = host.Services.GetRequiredService<DeviceRegistry>().Find(device.DeviceId);
        Assert.NotNull(record);
        Assert.False(record.HasToken);
    }

    /// <summary>Remove deletes the device.</summary>
    [Fact]
    public async Task Remove_DeletesTheDevice()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        _ = PairDevice(host, device);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/{device.DeviceId}/remove"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(host.Services.GetRequiredService<DeviceRegistry>().Find(device.DeviceId));
    }

    /// <summary>Rotate issues a new device token: the old one stops resolving.</summary>
    [Fact]
    public async Task Rotate_InvalidatesTheOldDeviceToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        string oldToken = PairDevice(host, device);

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/{device.DeviceId}/rotate"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(host.Services.GetRequiredService<DeviceRegistry>().FindByToken(oldToken));
    }

    /// <summary>A device verb on an unknown device is 404.</summary>
    /// <param name="verb">The verb.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("remove")]
    [InlineData("rotate")]
    public async Task DeviceVerb_UnknownDevice_Returns404(string verb)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/nobody/{verb}"), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A missing or wrong bearer is 401 and changes nothing.</summary>
    /// <param name="bearer">The bearer value, or <see langword="null"/> for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("wrong-token")]
    public async Task Request_MissingOrWrongBearer_Returns401(string? bearer)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        PairingRequest pending = OpenRequest(host, new TestDevice());

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Post, $"/_gateway/devices/requests/{pending.RequestId}/approve", bearer), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(host.Services.GetRequiredService<PairingService>().Pending());
    }

    /// <summary>A wrong bearer is counted by the rate limiter (scope <c>shared-secret</c>); a right one is not.</summary>
    [Fact]
    public async Task Request_WrongBearer_IsCountedByTheRateLimiter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        AuthRateLimiter limiter = host.Services.GetRequiredService<AuthRateLimiter>();
        int before = limiter.Count;

        using HttpResponseMessage good = await host.Client.SendAsync(Call(HttpMethod.Get, "/_gateway/devices"), ct);
        Assert.Equal(before, limiter.Count);
        using HttpResponseMessage bad = await host.Client.SendAsync(Call(HttpMethod.Get, "/_gateway/devices", bearer: "wrong-token"), ct);

        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(before + 1, limiter.Count);
    }

    /// <summary>A non-loopback caller is 403 even with the right bearer.</summary>
    [Fact]
    public async Task Request_NonLoopback_Returns403()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        using HttpResponseMessage response = await host.Client.SendAsync(Call(HttpMethod.Get, "/_gateway/devices", ip: GatewayGateHost.LanAddress), ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

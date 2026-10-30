using System.Net;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>Review fixes R-6, R-9 and R-13 through the real pipeline: challenge flooding, the machine name on <c>/auth/status</c>, and bare-name host detection.</summary>
public sealed class GatewayReviewFixHostedTests
{
    private static async Task<JsonElement> StatusAsync(GatewayGateHost host, string? cookie, string? ip, CancellationToken ct)
    {
        using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/auth/status", cookie, ip), ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.Clone();
    }

    /// <summary>R-9: a network caller without a session is not told the machine name.</summary>
    [Fact]
    public async Task Status_NetworkCallerWithoutSession_HidesMachineName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        JsonElement status = await StatusAsync(host, cookie: null, GatewayGateHost.LanAddress, ct);

        Assert.True(!status.TryGetProperty("host", out JsonElement name) || name.ValueKind == JsonValueKind.Null);
    }

    /// <summary>R-9: loopback sees the machine name.</summary>
    [Fact]
    public async Task Status_Loopback_ShowsMachineName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();

        JsonElement status = await StatusAsync(host, cookie: null, ip: null, ct);

        Assert.Equal(Environment.MachineName, status.GetProperty("host").GetString());
    }

    /// <summary>R-9: a network caller with a live session sees the machine name.</summary>
    [Fact]
    public async Task Status_NetworkCallerWithSession_ShowsMachineName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        string cookie = await host.ConnectAsync(new TestDevice(), ct);

        JsonElement status = await StatusAsync(host, cookie, GatewayGateHost.LanAddress, ct);

        Assert.True(status.GetProperty("authenticated").GetBoolean());
        Assert.Equal(Environment.MachineName, status.GetProperty("host").GetString());
    }

    /// <summary>R-6: the 61st challenge request in a minute from one network address is a 429.</summary>
    [Fact]
    public async Task Challenge_SixtyFirstFromOneAddress_IsTooManyRequests()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        HttpStatusCode last = HttpStatusCode.OK;
        for (int index = 0; index <= ChallengeRateLimiter.PerMinute; index++)
        {
            using HttpResponseMessage response = await host.Client.SendAsync(GatewayGateHost.Request(HttpMethod.Get, "/auth/challenge", ip: GatewayGateHost.LanAddress), ct);
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    /// <summary>R-13: the gate records an admitted bare-name Host in the runtime view, and the Security check item fires from it; a dotted or loopback Host does not.</summary>
    [Fact]
    public async Task Gate_BareNameHost_ReachesTheSecurityCheck()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        IGatewayRuntimeView view = host.Services.GetRequiredService<IGatewayRuntimeView>();
        host.Services.GetRequiredService<GatewayDynamicHosts>().Set(["huddlebox", "192.168.1.5", "desktop-a.tail1234.ts.net"]);
        foreach (string name in new[] { "localhost", "192.168.1.5", "desktop-a.tail1234.ts.net", "[::1]" })
        {
            using HttpResponseMessage plain = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{name}/auth/status")), ct);
            Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        }

        bool before = view.BareNameHostInUse;
        using HttpResponseMessage bare = await host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri("https://huddlebox/auth/status")), ct);
        IReadOnlyList<GatewaySecurityCheck.Item> items = GatewaySecurityCheck.Evaluate(new GatewaySecurityCheck.Input(
            new GatewayOptions { Bind = "lan" },
            TokenLength: 48,
            PasswordLength: null,
            TailscaleState.Missing,
            view.BareNameHostInUse,
            []));

        Assert.Equal(HttpStatusCode.OK, bare.StatusCode);
        Assert.False(before);
        Assert.True(view.BareNameHostInUse);
        Assert.Equal(["gateway.control_ui.allowed_origins_required"], items.Select(item => item.Id).Where(id => id.StartsWith("gateway.control_ui", StringComparison.Ordinal)));
    }
}

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 6.8, C-5, C-6, C-8: revocation closes the device's open <c>/_blazor</c> socket. Drives the raw Blazor protocol on <c>TestServer</c>:
/// negotiate, open the WebSocket with the session cookie, send the <c>blazorpack</c> handshake, then change the device or the clock.
/// The spike (REPORT: CONFIRMED) showed a global hub filter reaches <c>ComponentHub</c> and <c>Abort()</c> closes the socket.
/// </summary>
public sealed class GatewayConnectionGuardTests
{
    private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(2);

    /// <summary>A SignalR MessagePack frame: an invocation of <c>OnRenderCompleted(1, null)</c>, which reaches the hub filter before Blazor handles it.</summary>
    private static readonly byte[] HubInvocation =
    [
        0x1A, 0x96, 0x01, 0x80, 0xC0, 0xB1,
        .. "OnRenderCompleted"u8,
        0x92, 0x01, 0xC0, 0x90,
    ];

    /// <summary>Negotiates, opens the circuit's WebSocket with <paramref name="cookie"/> and completes the hub handshake.</summary>
    private static async Task<WebSocket> OpenAsync(GatewayGateHost host, string cookie, CancellationToken ct)
    {
        using HttpRequestMessage negotiate = GatewayGateHost.Request(HttpMethod.Post, "/_blazor/negotiate?negotiateVersion=1", cookie);
        using HttpResponseMessage response = await host.Client.SendAsync(negotiate, ct);
        response.EnsureSuccessStatusCode();
        string token = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("connectionToken").GetString() ?? string.Empty;

        WebSocketClient sockets = host.Factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = request => request.Headers.Cookie = cookie;
        WebSocket socket = await sockets.ConnectAsync(new Uri("ws://localhost/_blazor?id=" + token), ct);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"blazorpack\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, ct);
        byte[] buffer = new byte[256];
        WebSocketReceiveResult reply = await socket.ReceiveAsync(buffer, ct);
        Assert.Equal("{}\u001e", Encoding.UTF8.GetString(buffer, 0, reply.Count));
        return socket;
    }

    /// <summary>Waits until the guard has recorded the device's connection, which can trail the handshake reply.</summary>
    private static async Task WaitForConnectionAsync(GatewayGateHost host, string deviceId, CancellationToken ct)
    {
        GatewayConnectionGuard guard = host.Services.GetRequiredService<GatewayConnectionGuard>();
        for (int attempt = 0; attempt < 40 && guard.ConnectionCount(deviceId) == 0; attempt++)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(guard.ConnectionCount(deviceId) > 0);
    }

    /// <summary>Whether the server closes the socket within <see cref="CloseBudget"/>: a close frame, or the connection ending.</summary>
    private static async Task<bool> ClosesWithinAsync(WebSocket socket, CancellationToken ct)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CloseBudget);
        byte[] buffer = new byte[1024];
        try
        {
            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return true;
                }
            }
        }
        catch (WebSocketException)
        {
            return true; // The connection ended without a close frame: it is closed.
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Revoking, removing or rotating a device closes its open circuit socket within two seconds.</summary>
    /// <param name="change">What happens to the device.</param>
    [Theory]
    [InlineData("revoke")]
    [InlineData("remove")]
    [InlineData("rotate")]
    public async Task DeviceChange_ClosesItsBlazorSocketWithinTwoSeconds(string change)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice device = new();
        string cookie = await host.ConnectAsync(device, ct);
        using WebSocket socket = await OpenAsync(host, cookie, ct);
        await WaitForConnectionAsync(host, device.DeviceId, ct);
        DeviceRegistry devices = host.Services.GetRequiredService<DeviceRegistry>();

        _ = change switch
        {
            "revoke" => devices.Revoke(device.DeviceId),
            "remove" => devices.Remove(device.DeviceId),
            _ => devices.Rotate(device.DeviceId) is not null,
        };

        Assert.True(await ClosesWithinAsync(socket, ct));
    }

    /// <summary>Revoking one device leaves another device's socket open.</summary>
    [Fact]
    public async Task Revoke_OtherDevice_LeavesTheSocketOpen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GatewayGateHost host = new();
        TestDevice kept = new();
        TestDevice revoked = new();
        string keptCookie = await host.ConnectAsync(kept, ct);
        _ = await host.ConnectAsync(revoked, ct);
        using WebSocket socket = await OpenAsync(host, keptCookie, ct);
        await WaitForConnectionAsync(host, kept.DeviceId, ct);

        _ = host.Services.GetRequiredService<DeviceRegistry>().Revoke(revoked.DeviceId);

        Assert.False(await ClosesWithinAsync(socket, ct));
    }

    /// <summary>
    /// C-5: a session that expires while its socket is open is not dropped by a timer; the next hub invocation re-checks it and aborts.
    /// The registry runs on a fake clock moved past the 12-hour lifetime.
    /// </summary>
    [Fact]
    public async Task ExpiredSession_NextHubInvocation_ClosesTheSocket()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new(DateTimeOffset.UtcNow);
        using GatewayGateHost host = new(configure: services => services.AddSingleton(provider => new SessionRegistry(provider.GetRequiredService<DeviceRegistry>(), clock)));
        TestDevice device = new();
        string cookie = await host.ConnectAsync(device, ct);
        using WebSocket socket = await OpenAsync(host, cookie, ct);
        await WaitForConnectionAsync(host, device.DeviceId, ct);

        clock.Advance(SessionRegistry.Lifetime + TimeSpan.FromMinutes(1));
        await socket.SendAsync(HubInvocation, WebSocketMessageType.Binary, true, ct);

        Assert.True(await ClosesWithinAsync(socket, ct));
    }
}

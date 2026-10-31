using System.Net;
using Agency.Huddle.App.Gateway;
using Microsoft.AspNetCore.Http;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>The recorded listeners (Spec 6.2; corrections D-2 and D-5): finding the managed listener by its local endpoint, and the app URL.</summary>
public sealed class GatewayListenerTests
{
    private static readonly IPEndPoint ManagedEndpoint = new(IPAddress.Loopback, 45678);

    private static GatewayListener WithManaged()
    {
        GatewayListener listener = new();
        listener.RecordManaged(ManagedEndpoint);
        return listener;
    }

    private static ConnectionInfo Connection(IPAddress? address, int port)
    {
        DefaultHttpContext http = new();
        http.Connection.LocalIpAddress = address;
        http.Connection.LocalPort = port;
        return http.Connection;
    }

    /// <summary>A connection whose local endpoint equals the recorded managed endpoint is Managed, also in its IPv4-mapped form.</summary>
    [Fact]
    public void KindOf_MatchingLocalEndpoint_IsManaged()
    {
        GatewayListener listener = WithManaged();

        Assert.Equal(IngressListenerKind.Managed, listener.KindOf(Connection(IPAddress.Loopback, 45678)));
        Assert.Equal(IngressListenerKind.Managed, listener.KindOf(Connection(IPAddress.Loopback.MapToIPv6(), 45678)));
    }

    /// <summary>A different port, a different address on the same port, no local address (a <c>TestServer</c>), or no recorded endpoint is App.</summary>
    [Fact]
    public void KindOf_AnythingElse_IsApp()
    {
        GatewayListener listener = WithManaged();

        Assert.Equal(IngressListenerKind.App, listener.KindOf(Connection(IPAddress.Loopback, 5101)));
        Assert.Equal(IngressListenerKind.App, listener.KindOf(Connection(IPAddress.Parse("192.168.1.5"), 45678)));
        Assert.Equal(IngressListenerKind.App, listener.KindOf(Connection(null, 0)));
        Assert.Equal(IngressListenerKind.App, new GatewayListener().KindOf(Connection(IPAddress.Loopback, 45678)));
    }

    /// <summary>The app URL maps an any-address to <c>127.0.0.1</c> and leaves other hosts alone.</summary>
    [Theory]
    [InlineData("http://0.0.0.0:5101", "http://127.0.0.1:5101")]
    [InlineData("http://[::]:5101", "http://127.0.0.1:5101")]
    [InlineData("http://localhost:5101", "http://localhost:5101")]
    [InlineData("http://127.0.0.1:5300", "http://127.0.0.1:5300")]
    public void RecordApp_AnyAddressMapsToLoopback(string reported, string expected)
    {
        GatewayListener listener = new();

        listener.RecordApp(reported);

        Assert.Equal(expected, listener.AppUrl);
    }
}

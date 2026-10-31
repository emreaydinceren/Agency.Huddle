using System.Net;
using Agency.Huddle.App.Desktop;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins the authentication of <see cref="DesktopStopEndpoint"/>: loopback only, and only with the exact stop token.</summary>
public sealed class DesktopStopEndpointTests
{
    private const string HeaderName = "X-Huddle-Token";

    /// <summary>Builds a request context with the given remote address and, when any, the token header values.</summary>
    private static DefaultHttpContext NewContext(IPAddress? remote, StringValues token)
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = remote;
        if (token.Count > 0)
        {
            context.Request.Headers[HeaderName] = token;
        }

        return context;
    }

    /// <summary>Reads the status code of a result without executing it.</summary>
    private static int StatusOf(IResult result)
    {
        IStatusCodeHttpResult status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        return status.StatusCode ?? 0;
    }

    /// <summary>The route constant is the documented path.</summary>
    [Fact]
    public void Route_IsDesktopStop()
    {
        Assert.Equal("/_desktop/stop", DesktopStopEndpoint.Route);
    }

    /// <summary>The correct token from loopback is accepted with 202 and asks the host to stop exactly once.</summary>
    [Fact]
    public void HandleAsync_CorrectTokenFromLoopback_Returns202AndStopsOnce()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, session.StopToken), session, shutdown);

        Assert.Equal(202, StatusOf(result));
        Assert.Equal(1, shutdown.StopCalls);
    }

    /// <summary>The IPv6 loopback address is also loopback.</summary>
    [Fact]
    public void HandleAsync_CorrectTokenFromIpv6Loopback_Returns202AndStopsOnce()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.IPv6Loopback, session.StopToken), session, shutdown);

        Assert.Equal(202, StatusOf(result));
        Assert.Equal(1, shutdown.StopCalls);
    }

    /// <summary>A wrong token is refused with 401 and does not stop the host.</summary>
    [Fact]
    public void HandleAsync_WrongToken_Returns401AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, "not-the-token"), session, shutdown);

        Assert.Equal(401, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>A request with no token header is refused with 401 and does not stop the host.</summary>
    [Fact]
    public void HandleAsync_NoHeader_Returns401AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, StringValues.Empty), session, shutdown);

        Assert.Equal(401, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>The right token from a non-loopback address is refused with 403 and does not stop the host.</summary>
    [Fact]
    public void HandleAsync_CorrectTokenFromRemoteAddress_Returns403AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Parse("192.0.2.7"), session.StopToken), session, shutdown);

        Assert.Equal(403, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>A non-loopback caller with no token still gets 403, not 401: the address check comes first.</summary>
    [Fact]
    public void HandleAsync_NoHeaderFromRemoteAddress_Returns403()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Parse("192.0.2.7"), StringValues.Empty), session, shutdown);

        Assert.Equal(403, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>A null remote address (the in-memory test server) is allowed when the token is right.</summary>
    [Fact]
    public void HandleAsync_CorrectTokenWithNullRemote_Returns202AndStopsOnce()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(remote: null, session.StopToken), session, shutdown);

        Assert.Equal(202, StatusOf(result));
        Assert.Equal(1, shutdown.StopCalls);
    }

    /// <summary>A token that is only a prefix of the real one is refused with 401.</summary>
    [Fact]
    public void HandleAsync_TokenIsPrefixOfRealOne_Returns401AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();
        string prefix = session.StopToken[..^1];

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, prefix), session, shutdown);

        Assert.Equal(401, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>A real token with extra characters appended is refused with 401.</summary>
    [Fact]
    public void HandleAsync_TokenWithSuffix_Returns401AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, session.StopToken + "x"), session, shutdown);

        Assert.Equal(401, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>An empty header value is refused with 401.</summary>
    [Fact]
    public void HandleAsync_EmptyHeaderValue_Returns401AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, new StringValues(string.Empty)), session, shutdown);

        Assert.Equal(401, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>Two header values are refused with 401 even when one of them is the real token.</summary>
    [Fact]
    public void HandleAsync_TwoHeaderValuesIncludingRealToken_Returns401AndDoesNotStop()
    {
        DesktopSession session = new();
        FakeDesktopShutdown shutdown = new();

        IResult result = DesktopStopEndpoint.HandleAsync(
            NewContext(IPAddress.Loopback, new StringValues([session.StopToken, "other"])), session, shutdown);

        Assert.Equal(401, StatusOf(result));
        Assert.Equal(0, shutdown.StopCalls);
    }
}

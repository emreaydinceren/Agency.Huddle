using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 6.3 and E-2 / E-14: <c>tailscale serve status --json</c> shows who owns a Tailscale port and which hand-made routes point at Huddle's app port.
/// The shape is Tailscale's ServeConfig (<c>Web</c> keyed <c>host:port</c>, <c>Handlers</c> by path); the parser must tolerate anything else.
/// </summary>
public sealed class TailscaleServeStatusTests
{
    /// <summary>The route Huddle's claim creates is found by its Tailscale port, with its proxy target.</summary>
    [Fact]
    public void RouteOn_FindsTheRouteByTailscalePort()
    {
        TailscaleServeStatus status = TailscaleServeStatus.Parse(TailscaleSamples.ServeClaimed);

        TailscaleServeStatus.Route? route = status.RouteOn(5101);

        Assert.NotNull(route);
        Assert.Equal(5101, route.Port);
        Assert.Equal("/", route.Path);
        Assert.Equal("http://127.0.0.1:41233", route.Target);
        Assert.Null(status.RouteOn(443));
    }

    /// <summary>Somebody else's route on the port shows another target than the managed listener.</summary>
    [Fact]
    public void RouteOn_ForeignRoute_ShowsItsOwnTarget()
    {
        TailscaleServeStatus status = TailscaleServeStatus.Parse(TailscaleSamples.ServeForeignAndStray);

        Assert.Equal("http://127.0.0.1:3000", status.RouteOn(5101)?.Target);
    }

    /// <summary>A route that proxies to Huddle's app port is stray, whether it says <c>localhost</c> or <c>127.0.0.1</c>; a non-proxy handler and other ports are not.</summary>
    [Fact]
    public void StrayRoutes_ListsHandMadeRoutesToTheAppPort()
    {
        TailscaleServeStatus status = TailscaleServeStatus.Parse(TailscaleSamples.ServeForeignAndStray);

        IReadOnlyList<TailscaleServeStatus.Route> strays = status.StrayRoutes(5101);

        TailscaleServeStatus.Route stray = Assert.Single(strays);
        Assert.Equal(443, stray.Port);
        Assert.Equal("http://localhost:5101", stray.Target);
        Assert.Empty(status.StrayRoutes(5102));
    }

    /// <summary>The route Huddle itself made (to the managed port) is never stray.</summary>
    [Fact]
    public void StrayRoutes_HuddlesOwnClaim_IsNotStray()
    {
        TailscaleServeStatus status = TailscaleServeStatus.Parse(TailscaleSamples.ServeClaimed);

        Assert.Empty(status.StrayRoutes(5101));
    }

    /// <summary>Empty, unreadable or unexpected output means no routes and no exception.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("No serve config")]
    [InlineData("[1,2]")]
    [InlineData("""{"Web":{"host:notaport":{"Handlers":{"/":{"Proxy":"http://127.0.0.1:1"}}}}}""")]
    public void Parse_EmptyOrUnexpected_HasNoRoutes(string json)
    {
        TailscaleServeStatus status = TailscaleServeStatus.Parse(json);

        Assert.Empty(status.Routes);
        Assert.Null(status.RouteOn(443));
    }
}

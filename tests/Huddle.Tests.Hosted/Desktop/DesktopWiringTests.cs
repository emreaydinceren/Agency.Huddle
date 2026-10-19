using System.Net;
using Agency.Huddle.App.Desktop;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>
/// Pins that desktop mode is invisible when <c>Team:Desktop:Enabled</c> is off and works end to end
/// in-process when it is on, by driving the real <c>Program</c> through <see cref="TeamWebApplicationFactory"/>.
/// </summary>
public sealed class DesktopWiringTests
{
    /// <summary>Applies the two settings that switch desktop mode on without opening a browser.</summary>
    private static void EnableDesktop(IWebHostBuilder builder)
    {
        builder.UseSetting("Team:Desktop:Enabled", "true");
        builder.UseSetting("Team:Desktop:OpenBrowser", "false");
    }

    /// <summary>Builds a POST to the stop route, with the token header only when one is given.</summary>
    private static HttpRequestMessage StopRequest(string? token)
    {
        HttpRequestMessage request = new(HttpMethod.Post, DesktopStopEndpoint.Route);
        if (token is not null)
        {
            request.Headers.Add(DesktopStopEndpoint.TokenHeader, token);
        }

        return request;
    }

    /// <summary>
    /// With the default settings the stop route is not mapped (the Razor catch-all answers a POST there
    /// with 405, never the endpoint's 202 or 401), no session exists and no instance lock is taken.
    /// </summary>
    [Fact]
    public async Task DefaultHost_StopRoute_IsNotMappedAndTakesNoLock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.SendAsync(StopRequest("anything"), ct);

        Assert.NotEqual(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(factory.Services.GetService<DesktopSession>());
        Assert.False(File.Exists(Path.Combine(factory.DataDirPath, "instance.lock")));
    }

    /// <summary>An enabled host refuses a stop request that carries no token, and does not stop.</summary>
    [Fact]
    public async Task EnabledHost_StopWithoutToken_Returns401AndDoesNotStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeDesktopShutdown shutdown = new();
        using TeamWebApplicationFactory factory = new();
        using WebApplicationFactory<Program> enabled = factory.WithWebHostBuilder(builder =>
        {
            EnableDesktop(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IDesktopShutdown>(shutdown));
        });
        using HttpClient client = enabled.CreateClient();

        using HttpResponseMessage response = await client.SendAsync(StopRequest(null), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>An enabled host refuses a stop request with the wrong token, and does not stop.</summary>
    [Fact]
    public async Task EnabledHost_StopWithWrongToken_Returns401AndDoesNotStop()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeDesktopShutdown shutdown = new();
        using TeamWebApplicationFactory factory = new();
        using WebApplicationFactory<Program> enabled = factory.WithWebHostBuilder(builder =>
        {
            EnableDesktop(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IDesktopShutdown>(shutdown));
        });
        using HttpClient client = enabled.CreateClient();

        using HttpResponseMessage response = await client.SendAsync(StopRequest("not-the-token"), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>An enabled host accepts the session's own token with 202 and asks the shutdown seam to stop once.</summary>
    [Fact]
    public async Task EnabledHost_StopWithSessionToken_Returns202AndStopsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeDesktopShutdown shutdown = new();
        using TeamWebApplicationFactory factory = new();
        using WebApplicationFactory<Program> enabled = factory.WithWebHostBuilder(builder =>
        {
            EnableDesktop(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IDesktopShutdown>(shutdown));
        });
        using HttpClient client = enabled.CreateClient();
        string token = enabled.Services.GetRequiredService<DesktopSession>().StopToken;

        using HttpResponseMessage response = await client.SendAsync(StopRequest(token), ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, shutdown.StopCalls);
    }

    /// <summary>
    /// The stop route accepts POST only: a GET, even with the right token, never stops the host and is not
    /// a success (the Razor catch-all answers it with 404 rather than the 405 the plan assumed).
    /// </summary>
    [Fact]
    public async Task EnabledHost_GetStopRoute_DoesNotStopAndIsNotSuccess()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeDesktopShutdown shutdown = new();
        using TeamWebApplicationFactory factory = new();
        using WebApplicationFactory<Program> enabled = factory.WithWebHostBuilder(builder =>
        {
            EnableDesktop(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IDesktopShutdown>(shutdown));
        });
        using HttpClient client = enabled.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, DesktopStopEndpoint.Route);
        request.Headers.Add(DesktopStopEndpoint.TokenHeader, enabled.Services.GetRequiredService<DesktopSession>().StopToken);

        using HttpResponseMessage response = await client.SendAsync(request, ct);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(0, shutdown.StopCalls);
    }

    /// <summary>Turning desktop mode on leaves the liveness endpoint answering 200.</summary>
    [Fact]
    public async Task EnabledHost_Health_Returns200()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamWebApplicationFactory factory = new();
        using WebApplicationFactory<Program> enabled = factory.WithWebHostBuilder(EnableDesktop);
        using HttpClient client = enabled.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

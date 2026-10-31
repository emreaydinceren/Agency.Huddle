using Agency.Huddle.App.Cli;
using Agency.Huddle.Tests.Ui;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>C-14: the development console line is printed only when the server reports addresses; a TestServer run stays quiet.</summary>
public sealed class DevConsoleQuietTests
{
    /// <summary>Starting the application on the TestServer writes nothing to the development console writer.</summary>
    [Fact]
    public async Task TestServerRun_WritesNothingToTheDevConsole()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using StringWriter sink = new();
        using TeamWebApplicationFactory root = new();
        using WebApplicationFactory<Program> app = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(new DevConsoleOutput(sink))));
        using HttpClient client = app.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Empty(sink.ToString());
    }

    /// <summary>A configured <c>http_ports</c> (the SDK container sets it) is not a bound address: the TestServer binds nothing, so nothing is announced.</summary>
    [Fact]
    public async Task TestServerRun_WithHttpPortsConfigured_WritesNothingToTheDevConsole()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using StringWriter sink = new();
        using TeamWebApplicationFactory root = new();
        using WebApplicationFactory<Program> app = root.WithWebHostBuilder(builder =>
        {
            _ = builder.UseSetting("http_ports", "8080");
            _ = builder.ConfigureTestServices(services => services.AddSingleton(new DevConsoleOutput(sink)));
        });
        using HttpClient client = app.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Empty(sink.ToString());
    }
}

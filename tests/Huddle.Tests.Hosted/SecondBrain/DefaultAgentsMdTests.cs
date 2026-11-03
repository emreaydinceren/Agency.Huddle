using Agency.Huddle.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins that start-up writes the default Teams/AGENTS.md only when the file is missing (Spec §6.4, D-40).</summary>
public sealed class DefaultAgentsMdTests
{
    private const string ResourceName = "Overlays/second-brain/AGENTS.md";

    /// <summary>With no Teams/AGENTS.md, start-up writes the embedded default byte for byte.</summary>
    [Fact]
    public async Task StartAsync_NoAgentsMd_WritesEmbeddedDefault()
    {
        using TempDataDir dataDir = new();
        Agency.Huddle.App.SecondBrain.DefaultAgentsMdService service = new(dataDir.Options());

        await service.StartAsync(TestContext.Current.CancellationToken);

        byte[] written = await File.ReadAllBytesAsync(AgentsMdPath(dataDir.Path), TestContext.Current.CancellationToken);
        Assert.Equal(EmbeddedDefault(), written);
    }

    /// <summary>An existing Teams/AGENTS.md is left byte-identical, whatever its text.</summary>
    [Fact]
    public async Task StartAsync_ExistingAgentsMd_LeavesFileByteIdentical()
    {
        using TempDataDir dataDir = new();
        byte[] own = "# The Human's own conduct\r\n- Keep it short.\r\n"u8.ToArray();
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Teams"));
        await File.WriteAllBytesAsync(AgentsMdPath(dataDir.Path), own, TestContext.Current.CancellationToken);
        Agency.Huddle.App.SecondBrain.DefaultAgentsMdService service = new(dataDir.Options());

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(own, await File.ReadAllBytesAsync(AgentsMdPath(dataDir.Path), TestContext.Current.CancellationToken));
    }

    /// <summary>The service is registered as a hosted service, so start-up runs it (wiring test, Spec §6.4).</summary>
    [Fact]
    public async Task AddTeamServices_RegistersDefaultAgentsMdService()
    {
        using TempDataDir dataDir = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:DataDir"] = dataDir.Path,
                ["Team:HumanName"] = "You",
                ["Team:DemoAgent:Enabled"] = "false",
                ["Team:Acp:Enabled"] = "false",
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTeamServices(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetServices<IHostedService>(), static service => service is Agency.Huddle.App.SecondBrain.DefaultAgentsMdService);
    }

    /// <summary>The path of Teams/AGENTS.md under a data folder.</summary>
    /// <param name="dataRoot">The data folder.</param>
    /// <returns>The full path of the default conduct file.</returns>
    private static string AgentsMdPath(string dataRoot) => Path.Combine(dataRoot, "Teams", "AGENTS.md");

    /// <summary>Reads the embedded default's raw bytes, the bytes the service must write.</summary>
    /// <returns>The resource's bytes, unchanged.</returns>
    private static byte[] EmbeddedDefault()
    {
        using Stream stream = EmbeddedResources.Open(typeof(Agency.Huddle.App.SecondBrain.DefaultAgentsMdService).Assembly, ResourceName);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

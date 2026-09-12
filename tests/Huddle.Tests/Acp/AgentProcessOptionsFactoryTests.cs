using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

public sealed class AgentProcessOptionsFactoryTests
{
    private const string RelativeAdapterPath =
        "tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js";

    [Fact]
    public void ExplicitArgs_WinOverLocator()
    {
        using var dir = new TempDataDir();
        var adapterPath = CreateFakeAdapter(dir.Path);
        var probeStart = Path.Combine(dir.Path, "src", "Team.App");
        Directory.CreateDirectory(probeStart);

        var options = new AcpOptions { Args = ["explicit-arg.js"] };

        var result = AgentProcessOptionsFactory.TryCreate(options, dir.Path, probeStart);

        Assert.NotNull(result);
        Assert.Equal(["explicit-arg.js"], result.Args);
        Assert.NotEqual(adapterPath, result.Args[0]);
    }

    [Fact]
    public void AdapterPath_UsedWhenArgsAbsent()
    {
        using var dir = new TempDataDir();
        var probeStart = Path.Combine(dir.Path, "src", "Team.App");
        Directory.CreateDirectory(probeStart);
        var options = new AcpOptions { AdapterPath = @"C:\somewhere\index.js" };

        var result = AgentProcessOptionsFactory.TryCreate(options, dir.Path, probeStart);

        Assert.NotNull(result);
        Assert.Equal([@"C:\somewhere\index.js"], result.Args);
    }

    [Fact]
    public void MissingAdapter_ReturnsNull()
    {
        using var dir = new TempDataDir();
        var probeStart = Path.Combine(dir.Path, "src", "Team.App");
        Directory.CreateDirectory(probeStart);
        var options = new AcpOptions();

        var result = AgentProcessOptionsFactory.TryCreate(options, dir.Path, probeStart);

        Assert.Null(result);
    }

    [Fact]
    public void WorkingDirectory_IsTheSandbox()
    {
        using var dir = new TempDataDir();
        var probeStart = Path.Combine(dir.Path, "src", "Team.App");
        Directory.CreateDirectory(probeStart);
        var options = new AcpOptions { AdapterPath = @"C:\somewhere\index.js" };
        var sandboxDir = Path.Combine(dir.Path, "agents", "some-persona");

        var result = AgentProcessOptionsFactory.TryCreate(options, sandboxDir, probeStart);

        Assert.NotNull(result);
        Assert.Equal(sandboxDir, result.WorkingDirectory);
    }

    private static string CreateFakeAdapter(string root)
    {
        var adapterPath = Path.Combine(root, RelativeAdapterPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(adapterPath)!);
        File.WriteAllText(adapterPath, "// adapter");
        return adapterPath;
    }
}
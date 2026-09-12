using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

public sealed class AdapterLocatorTests
{
    private const string RelativeAdapterPath =
        "tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js";

    [Fact]
    public void Locate_FindsAdapterAboveStartDirectory()
    {
        using var dir = new TempDataDir();
        var adapterPath = Path.Combine(dir.Path, RelativeAdapterPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(adapterPath)!);
        File.WriteAllText(adapterPath, "// adapter");

        var startDirectory = Path.Combine(dir.Path, "src", "Team.App");
        Directory.CreateDirectory(startDirectory);

        var located = AdapterLocator.Locate(startDirectory);

        Assert.Equal(adapterPath, located);
    }

    [Fact]
    public void Locate_ReturnsNullWhenAbsent()
    {
        using var dir = new TempDataDir();
        var startDirectory = Path.Combine(dir.Path, "src", "Team.App");
        Directory.CreateDirectory(startDirectory);

        var located = AdapterLocator.Locate(startDirectory);

        Assert.Null(located);
    }
}
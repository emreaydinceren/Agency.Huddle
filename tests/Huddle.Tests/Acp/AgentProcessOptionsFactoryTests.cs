using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Exercises <see cref="AgentProcessOptionsFactory.TryCreate(AdapterProfile, string, string)"/>
/// against an <see cref="AdapterProfile"/> — the retargeted signature from the legacy
/// <c>AcpOptions</c>-shaped one. Pins the Spec §12 E-7 defense: a profile that does not take the
/// <c>mcp__</c> tool-name prefix must never fall through to <see cref="AdapterLocator"/>, which
/// knows only how to find the Node adapter.
/// </summary>
public sealed class AgentProcessOptionsFactoryTests
{
    private const string RelativeAdapterPath =
        "tools/acp/node_modules/@agentclientprotocol/claude-agent-acp/dist/index.js";

    /// <summary>Explicit <see cref="AdapterProfile.Args"/> wins over both AdapterPath and the locator.</summary>
    [Fact]
    public void TryCreate_ArgsNonEmpty_WinsOverAdapterPathAndLocator()
    {
        using TempDataDir dir = new();
        AgentProcessOptionsFactoryTests.CreateFakeAdapter(dir.Path);
        var probeStart = Path.Combine(dir.Path, "src", "Huddle.App");
        Directory.CreateDirectory(probeStart);
        AdapterProfile profile = new(
            Id: "claude",
            DisplayName: "Claude",
            Description: null,
            Command: "node",
            Args: ["explicit-arg.js"],
            AdapterPath: @"C:\somewhere\index.js",
            UsesToolNamePrefix: true);

        var result = AgentProcessOptionsFactory.TryCreate(profile, dir.Path, probeStart);

        Assert.NotNull(result);
        Assert.Equal(["explicit-arg.js"], result.Args);
    }

    /// <summary>With Args absent, <see cref="AdapterProfile.AdapterPath"/> wins over the locator.</summary>
    [Fact]
    public void TryCreate_AdapterPathUsed_WhenArgsAbsent()
    {
        using TempDataDir dir = new();
        var probeStart = Path.Combine(dir.Path, "src", "Huddle.App");
        Directory.CreateDirectory(probeStart);
        AdapterProfile profile = new(
            Id: "claude",
            DisplayName: "Claude",
            Description: null,
            Command: "node",
            Args: null,
            AdapterPath: @"C:\somewhere\index.js",
            UsesToolNamePrefix: true);

        var result = AgentProcessOptionsFactory.TryCreate(profile, dir.Path, probeStart);

        Assert.NotNull(result);
        Assert.Equal([@"C:\somewhere\index.js"], result.Args);
    }

    /// <summary>
    /// With Args and AdapterPath both absent, and the profile taking the <c>mcp__</c> prefix, the
    /// Node adapter locator is consulted and its result is used.
    /// </summary>
    [Fact]
    public void TryCreate_LocatorConsulted_WhenPrefixedAndNoArgsOrAdapterPath()
    {
        using TempDataDir dir = new();
        var adapterPath = AgentProcessOptionsFactoryTests.CreateFakeAdapter(dir.Path);
        var probeStart = Path.Combine(dir.Path, "src", "Huddle.App");
        Directory.CreateDirectory(probeStart);
        AdapterProfile profile = new(
            Id: "claude",
            DisplayName: "Claude",
            Description: null,
            Command: "node",
            Args: null,
            AdapterPath: null,
            UsesToolNamePrefix: true);

        var result = AgentProcessOptionsFactory.TryCreate(profile, dir.Path, probeStart);

        Assert.NotNull(result);
        Assert.Equal([adapterPath], result.Args);
    }

    /// <summary>
    /// With nothing to go on — no Args, no AdapterPath, and the locator finding nothing — the
    /// factory returns null rather than throwing, so a developer who has not run
    /// tools/acp/install.ps1 can still run the app.
    /// </summary>
    [Fact]
    public void TryCreate_NothingConfigured_ReturnsNullNotException()
    {
        using TempDataDir dir = new();
        var probeStart = Path.Combine(dir.Path, "src", "Huddle.App");
        Directory.CreateDirectory(probeStart);
        AdapterProfile profile = new(
            Id: "claude",
            DisplayName: "Claude",
            Description: null,
            Command: "node",
            Args: null,
            AdapterPath: null,
            UsesToolNamePrefix: true);

        var result = AgentProcessOptionsFactory.TryCreate(profile, dir.Path, probeStart);

        Assert.Null(result);
    }

    /// <summary>
    /// Spec §12 E-7: a profile that does not take the <c>mcp__</c> prefix is not the Node adapter,
    /// so it must never fall through to <see cref="AdapterLocator"/> — proved here by pointing
    /// <c>probeStart</c> at a tree that DOES contain the Node adapter's relative path (so the
    /// locator would succeed if it were consulted) and asserting the result is still null.
    /// </summary>
    [Fact]
    public void TryCreate_UnprefixedProfileWithNothingConfigured_ReturnsNullWithoutConsultingLocator()
    {
        using TempDataDir dir = new();
        AgentProcessOptionsFactoryTests.CreateFakeAdapter(dir.Path);
        var probeStart = Path.Combine(dir.Path, "src", "Huddle.App");
        Directory.CreateDirectory(probeStart);
        AdapterProfile profile = new(
            Id: "agency",
            DisplayName: "Agency",
            Description: null,
            Command: @"C:\tools\agency-acp\agency-acp.exe",
            Args: null,
            AdapterPath: null,
            UsesToolNamePrefix: false);

        var result = AgentProcessOptionsFactory.TryCreate(profile, dir.Path, probeStart);

        Assert.Null(result);
    }

    /// <summary>The working directory passed through is the sandbox, never the probe start.</summary>
    [Fact]
    public void TryCreate_WorkingDirectory_IsTheSandbox()
    {
        using TempDataDir dir = new();
        var probeStart = Path.Combine(dir.Path, "src", "Huddle.App");
        Directory.CreateDirectory(probeStart);
        AdapterProfile profile = new(
            Id: "claude",
            DisplayName: "Claude",
            Description: null,
            Command: "node",
            Args: null,
            AdapterPath: @"C:\somewhere\index.js",
            UsesToolNamePrefix: true);
        var sandboxDir = Path.Combine(dir.Path, "agents", "some-persona");

        var result = AgentProcessOptionsFactory.TryCreate(profile, sandboxDir, probeStart);

        Assert.NotNull(result);
        Assert.Equal(sandboxDir, result.WorkingDirectory);
    }

    /// <summary>Writes a placeholder file at the Node adapter's well-known relative path under <paramref name="root"/>.</summary>
    private static string CreateFakeAdapter(string root)
    {
        var adapterPath = Path.Combine(root, RelativeAdapterPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(adapterPath)!);
        File.WriteAllText(adapterPath, "// adapter");
        return adapterPath;
    }
}

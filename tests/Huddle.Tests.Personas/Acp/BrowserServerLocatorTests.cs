using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Tests for <see cref="BrowserServerLocator"/>.</summary>
public sealed class BrowserServerLocatorTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "huddle-browser-test");

    private static readonly string RepoCli =
        Path.Combine(Root, "tools", "acp", "node_modules", "@playwright", "mcp", "cli.js");

    private static readonly string StartDirectory = Path.Combine(Root, "src", "bin");

    private static readonly string EdgeUnderProgramFiles =
        Path.Combine(Root, "pf", "Microsoft", "Edge", "Application", "msedge.exe");

    private static BrowserLaunchFacts Locate(
        BrowserPlatform platform,
        IEnumerable<string> existing,
        Dictionary<string, string>? environment = null,
        BrowserOptions? options = null,
        string? adapterPath = null)
    {
        HashSet<string> files = new(existing, StringComparer.Ordinal);
        Dictionary<string, string> env = environment ?? [];
        return BrowserServerLocator.Locate(
            options ?? new BrowserOptions(),
            StartDirectory,
            adapterPath,
            files.Contains,
            name => env.GetValueOrDefault(name),
            platform);
    }

    private static Dictionary<string, string> WindowsEnvironment() =>
        new(StringComparer.Ordinal) { ["ProgramFiles"] = Path.Combine(Root, "pf") };

    /// <summary>The CLI is found by walking up from the start directory.</summary>
    [Fact]
    public void Locate_CliInAnAncestor_IsFound()
    {
        BrowserLaunchFacts facts = Locate(BrowserPlatform.Linux, [RepoCli]);

        Assert.Equal(RepoCli, facts.CliPath);
    }

    /// <summary>The CLI is found in the node_modules folder that holds the given adapter.</summary>
    [Fact]
    public void Locate_AdapterPathGiven_FindsTheCliBesideIt()
    {
        string modules = Path.Combine(Root, "elsewhere", "node_modules");
        string adapter = Path.Combine(modules, "@agentclientprotocol", "claude-agent-acp", "dist", "index.js");
        string beside = Path.Combine(modules, "@playwright", "mcp", "cli.js");

        BrowserLaunchFacts facts = Locate(BrowserPlatform.Linux, [beside], adapterPath: adapter);

        Assert.Equal(beside, facts.CliPath);
    }

    /// <summary>With no CLI installed the facts are unavailable and say it is not installed.</summary>
    [Fact]
    public void Locate_NoCli_IsUnavailableAsNotInstalled()
    {
        BrowserLaunchFacts facts = Locate(BrowserPlatform.Windows, [EdgeUnderProgramFiles], WindowsEnvironment());

        Assert.Null(facts.CliPath);
        Assert.False(facts.Available);
        Assert.Equal("Playwright MCP is not installed", facts.UnavailableReason);
    }

    /// <summary>Windows finds Edge under Program Files.</summary>
    [Fact]
    public void Locate_WindowsWithEdge_ChoosesMsedge()
    {
        BrowserLaunchFacts facts = Locate(BrowserPlatform.Windows, [RepoCli, EdgeUnderProgramFiles], WindowsEnvironment());

        Assert.Equal("msedge", facts.Channel);
        Assert.True(facts.Available);
        Assert.Null(facts.UnavailableReason);
    }

    /// <summary>Windows without Edge has no channel and says no browser was found.</summary>
    [Fact]
    public void Locate_WindowsWithoutEdge_HasNoBrowser()
    {
        BrowserLaunchFacts facts = Locate(BrowserPlatform.Windows, [RepoCli], WindowsEnvironment());

        Assert.Null(facts.Channel);
        Assert.Equal("no Edge or Chrome was found", facts.UnavailableReason);
    }

    /// <summary>Linux and macOS prefer Chrome even when Edge is also installed.</summary>
    [Theory]
    [InlineData(BrowserPlatform.Linux, "/usr/bin/google-chrome", "/usr/bin/microsoft-edge")]
    [InlineData(BrowserPlatform.Linux, "/opt/google/chrome/chrome", "/opt/microsoft/msedge/msedge")]
    [InlineData(BrowserPlatform.MacOS, "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome", "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge")]
    public void Locate_ChromeAndEdgeBothInstalled_PrefersChrome(BrowserPlatform platform, string chrome, string edge)
    {
        BrowserLaunchFacts facts = Locate(platform, [RepoCli, chrome, edge]);

        Assert.Equal("chrome", facts.Channel);
    }

    /// <summary>Linux and macOS fall back to Edge when Chrome is missing.</summary>
    [Theory]
    [InlineData(BrowserPlatform.Linux, "/usr/bin/microsoft-edge-stable")]
    [InlineData(BrowserPlatform.MacOS, "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge")]
    public void Locate_OnlyEdgeInstalled_FallsBackToMsedge(BrowserPlatform platform, string edge)
    {
        BrowserLaunchFacts facts = Locate(platform, [RepoCli, edge]);

        Assert.Equal("msedge", facts.Channel);
    }

    /// <summary>Linux and macOS with neither browser give the no-browser reason.</summary>
    [Theory]
    [InlineData(BrowserPlatform.Linux)]
    [InlineData(BrowserPlatform.MacOS)]
    public void Locate_NeitherBrowser_GivesTheNoBrowserReason(BrowserPlatform platform)
    {
        BrowserLaunchFacts facts = Locate(platform, [RepoCli]);

        Assert.Null(facts.Channel);
        Assert.Equal("no Edge or Chrome was found", facts.UnavailableReason);
    }

    /// <summary>A configured channel wins over what is installed.</summary>
    [Fact]
    public void Locate_ChannelOption_Wins()
    {
        BrowserLaunchFacts facts = Locate(
            BrowserPlatform.Linux,
            [RepoCli, "/usr/bin/google-chrome"],
            options: new BrowserOptions { Channel = "msedge" });

        Assert.Equal("msedge", facts.Channel);
    }

    /// <summary>Linux with neither DISPLAY nor WAYLAND_DISPLAY is headless with no display.</summary>
    [Fact]
    public void Locate_LinuxWithoutDisplay_IsHeadlessAndNoDisplay()
    {
        BrowserLaunchFacts facts = Locate(BrowserPlatform.Linux, [RepoCli]);

        Assert.True(facts.Headless);
        Assert.True(facts.NoDisplay);
    }

    /// <summary>Linux with WAYLAND_DISPLAY set is neither headless nor without a display.</summary>
    [Fact]
    public void Locate_LinuxWithWayland_IsNeitherHeadlessNorNoDisplay()
    {
        BrowserLaunchFacts facts = Locate(
            BrowserPlatform.Linux,
            [RepoCli],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["WAYLAND_DISPLAY"] = "wayland-0" });

        Assert.False(facts.Headless);
        Assert.False(facts.NoDisplay);
    }

    /// <summary>The Headless option forces headless on Windows, where a display exists.</summary>
    [Fact]
    public void Locate_HeadlessOptionOnWindows_ForcesHeadless()
    {
        BrowserLaunchFacts facts = Locate(
            BrowserPlatform.Windows,
            [RepoCli, EdgeUnderProgramFiles],
            WindowsEnvironment(),
            new BrowserOptions { Headless = true });

        Assert.True(facts.Headless);
        Assert.False(facts.NoDisplay);
    }

    /// <summary>Windows without the Headless option stays headed.</summary>
    [Fact]
    public void Locate_WindowsDefault_IsHeaded()
    {
        BrowserLaunchFacts facts = Locate(BrowserPlatform.Windows, [RepoCli, EdgeUnderProgramFiles], WindowsEnvironment());

        Assert.False(facts.Headless);
    }

    /// <summary>Enabled false makes the browser unavailable with its own reason, even when everything is installed.</summary>
    [Fact]
    public void Locate_Disabled_IsUnavailableWithItsReason()
    {
        BrowserLaunchFacts facts = Locate(
            BrowserPlatform.Windows,
            [RepoCli, EdgeUnderProgramFiles],
            WindowsEnvironment(),
            new BrowserOptions { Enabled = false });

        Assert.False(facts.Available);
        Assert.Equal("the browser is turned off for this install", facts.UnavailableReason);
    }
}

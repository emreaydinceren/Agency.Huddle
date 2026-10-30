using Agency.Huddle.App.Gateway;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Remote access spec 6.15 (OP-7): the actions that act on the machine Huddle runs on, namely the Agent card's Open and the folder picker,
/// are refused whenever remote access is configured, for every session. The rule: <c>Bind</c> is not <c>loopback</c> or <c>Tailscale:Mode</c> is not <c>off</c>.
/// </summary>
public sealed class HostActionsTests
{
    private static string Source(params string[] path) => File.ReadAllText(CssSource.RepoPath(["src", "Huddle.App", .. path]));

    /// <summary>Loopback bind with Tailscale off is not configured.</summary>
    [Fact]
    public void IsConfigured_LoopbackAndTailscaleOff_IsFalse()
    {
        RemoteAccessState state = new(new GatewayOptions { Bind = "loopback", Tailscale = new GatewayOptions.TailscaleOptions { Mode = "off" } });

        Assert.False(state.IsConfigured);
    }

    /// <summary>Any other bind makes it configured, whatever Tailscale says.</summary>
    [Theory]
    [InlineData("lan")]
    [InlineData("tailnet")]
    [InlineData("custom")]
    public void IsConfigured_NonLoopbackBind_IsTrue(string bind)
    {
        RemoteAccessState state = new(new GatewayOptions { Bind = bind });

        Assert.True(state.IsConfigured);
    }

    /// <summary>Any Tailscale mode other than off makes it configured, even on a loopback bind.</summary>
    [Theory]
    [InlineData("serve")]
    [InlineData("funnel")]
    public void IsConfigured_TailscaleOn_IsTrue(string mode)
    {
        RemoteAccessState state = new(new GatewayOptions { Bind = "loopback", Tailscale = new GatewayOptions.TailscaleOptions { Mode = mode } });

        Assert.True(state.IsConfigured);
    }

    /// <summary>The words are compared without regard to case, so <c>Loopback</c> and <c>OFF</c> still mean off.</summary>
    [Fact]
    public void IsConfigured_UpperCaseLoopbackAndOff_IsFalse()
    {
        RemoteAccessState state = new(new GatewayOptions { Bind = "LOOPBACK", Tailscale = new GatewayOptions.TailscaleOptions { Mode = "OFF" } });

        Assert.False(state.IsConfigured);
    }

    /// <summary>The Agent card's Open refuses while configured, and says why, without starting a process (the path need not exist).</summary>
    [Fact]
    public void OpenFile_Configured_RefusesWithoutLaunching()
    {
        HostActions actions = new(new FakeRemoteAccessState(configured: true));

        HostActionResult result = actions.OpenFile(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".md"));

        Assert.False(result.Done);
        Assert.Equal(HostActions.RefusedMessage, result.Error);
    }

    /// <summary>The folder picker's listing refuses while configured: no folder names of the host leave it.</summary>
    [Fact]
    public void ListFolders_Configured_RefusesAndListsNothing()
    {
        using TempDataDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "secret-project"));
        HostActions actions = new(new FakeRemoteAccessState(configured: true));

        Agency.Huddle.App.Components.Shared.FolderListing listing = actions.ListFolders(dir.Path);

        Assert.Equal(HostActions.RefusedMessage, listing.Error);
        Assert.Empty(listing.Folders);
        Assert.False(listing.CanSelect);
    }

    /// <summary>With remote access off the folder picker lists as it always did.</summary>
    [Fact]
    public void ListFolders_NotConfigured_ListsTheFolders()
    {
        using TempDataDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Path, "alpha"));
        HostActions actions = new(new FakeRemoteAccessState(configured: false));

        Agency.Huddle.App.Components.Shared.FolderListing listing = actions.ListFolders(dir.Path);

        Assert.Null(listing.Error);
        Assert.Equal("alpha", Assert.Single(listing.Folders).Name);
    }

    /// <summary>Both pages that offer the folder picker ask the one injected state before they draw their Browse control, as the picker itself does.</summary>
    [Theory]
    [InlineData("Components", "Pages", "BuildTeam.razor")]
    [InlineData("Components", "Teams", "DuplicateTeamDialog.razor")]
    [InlineData("Components", "Shared", "FolderPickerDialog.razor")]
    [InlineData("Components", "Shared", "AgentProfileCard.razor")]
    public void Razor_HostActionEntryPoints_AskTheOneState(params string[] path)
    {
        string source = Source(path);

        Assert.Contains("IRemoteAccessState", source, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("IsConfigured", source, StringComparison.Ordinal); // contains-ok: haystack is the razor source
    }

    /// <summary>No component starts a process or lists the host's folders on its own: only <see cref="HostActions"/> does (the desktop browser opener and the Tailscale CLI are not host actions).</summary>
    [Fact]
    public void Razor_NoComponentLaunchesAProcessDirectly()
    {
        string card = Source("Components", "Shared", "AgentProfileCard.razor");

        Assert.DoesNotContain("Process.Start", card, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.DoesNotContain("UseShellExecute", card, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.DoesNotContain("FolderBrowser.List", Source("Components", "Shared", "FolderPickerDialog.razor"), StringComparison.Ordinal); // contains-ok: haystack is the razor source
    }
}

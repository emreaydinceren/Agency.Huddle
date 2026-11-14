using Agency.Huddle.App.Components.Archived;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Pins the Archived page's addresses: each tab has its own, <c>/archived</c> is the Teams tab, and every one opens the same Tab.</summary>
public sealed class ArchivedTabsTests
{
    /// <summary>An address names its tab ignoring case; no segment, or one the page does not know, is the Teams tab.</summary>
    /// <param name="url">The address.</param>
    /// <param name="expected">The name of the tab it names.</param>
    [Theory]
    [InlineData("/archived", "Teams")]
    [InlineData("/archived/teams", "Teams")]
    [InlineData("/archived/projects", "Projects")]
    [InlineData("/archived/Rooms?x=1", "Rooms")]
    [InlineData("/archived/nonsense", "Teams")]
    [InlineData(null, "Teams")]
    public void FromUrl_NamesTheTab(string? url, string expected)
    {
        Assert.Equal(Enum.Parse<ArchivedTab>(expected), ArchivedTabs.FromUrl(url));
    }

    /// <summary>
    /// The Rooms tab's old address, <c>/archived/chats</c>, still opens the Rooms tab, so a Tab the browser saved
    /// before the tab was renamed opens where it did; the tab's own address is <c>/archived/rooms</c>.
    /// </summary>
    /// <param name="url">The old address, in any case and with a query.</param>
    [Theory]
    [InlineData("/archived/chats")]
    [InlineData("/archived/Chats?x=1")]
    public void FromUrl_OldChatsAddress_IsTheRoomsTab(string url)
    {
        Assert.Equal(ArchivedTab.Rooms, ArchivedTabs.FromUrl(url));
        Assert.Equal("/archived/rooms", ArchivedTabs.Href(ArchivedTab.Rooms));
    }

    /// <summary>Each tab's address reads back as that tab.</summary>
    /// <param name="tab">The name of the tab.</param>
    [Theory]
    [InlineData("Teams")]
    [InlineData("Projects")]
    [InlineData("Rooms")]
    public void Href_RoundTrips(string tab)
    {
        ArchivedTab parsed = Enum.Parse<ArchivedTab>(tab);

        Assert.Equal(parsed, ArchivedTabs.FromUrl(ArchivedTabs.Href(parsed)));
    }

    /// <summary>Every address of the page opens one Tab, titled Archived, with the Archive icon's name.</summary>
    /// <param name="url">The address.</param>
    [Theory]
    [InlineData("/archived")]
    [InlineData("/archived/projects")]
    [InlineData("/archived/rooms")]
    public void TabKeys_EveryAddressIsTheArchivedTab(string url)
    {
        TabDescriptor descriptor = TabKeys.Describe(url);

        Assert.Equal(new TabDescriptor("archived", "Archived", "archived"), descriptor);
    }
}

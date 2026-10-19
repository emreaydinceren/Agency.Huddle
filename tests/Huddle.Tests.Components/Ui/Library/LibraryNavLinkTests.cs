using Bunit;
using Agency.Huddle.App.Components.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the sidebar's Library entry (<see cref="LibraryNavLink"/>): one home per tree. The Library page owns the tree
/// (Teams, Teammates, Uploads and every pinned folder), so the sidebar carries a single section link to
/// <c>/library</c> and none of the roots, and it renders nothing when <c>Library.Enabled</c> is off.
/// </summary>
public sealed class LibraryNavLinkTests
{
    /// <summary>The entry is one link labelled "Library" that opens the Library page itself, with no scope query.</summary>
    [Fact]
    public async Task RendersOneLinkToTheLibraryPage()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTeamOptions(options => options.Library.Enabled = true).WithTabs();

        var cut = ctx.Render<LibraryNavLink>();

        var link = Assert.Single(cut.FindAll("a"));
        Assert.Equal("/library", link.GetAttribute("href"));
        Assert.Equal("Library", link.TextContent.Trim());
    }

    /// <summary>The Library page's own tree is not repeated: no Teams, Teammates or Uploads row, no group and no Add folder row.</summary>
    [Fact]
    public async Task DoesNotRepeatTheLibraryTree()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTeamOptions(options => options.Library.Enabled = true).WithTabs();

        var cut = ctx.Render<LibraryNavLink>();

        Assert.Empty(cut.FindAll(".mud-nav-group"));
        Assert.Empty(cut.FindAll(".library-nav-root-link"));
        Assert.Empty(cut.FindAll(".nav-action-link"));
        string text = cut.Markup;
        Assert.DoesNotContain("Teammates", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Uploads", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Add folder", text, StringComparison.Ordinal);
    }

    /// <summary>The entry is a destination row, not a section header: the same <c>nav-row-link</c> class the Teammates link takes.</summary>
    [Fact]
    public async Task IsDrawnAsADestinationRow()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTeamOptions(options => options.Library.Enabled = true).WithTabs();

        var cut = ctx.Render<LibraryNavLink>();

        Assert.Single(cut.FindAll(".nav-row-link"));
        Assert.Empty(cut.FindAll(".nav-section-link"));
    }

    /// <summary>With <c>Library.Enabled</c> off, the component renders nothing at all.</summary>
    [Fact]
    public async Task LibraryDisabled_RendersNothing()
    {
        await using MudBunitContext ctx = UiTestHost.Create().WithTeamOptions(options => options.Library.Enabled = false).WithTabs();

        var cut = ctx.Render<LibraryNavLink>();

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>The shell mounts the entry exactly once, so the sidebar links to Library once.</summary>
    [Fact]
    public void MainLayout_MountsTheLibraryEntryOnce()
    {
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(layout, "<LibraryNavLink"));
        Assert.DoesNotContain("library-nav-root-link", layout, StringComparison.Ordinal);
    }
}

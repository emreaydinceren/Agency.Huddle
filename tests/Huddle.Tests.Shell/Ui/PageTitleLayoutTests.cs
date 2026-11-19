using Agency.Huddle.Tests.Css;
namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source-text checks for the page-title model (DS-10, DS-11): every page's title starts at the same left
/// edge, and an inline edit of a title keeps the title's type. Stylesheet and markup are read as text because
/// the rules are what the browser applies; the rendered inline edit is pinned in <c>ChatPageTests</c>.
/// </summary>
public sealed class PageTitleLayoutTests
{

    private static string Page(string name) =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", name));

    /// <summary>A reading page limits the width of its content, not its position: the width cap stays and the centring auto margins are dropped.</summary>
    [Fact]
    public void PageReading_KeepsTheWidthCapAndDropsTheCentring()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        int rule = css.IndexOf(".mud-container.page-reading {", StringComparison.Ordinal);
        Assert.True(rule >= 0, "app.css has no .mud-container.page-reading rule.");
        string body = css[rule..css.IndexOf('}', rule)];
        Assert.Contains("margin-left: 0;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.Contains("margin-right: 0;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.DoesNotContain("max-width", body, StringComparison.Ordinal);
    }

    /// <summary>The reading pages (Settings, Build a team) and the capped Team notice are left-aligned and still use MaxWidth.Large.</summary>
    [Theory]
    [InlineData("Settings.razor")]
    [InlineData("BuildTeam.razor")]
    [InlineData("TeamPage.razor")]
    public void ReadingPages_AreLeftAlignedAtLargeWidth(string page)
    {
        string markup = Page(page);

        Assert.Contains("MaxWidth.Large", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.Contains("page-reading", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
    }

    /// <summary>The inline title edit takes the h5 role's size, weight and line height from the Theme, so the field and the heading it replaces measure the same.</summary>
    [Fact]
    public void TitleInlineEdit_UsesTheH5Role()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        int rule = css.IndexOf(".title-inline-edit .mud-input-slot", StringComparison.Ordinal);
        Assert.True(rule >= 0, "app.css has no .title-inline-edit rule.");
        string body = css[rule..css.IndexOf('}', rule)];
        Assert.Contains("font-size: var(--mud-typography-h5-size);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.Contains("font-weight: var(--mud-typography-h5-weight);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
        Assert.Contains("line-height: var(--mud-typography-h5-lineheight);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not rendered markup
    }

    /// <summary>The Room header's rename field is the shared title inline edit, with no floating label.</summary>
    [Fact]
    public void RoomRename_UsesTheTitleInlineEditWithoutALabel()
    {
        string markup = Page("Chat.razor");

        Assert.Contains("title-inline-edit", markup, StringComparison.Ordinal); // contains-ok: Razor source text, not rendered markup
        Assert.DoesNotContain(" Label=\"Room name\"", markup, StringComparison.Ordinal);
    }
}

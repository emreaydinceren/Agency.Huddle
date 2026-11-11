using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Source-text guards for the card grids of the Hire section, on Build a team and on the Team page's Members tab (B9a, B9c, B12). A
/// browser is not rendered here, so each property is pinned in the stylesheet that carries it, as
/// <c>Library.LibrarySplitLayoutSourceTests</c> does. The roles and the roster are two rows of cards that fill down and then
/// across, and what does not fit is reached by scrolling sideways; the team types above them keep wrapping.
/// </summary>
public sealed class BuildTeamGridLayoutSourceTests
{
    /// <summary>The roles wrap into rows of whole cards (#474): an auto-fill grid of 230px-or-wider columns (200px plus 15%, so a card holds its Add button and Suggested pill on one line), never a sideways scroller that cuts a card through.</summary>
    [Fact]
    public void AppCss_RoleGrid_WrapsIntoWholeCards()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".role-grid");

        Assert.Contains("grid-template-columns: repeat(auto-fill, minmax(230px, 1fr));", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("overflow-x", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("grid-auto-flow", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The roster is two rows that fill down, then across, with the gap and scrollbar room of the spacing scale, and no wrapping columns.</summary>
    [Fact]
    public void AppCss_RosterGrid_IsATwoRowScrollingRule()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        string body = CssText.RuleBody(css, ".roster-grid");
        Assert.Contains("display: grid;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("grid-template-rows: repeat(2, auto);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("grid-auto-flow: column;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("gap: 12px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("overflow-x: auto;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("padding-bottom: 8px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("auto-fill", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("grid-template-columns", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The roster keeps its 20rem cards as a fixed column width.</summary>
    [Fact]
    public void AppCss_RosterGrid_KeepsItsCardWidth()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".roster-grid");

        Assert.Contains("grid-auto-columns: 20rem;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A lone roster card gets one row, so an empty second row does not leave its row gap below the card.</summary>
    [Fact]
    public void AppCss_RosterGrid_ALoneCardHasOneRow()
    {
        string css = Normalised(CssText.AppCss(CssNewlines.AsIs));

        Assert.Contains(
            "\n.roster-grid:not(:has(> :nth-child(2))) {\n    grid-template-rows: auto;\n}",
            css,
            StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The team types above keep wrapping into rows and never scroll sideways: the scrolling rows are the roles' and the roster's alone.</summary>
    [Fact]
    public void AppCss_TeamTypeGrid_StillWraps()
    {
        string body = CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".team-type-grid");

        Assert.Contains("grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("overflow-x", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("grid-auto-flow", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The text with every line ending a plain <c>\n</c>, so a multi-line expectation holds whichever way the file was checked out.</summary>
    /// <param name="css">The stylesheet's text.</param>
    /// <returns>The same text with <c>\n</c> endings.</returns>
    private static string Normalised(string css) => css.Replace("\r\n", "\n", StringComparison.Ordinal);

}

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Source-text guards for the card grids of the Hire section, on Build a team and on the Team page's Members tab (B9a, B9c, B12). A
/// browser is not rendered here, so each property is pinned in the stylesheet that carries it, as
/// <see cref="Library.LibrarySplitLayoutSourceTests"/> does. The roles and the roster are two rows of cards that fill down and then
/// across, and what does not fit is reached by scrolling sideways; the team types above them keep wrapping.
/// </summary>
public sealed class BuildTeamGridLayoutSourceTests
{
    /// <summary>The roles and the roster share one rule: two rows that fill down, then across, with the gap and scrollbar room of the spacing scale, and no wrapping columns.</summary>
    [Fact]
    public void AppCss_RoleGridAndRosterGrid_ShareOneTwoRowScrollingRule()
    {
        string css = ReadAppCss();

        Assert.Contains("\n.role-grid,\n.roster-grid {", Normalised(css), StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        string body = RuleBody(css, ".roster-grid");
        Assert.Contains("display: grid;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("grid-template-rows: repeat(2, auto);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("grid-auto-flow: column;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("gap: 12px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("overflow-x: auto;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("padding-bottom: 8px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("auto-fill", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("grid-template-columns", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The roles keep their 220px cards and the roster its 20rem cards, now as fixed column widths.</summary>
    [Fact]
    public void AppCss_RoleGridAndRosterGrid_KeepTheirCardWidths()
    {
        string css = Normalised(ReadAppCss());

        Assert.Contains("\n.role-grid {\n    grid-auto-columns: 220px;\n}", css, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("\n.roster-grid {\n    grid-auto-columns: 20rem;\n}", css, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>A lone card gets one row, so an empty second row does not leave its row gap below the card.</summary>
    [Fact]
    public void AppCss_RoleGridAndRosterGrid_ALoneCardHasOneRow()
    {
        string css = Normalised(ReadAppCss());

        Assert.Contains(
            "\n.role-grid:not(:has(> :nth-child(2))),\n.roster-grid:not(:has(> :nth-child(2))) {\n    grid-template-rows: auto;\n}",
            css,
            StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>The team types above keep wrapping into rows and never scroll sideways: the scrolling rows are the roles' and the roster's alone.</summary>
    [Fact]
    public void AppCss_TeamTypeGrid_StillWraps()
    {
        string body = RuleBody(ReadAppCss(), ".team-type-grid");

        Assert.Contains("grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("overflow-x", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("grid-auto-flow", body, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>Reads <c>app.css</c> from the repository.</summary>
    /// <returns>The stylesheet's text.</returns>
    private static string ReadAppCss() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    /// <summary>The text with every line ending a plain <c>\n</c>, so a multi-line expectation holds whichever way the file was checked out.</summary>
    /// <param name="css">The stylesheet's text.</param>
    /// <returns>The same text with <c>\n</c> endings.</returns>
    private static string Normalised(string css) => css.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The declarations inside the first rule whose selector is exactly <paramref name="selector"/> at the start of a line, so <c>.a</c> never matches <c>.a-b</c>; a selector that ends a list matches the list's rule.</summary>
    /// <param name="css">The stylesheet's text.</param>
    /// <param name="selector">The selector, as written at the start of a line.</param>
    /// <returns>The text between the rule's braces.</returns>
    private static string RuleBody(string css, string selector)
    {
        string opening = "\n" + selector + " {";
        int start = css.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, $"app.css has no rule with the selector '{selector}'.");

        int end = css.IndexOf('}', start);
        return css[(start + opening.Length)..end];
    }
}

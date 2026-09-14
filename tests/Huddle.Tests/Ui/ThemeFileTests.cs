using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <see cref="ThemeCatalog"/> and <see cref="ThemeTokens"/> against the files they describe, so
/// the C# lists and the on-disk theme stylesheets cannot drift apart. Uses <see cref="CssSource"/>
/// from the wave-1 stylesheet suites, the same technique <see cref="ThemeSourceTests"/> uses, because
/// nothing in this suite renders a browser and CSS custom properties resolve entirely inside the
/// browser's cascade.
/// </summary>
public sealed class ThemeFileTests
{
    /// <summary>
    /// Every theme <see cref="ThemeCatalog.BuiltIn"/> lists has a stylesheet on disk. Without this,
    /// the Appearance tab could offer a theme whose file 404s the moment it is selected.
    /// </summary>
    [Fact]
    public void ThemeCatalog_EveryBuiltInThemeHasAStylesheet()
    {
        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            var path = CssSource.RepoPath("src", "Huddle.App", "wwwroot", "themes", $"{theme.Id}.css");
            Assert.True(File.Exists(path), $"'{theme.Id}' is in ThemeCatalog.BuiltIn but '{path}' does not exist.");
        }
    }

    /// <summary>
    /// The other direction of <see cref="ThemeCatalog_EveryBuiltInThemeHasAStylesheet"/>: every
    /// stylesheet under <c>wwwroot/themes/</c> is listed in <see cref="ThemeCatalog.BuiltIn"/>.
    /// Together the two tests stop the C# list and the files on disk drifting apart in either
    /// direction.
    /// </summary>
    [Fact]
    public void ThemeFiles_EveryStylesheetIsInTheCatalog()
    {
        var themesDirectory = CssSource.RepoPath("src", "Huddle.App", "wwwroot", "themes");
        var catalogIds = ThemeCatalog.BuiltIn.Select(theme => theme.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(themesDirectory, "*.css"))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            Assert.Contains(id, catalogIds);
        }
    }

    /// <summary>
    /// The layering invariant every theme file must uphold: without a <c>color-scheme:</c> of exactly
    /// <c>light</c> or <c>dark</c>, <c>theme.css</c>'s <c>light dark</c> stays in force and every token
    /// the theme did not override resolves against the operating system's preference rather than the
    /// theme's. A dark theme with unmapped surfaces on a light OS would render those surfaces light -
    /// it reads as a tokenisation bug and is not one, provided this test passes.
    /// </summary>
    [Fact]
    public void ThemeFiles_EveryThemeDeclaresColorScheme()
    {
        var themesDirectory = CssSource.RepoPath("src", "Huddle.App", "wwwroot", "themes");

        foreach (var path in Directory.EnumerateFiles(themesDirectory, "*.css"))
        {
            var declaredScheme = ReadColorScheme(path);
            Assert.True(
                string.Equals(declaredScheme, "light", StringComparison.Ordinal)
                    || string.Equals(declaredScheme, "dark", StringComparison.Ordinal),
                $"'{path}' declares color-scheme: {declaredScheme ?? "(none)"}, expected exactly 'light' or 'dark'.");
        }
    }

    /// <summary>
    /// Each theme file's only selector is <c>:root</c>. Anything else loses the equal-specificity,
    /// later-in-source-order argument the three-layer cascade depends on.
    /// </summary>
    [Fact]
    public void ThemeFiles_DeclareExactlyOneRootBlock()
    {
        var themesDirectory = CssSource.RepoPath("src", "Huddle.App", "wwwroot", "themes");

        foreach (var path in Directory.EnumerateFiles(themesDirectory, "*.css"))
        {
            var selectors = ReadSelectors(path);
            Assert.Single(selectors);
            Assert.Equal(":root", selectors[0]);
        }
    }

    /// <summary>
    /// Every custom property a theme file declares is one <c>theme.css</c> also declares. This passes
    /// trivially for the two built-ins, which declare none, and exists for roadmap item 7's imported
    /// themes, which will declare some.
    /// </summary>
    [Fact]
    public void ThemeFiles_DeclareOnlyTokensThemeCssDeclares()
    {
        var themeCssTokens = CssSource.ReadDeclaredTokens(ThemeCssPath).ToHashSet(StringComparer.Ordinal);
        var themesDirectory = CssSource.RepoPath("src", "Huddle.App", "wwwroot", "themes");

        foreach (var path in Directory.EnumerateFiles(themesDirectory, "*.css"))
        {
            foreach (var token in CssSource.ReadDeclaredTokens(path))
            {
                Assert.Contains(token, themeCssTokens);
            }
        }
    }

    /// <summary>
    /// <see cref="ThemeTokens.All"/> is a second list beside <c>theme.css</c>, kept only because
    /// roadmap item 7's override validator needs the key set in C# without parsing a web asset at
    /// startup. This test is what makes that second list safe: it must equal
    /// <c>CssSource.ReadDeclaredTokens(theme.css)</c> as a set, in both directions.
    /// </summary>
    [Fact]
    public void ThemeTokens_MatchesThemeCssExactly()
    {
        var declared = CssSource.ReadDeclaredTokens(ThemeCssPath).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(declared.Count, ThemeTokens.All.Count);

        foreach (var token in declared)
        {
            Assert.Contains(token, ThemeTokens.All);
        }

        foreach (var token in ThemeTokens.All)
        {
            Assert.Contains(token, declared);
        }
    }

    private static string ThemeCssPath =>
        CssSource.RepoPath("src", "Huddle.App", "wwwroot", "theme.css");

    /// <summary>
    /// Returns the value of the <c>color-scheme:</c> declaration inside <paramref name="cssPath"/>'s
    /// <c>:root</c> block, or <see langword="null"/> if the file declares none.
    /// </summary>
    /// <param name="cssPath">Path to the stylesheet to read.</param>
    private static string? ReadColorScheme(string cssPath)
    {
        foreach (var line in File.ReadLines(cssPath))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("color-scheme:", StringComparison.Ordinal))
            {
                continue;
            }

            var semicolonIndex = trimmed.IndexOf(';');
            Assert.True(semicolonIndex > 0, $"'{cssPath}' has a color-scheme declaration with no terminating ';'.");

            return trimmed["color-scheme:".Length..semicolonIndex].Trim();
        }

        return null;
    }

    /// <summary>
    /// Every selector in <paramref name="cssPath"/> - the text immediately before each <c>{</c> - in
    /// file order.
    /// </summary>
    /// <param name="cssPath">Path to the stylesheet to read.</param>
    private static List<string> ReadSelectors(string cssPath)
    {
        var text = File.ReadAllText(cssPath);
        List<string> selectors = [];
        var searchStart = 0;

        while (true)
        {
            var braceIndex = text.IndexOf('{', searchStart);
            if (braceIndex < 0)
            {
                break;
            }

            var lineStart = text.LastIndexOf('\n', braceIndex) + 1;
            var selector = text[lineStart..braceIndex].Trim();
            if (!string.IsNullOrEmpty(selector))
            {
                selectors.Add(selector);
            }

            searchStart = braceIndex + 1;
        }

        return selectors;
    }
}

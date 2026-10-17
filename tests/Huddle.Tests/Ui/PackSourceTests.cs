using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Holds every Theme style pack (<c>wwwroot/packs/*.css</c>, ADR-0057) to the rules <c>app.css</c> keeps, plus the one
/// rule that makes a pack safe to load for every Theme: each of its selectors is scoped to its own Theme's marker, so
/// it can never leak into another. A source assertion, for the reason <see cref="ThemeSourceTests"/> gives: nothing in
/// this suite renders a browser.
/// </summary>
public sealed partial class PackSourceTests
{
    /// <summary>Every pack stylesheet, as theory data of its file name without the extension.</summary>
    public static TheoryData<string> PackNames()
    {
        TheoryData<string> data = new();
        foreach (string path in Directory.EnumerateFiles(PacksDirectory(), "*.css"))
        {
            data.Add(Path.GetFileNameWithoutExtension(path));
        }

        return data;
    }

    /// <summary>The packs folder holds at least Nordic's pack, so the theories below cannot pass by enumerating nothing.</summary>
    [Fact]
    public void PacksFolder_HoldsNordicsPack()
    {
        Assert.True(File.Exists(PackPath(NordicTheme.Pack)), $"Expected {PackPath(NordicTheme.Pack)}.");
    }

    /// <summary>A pack holds no colour literal: every colour is built from a palette variable MudBlazor emits.</summary>
    /// <param name="pack">The pack's name.</param>
    [Theory]
    [MemberData(nameof(PackNames))]
    public void Pack_DeclaresNoColourLiteral(string pack)
    {
        Assert.Empty(CssSource.FindColourLiterals(PackPath(pack)));
    }

    /// <summary>A pack declares no font-family literal; its faces are custom-property values such as <c>--font-read</c>.</summary>
    /// <param name="pack">The pack's name.</param>
    [Theory]
    [MemberData(nameof(PackNames))]
    public void Pack_DeclaresNoFontFamilyLiteral(string pack)
    {
        Assert.Empty(CssSource.FindFontFamilyLiterals(PackPath(pack)));
    }

    /// <summary>
    /// Every <c>var()</c> in a pack names a variable <c>app.css</c> may name, or an <c>--n-*</c> token the same pack
    /// declares, so a typo cannot resolve to nothing in the browser.
    /// </summary>
    /// <param name="pack">The pack's name.</param>
    [Theory]
    [MemberData(nameof(PackNames))]
    public void Pack_UsesOnlyAllowedVariables(string pack)
    {
        string path = PackPath(pack);
        HashSet<string> ownTokens = CssSource.ReadDeclaredTokens(path)
            .Where(static token => token.StartsWith("--n-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> allowed = ThemeSourceTests.AllowedVariables;
        List<string> unknown = CssSource.ReadReferencedTokens(path)
            .Where(token => !allowed.Contains(token) && !ownTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>
    /// A pack declares only its own <c>--n-*</c> tokens and the shared tokens it re-points; it never invents another
    /// name that <c>app.css</c> could start to depend on.
    /// </summary>
    /// <param name="pack">The pack's name.</param>
    [Theory]
    [MemberData(nameof(PackNames))]
    public void Pack_DeclaresOnlyItsOwnOrSharedTokens(string pack)
    {
        HashSet<string> allowed = ThemeSourceTests.AllowedVariables;
        List<string> foreign = CssSource.ReadDeclaredTokens(PackPath(pack))
            .Where(token => !token.StartsWith("--n-", StringComparison.Ordinal) && !allowed.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(foreign);
    }

    /// <summary>
    /// Every selector in a pack starts with its own scope, <c>:root:has([data-theme-pack="name"])</c> or the
    /// single-Theme <c>:root:has([data-huddle-theme="name-..."])</c>, and the pack has no at-rule that could
    /// carry an unscoped one. This is what makes linking every pack for every Theme safe.
    /// </summary>
    /// <param name="pack">The pack's name.</param>
    [Theory]
    [MemberData(nameof(PackNames))]
    public void Pack_ScopesEverySelectorToItsTheme(string pack)
    {
        string source = CssComment().Replace(File.ReadAllText(PackPath(pack)), string.Empty);
        string packScope = $":root:has([data-theme-pack=\"{pack}\"])";
        string themeScope = $":root:has([data-huddle-theme=\"{pack}-";

        Assert.DoesNotContain("@", source, StringComparison.Ordinal);

        List<string> selectors = [.. RuleSelectors().Matches(source)
            .SelectMany(static match => match.Groups["selectors"].Value.Split(','))
            .Select(static selector => selector.Trim())];
        Assert.NotEmpty(selectors);

        List<string> unscoped = selectors
            .Where(selector => !selector.StartsWith(packScope, StringComparison.Ordinal)
                && !selector.StartsWith(themeScope, StringComparison.Ordinal))
            .ToList();
        Assert.Empty(unscoped);
    }

    /// <summary>
    /// A pack belongs to a Theme: every pack file is named by some built-in Theme's <see cref="ThemeDescriptor.Pack"/>,
    /// and every Theme that names a pack has its file, so neither can be renamed without the other.
    /// </summary>
    [Fact]
    public void Packs_AndTheThemesThatCarryThem_MatchOneToOne()
    {
        HashSet<string> named = ThemeCatalog.BuiltIn
            .Select(static descriptor => descriptor.Pack)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> files = Directory.EnumerateFiles(PacksDirectory(), "*.css")
            .Select(static path => Path.GetFileNameWithoutExtension(path))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(named.Order(StringComparer.Ordinal), files.Order(StringComparer.Ordinal));
    }

    /// <summary>The packs folder under <c>wwwroot</c>.</summary>
    private static string PacksDirectory() => CssSource.RepoPath("src", "Huddle.App", "wwwroot", "packs");

    /// <summary>The path of the pack named <paramref name="pack"/>.</summary>
    /// <param name="pack">The pack's name.</param>
    private static string PackPath(string pack) => Path.Combine(PacksDirectory(), pack + ".css");

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CssComment();

    [GeneratedRegex(@"(?<selectors>[^{}]+)\{[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex RuleSelectors();
}

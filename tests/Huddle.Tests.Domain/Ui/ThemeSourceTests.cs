using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Reads <c>app.css</c> and every scoped <c>.razor.css</c> as source text - a source assertion,
/// for the same reason <c>TeammateDialogParametersTests</c> reflects over real types rather than
/// rendering one - because nothing in this suite renders a browser, and CSS custom properties resolve
/// entirely inside the browser's cascade. MudBlazor's <c>MudTheme</c> is the single source of theming
/// now, so what this suite can
/// still decide is narrower than before <c>theme.css</c> retired: no colour literal outside the one
/// documented exemption, no font-family literal, and every <c>var()</c> in <c>app.css</c> naming
/// either a variable MudBlazor's own theme generates or <c>--font-mono</c>, the one token MudBlazor
/// has no equivalent for (declared in <c>app-vars.css</c>).
/// </summary>
public sealed partial class ThemeSourceTests
{
    /// <summary>
    /// Every CSS custom property name a stylesheet may read besides the registered skin tokens: the variables
    /// <c>MudThemeProvider.GenerateTheme</c> emits for a default <c>MudTheme</c> in MudBlazor 9.10.0, plus the few
    /// <c>app-vars.css</c> and <c>app.css</c> declare themselves. The list lives in production
    /// (<see cref="MudVariables"/>) so the skin expression parser and this test cannot disagree.
    /// </summary>
    internal static IReadOnlySet<string> MudBlazorVariables => MudVariables.All;

    /// <summary>
    /// <c>app.css</c> declares no colour literal: every colour comes from a MudBlazor theme
    /// variable. The failure message names the offending line number and its text - <see cref="CssSource.FindColourLiterals"/>
    /// formats each hit that way - so this test exists to be read by whoever trips it.
    /// </summary>
    [Fact]
    public void AppCss_DeclaresNoColourLiteral()
    {
        var hits = CssSource.FindColourLiterals(AppCssPath);

        Assert.Empty(hits);
    }

    /// <summary>
    /// <c>app.css</c> declares no font-family literal: every <c>font-family</c> value is a
    /// single <c>var(--token)</c> reference. The <c>font: inherit</c> shorthand declarations are
    /// not literals and must not be flagged - <see cref="CssSource.FindFontFamilyLiterals"/>'s
    /// rule is "not a single <c>var(--...)</c>", and <c>inherit</c> is explicitly allowed.
    /// </summary>
    [Fact]
    public void AppCss_DeclaresNoFontFamilyLiteral()
    {
        var hits = CssSource.FindFontFamilyLiterals(AppCssPath);

        Assert.Empty(hits);
    }

    /// <summary>
    /// <c>app.css</c>'s braces balance outside comments, and never close more than they opened. An
    /// unclosed block nests every later rule inside it - a merge that dropped one <c>}</c> after an
    /// <c>@container</c> query once left every chip rule after it unapplied while each source assertion
    /// on those rules still passed.
    /// </summary>
    [Fact]
    public void AppCss_BracesBalance()
    {
        string css = File.ReadAllText(AppCssPath);
        int depth = 0;
        int lowest = 0;

        int index = 0;
        while (index < css.Length)
        {
            if (css[index] == '/' && index + 1 < css.Length && css[index + 1] == '*')
            {
                int end = css.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? css.Length : end + 2;
                continue;
            }

            if (css[index] == '{')
            {
                depth++;
            }
            else if (css[index] == '}')
            {
                depth--;
                lowest = Math.Min(lowest, depth);
            }

            index++;
        }

        Assert.Equal(0, lowest);
        Assert.Equal(0, depth);
    }

    /// <summary>
    /// Every token <c>app.css</c> references through <c>var(...)</c> is one MudBlazor's own theme
    /// generates, a registered skin token, or one of the few names <c>app-vars.css</c> declares as an alias
    /// (<c>--font-mono</c>, <c>--font-read</c>, the <c>--ink-*</c> ladder). This is the
    /// mechanical successor to the retired <c>AppCss_UsesOnlyTokensDeclaredInThemeCss</c>: with
    /// <c>theme.css</c> gone, "one source of truth" for a token name means MudBlazor's own
    /// generated variable set plus the skin token registry rather than a file this repository owns.
    /// </summary>
    [Fact]
    public void AppCss_UsesOnlyMudBlazorOrSkinTokens()
    {
        var referenced = CssSource.ReadReferencedTokens(AppCssPath).Distinct(StringComparer.Ordinal);

        var allowed = AllowedVariables;
        var undeclared = referenced.Where(token => !allowed.Contains(token)).ToList();

        Assert.Empty(undeclared);
    }

    /// <summary>
    /// Every <c>var(--hu-*)</c> read anywhere in <c>app.css</c> and every scoped stylesheet names a registered skin
    /// token, so a typo such as <c>--hu-ground-pgae</c> fails here instead of resolving to nothing in the browser.
    /// </summary>
    [Fact]
    public void AppCss_ReadsOnlyKnownTokens()
    {
        HashSet<string> known = SkinTokens.All.Select(token => token.CssName).ToHashSet(StringComparer.Ordinal);
        List<string> paths = [AppCssPath];
        paths.AddRange(Directory.EnumerateFiles(CssSource.RepoPath("src", "Huddle.App", "Components"), "*.razor.css", SearchOption.AllDirectories));

        var unknown = paths
            .SelectMany(CssSource.ReadReferencedTokens)
            .Where(token => token.StartsWith("--hu-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Where(token => !known.Contains(token))
            .ToList();

        Assert.Empty(unknown);
    }

    /// <summary>
    /// The names a stylesheet may take in <c>var()</c>: MudBlazor's own variables and the three aliases
    /// <c>app-vars.css</c> keeps (<see cref="MudBlazorVariables"/>), plus the CSS name of every registered skin token.
    /// </summary>
    internal static HashSet<string> AllowedVariables
    {
        get
        {
            HashSet<string> allowed = new(MudBlazorVariables, StringComparer.Ordinal);
            allowed.UnionWith(SkinTokens.All.Select(token => token.CssName));
            return allowed;
        }
    }

    /// <summary>
    /// Every scoped stylesheet under <c>Components/**/*.razor.css</c> declares no colour literal,
    /// with one documented exemption - <c>MainLayout.razor.css</c>. That file's
    /// <c>#blazor-error-ui</c> banner declares <c>color-scheme: light only</c> and a hard-coded
    /// <c>lightyellow</c> background on purpose: it is the banner Blazor shows once the circuit has
    /// already failed, the one moment a theme cannot be trusted, so it must stay legible regardless
    /// of which theme is in force. Enumerating the directory, rather than naming each file that
    /// needs tokenising, is the point: a scoped stylesheet added later is covered by this test the
    /// day it appears, with no test change required.
    /// </summary>
    [Fact]
    public void ScopedCss_DeclaresNoColourLiteral()
    {
        var componentsDirectory = CssSource.RepoPath("src", "Huddle.App", "Components");
        var scopedStylesheets = Directory
            .EnumerateFiles(componentsDirectory, "*.razor.css", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), ExemptScopedStylesheet, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(scopedStylesheets);

        var hits = scopedStylesheets.SelectMany(CssSource.FindColourLiterals).ToList();

        Assert.Empty(hits);
    }

    /// <summary>
    /// Every <c>var()</c> in every scoped stylesheet under <c>Components/**/*.razor.css</c> names
    /// either a variable MudBlazor's own theme generates or <c>--font-mono</c> - the scoped-stylesheet
    /// counterpart of <see cref="AppCss_UsesOnlyMudBlazorOrSkinTokens"/>, and the test that would have
    /// caught <c>ReconnectModal.razor.css</c> being left pointing at tokens <c>theme.css</c> no longer
    /// declares: that file has no colour <em>literal</em>, so <see cref="ScopedCss_DeclaresNoColourLiteral"/>
    /// passed regardless, while its <c>var()</c> calls quietly resolved to nothing. No exemption for
    /// <c>MainLayout.razor.css</c> here - unlike the colour-literal sweep, that file uses hard-coded
    /// literals rather than <c>var()</c> for its always-legible error banner, so it has nothing for
    /// this test to flag and passes without special-casing. Enumerating the directory, rather than
    /// naming each file, is the same argument <see cref="ScopedCss_DeclaresNoColourLiteral"/> makes:
    /// a scoped stylesheet added later is covered the day it appears, with no test change required.
    /// </summary>
    [Fact]
    public void ScopedCss_UsesOnlyMudBlazorVariables()
    {
        var componentsDirectory = CssSource.RepoPath("src", "Huddle.App", "Components");
        var scopedStylesheets = Directory
            .EnumerateFiles(componentsDirectory, "*.razor.css", SearchOption.AllDirectories)
            .ToList();

        Assert.NotEmpty(scopedStylesheets);

        var allowed = AllowedVariables;
        var undeclared = scopedStylesheets
            .SelectMany(CssSource.ReadReferencedTokens)
            .Distinct(StringComparer.Ordinal)
            .Where(token => !allowed.Contains(token))
            .ToList();

        Assert.Empty(undeclared);
    }

    /// <summary>
    /// No <c>MudText</c> in any <c>.razor</c> file under <c>Components</c> carries
    /// <c>Color="Color.Secondary"</c>. In MudBlazor that is the secondary <em>brand</em> colour - a
    /// blue in this application's palette, per <c>ThemeCatalog</c> - not "muted body text", which is
    /// the utility class <c>mud-text-secondary</c> (resolving to
    /// <c>--mud-palette-text-secondary</c>). The two spellings are one word apart and read as
    /// interchangeable, which is exactly how seven <c>MudText</c> elements across
    /// <c>Teammates.razor</c> and <c>TeammateCard.razor</c> once rendered as brand-blue links instead
    /// of the muted grey text every other element on the same page already got right through
    /// <c>app.css</c>'s <c>var(--mud-palette-text-secondary)</c>. Enumerating the directory, rather
    /// than naming each file, is the same argument <see cref="ScopedCss_DeclaresNoColourLiteral"/>
    /// makes: a component added later is covered the day it appears, with no test change required.
    /// </summary>
    [Fact]
    public void RazorComponents_NoMudTextUsesColorSecondary()
    {
        var componentsDirectory = CssSource.RepoPath("src", "Huddle.App", "Components");
        var razorFiles = Directory
            .EnumerateFiles(componentsDirectory, "*.razor", SearchOption.AllDirectories)
            .ToList();

        Assert.NotEmpty(razorFiles);

        var hits = razorFiles.SelectMany(FindMudTextColorSecondary).ToList();

        Assert.Empty(hits);
    }

    /// <summary>
    /// Every <c>&lt;MudText ... Color="Color.Secondary" ...&gt;</c> opening tag found in
    /// <paramref name="razorPath"/>, formatted as <c>"path line N"</c> so a hit is readable without
    /// opening the file.
    /// </summary>
    /// <param name="razorPath">Path to the <c>.razor</c> file to scan.</param>
    private static IReadOnlyList<string> FindMudTextColorSecondary(string razorPath)
    {
        var text = File.ReadAllText(razorPath);
        List<string> hits = [];
        foreach (Match match in MudTextColorSecondaryPattern().Matches(text))
        {
            var line = text[..match.Index].Count(character => character == '\n') + 1;
            hits.Add($"{razorPath} line {line}");
        }

        return hits;
    }

    [GeneratedRegex(@"<MudText\b[^>]*\bColor\s*=\s*""Color\.Secondary""[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex MudTextColorSecondaryPattern();

    /// <summary>
    /// No <c>.razor</c> file under <c>Components</c> carries a colour literal inside a
    /// <c>Style="..."</c> attribute. <c>TeammateAvatar</c> is the reason this rule exists: it renders
    /// a per-Teammate background colour, and that colour is real user data - typed into
    /// <c>avatars.json</c> or picked in the avatar editor - not a design choice this repository owns,
    /// so it cannot live in a stylesheet the way every other colour in this application does. It is
    /// built entirely from a C# string (<c>TeammateAvatar.InlineStyle</c>) and handed to
    /// <c>MudAvatar</c>'s own <c>Style</c> parameter as <c>Style="@this.InlineStyle"</c> - never a
    /// literal typed straight into markup. Nothing above catches that shape: every colour sweep in
    /// this class reads <c>.css</c> files, and <see cref="RazorComponents_NoMudTextUsesColorSecondary"/>
    /// only ever looks at a <c>Color="Color.Secondary"</c> enum comparison, so a hex value typed into a
    /// <c>Style="..."</c> attribute would slip straight through every other test here and still build
    /// clean with zero warnings. This test passes the day it is written - nothing under
    /// <c>Components/</c> currently puts a colour literal in a <c>Style="..."</c> attribute - so its
    /// value is entirely in stopping a later regression, not in catching a present one. Enumerating
    /// the directory, rather than naming each file, is the same argument
    /// <see cref="ScopedCss_DeclaresNoColourLiteral"/> and <see cref="RazorComponents_NoMudTextUsesColorSecondary"/>
    /// make: a component added later is covered the day it appears, with no test change required. No
    /// exemption list: if one is ever needed, a colour literal has crept into a component's markup and
    /// that is the defect to fix, not a reason to carve out a hole in this test.
    /// </summary>
    [Fact]
    public void RazorComponents_NoInlineColourLiteralInStyleAttribute()
    {
        var componentsDirectory = CssSource.RepoPath("src", "Huddle.App", "Components");
        var razorFiles = Directory
            .EnumerateFiles(componentsDirectory, "*.razor", SearchOption.AllDirectories)
            .ToList();

        Assert.NotEmpty(razorFiles);

        var hits = razorFiles.SelectMany(FindInlineColourLiteralInStyleAttribute).ToList();

        Assert.Empty(hits);
    }

    /// <summary>
    /// Every <c>Style="..."</c> attribute in <paramref name="razorPath"/> whose value contains a
    /// colour literal, formatted as <c>"path line N"</c> - the same format
    /// <see cref="FindMudTextColorSecondary"/> uses, so a hit is readable without opening the file.
    /// Reuses <see cref="CssSource.ColourLiteralPattern"/> rather than duplicating it, so a stylesheet
    /// declaration and a component's inline style are held to exactly one colour-literal rule.
    /// </summary>
    /// <param name="razorPath">Path to the <c>.razor</c> file to scan.</param>
    private static IReadOnlyList<string> FindInlineColourLiteralInStyleAttribute(string razorPath)
    {
        var text = File.ReadAllText(razorPath);
        List<string> hits = [];
        foreach (Match match in StyleAttributePattern().Matches(text))
        {
            if (!CssSource.ColourLiteralPattern().IsMatch(match.Groups["value"].Value))
            {
                continue;
            }

            var line = text[..match.Index].Count(character => character == '\n') + 1;
            hits.Add($"{razorPath} line {line}");
        }

        return hits;
    }

    [GeneratedRegex(@"\bStyle\s*=\s*""(?<value>[^""]*)""", RegexOptions.CultureInvariant)]
    private static partial Regex StyleAttributePattern();

    // The one scoped stylesheet the colour-literal sweep does not tokenise, and why: see the summary
    // on ScopedCss_DeclaresNoColourLiteral. It carries no exemption in ScopedCss_UsesOnlyMudBlazorVariables
    // above - see that test's own summary for why none is needed.
    private const string ExemptScopedStylesheet = "MainLayout.razor.css";

    private static string AppCssPath =>
        RepoPaths.App("wwwroot", "app.css");
}

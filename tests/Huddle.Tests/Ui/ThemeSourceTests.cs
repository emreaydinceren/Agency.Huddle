using System.Text.RegularExpressions;

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
    /// Every CSS custom property name <c>MudThemeProvider.GenerateTheme</c> emits for a default
    /// <c>MudTheme</c> in MudBlazor 9.10.0, plus <c>--font-mono</c> - the one token MudBlazor has no
    /// equivalent for, declared in <c>app-vars.css</c> instead. <c>app.css</c> may reference no
    /// <c>var()</c> outside this set; a name here that no rule ever uses is harmless, but a
    /// <c>var()</c> in <c>app.css</c> naming anything outside this set is either a typo or a
    /// hand-invented token, and this list is what makes either one a build failure rather than a
    /// silent no-op in the browser.
    /// </summary>
    private static readonly HashSet<string> MudBlazorVariables = new(StringComparer.Ordinal)
    {
        "--font-mono",

        "--mud-appbar-height",
        "--mud-default-borderradius",
        "--mud-drawer-width-left",
        "--mud-drawer-width-mini-left",
        "--mud-drawer-width-mini-right",
        "--mud-drawer-width-right",
        "--mud-elevation-0", "--mud-elevation-1", "--mud-elevation-2", "--mud-elevation-3",
        "--mud-elevation-4", "--mud-elevation-5", "--mud-elevation-6", "--mud-elevation-7",
        "--mud-elevation-8", "--mud-elevation-9", "--mud-elevation-10", "--mud-elevation-11",
        "--mud-elevation-12", "--mud-elevation-13", "--mud-elevation-14", "--mud-elevation-15",
        "--mud-elevation-16", "--mud-elevation-17", "--mud-elevation-18", "--mud-elevation-19",
        "--mud-elevation-20", "--mud-elevation-21", "--mud-elevation-22", "--mud-elevation-23",
        "--mud-elevation-24", "--mud-elevation-25",
        "--mud-native-html-color-scheme",
        "--mud-palette-action-default", "--mud-palette-action-default-hover",
        "--mud-palette-action-disabled", "--mud-palette-action-disabled-background",
        "--mud-palette-appbar-background", "--mud-palette-appbar-text",
        "--mud-palette-background", "--mud-palette-background-gray",
        "--mud-palette-black",
        "--mud-palette-border-opacity",
        "--mud-palette-dark", "--mud-palette-dark-darken", "--mud-palette-dark-hover",
        "--mud-palette-dark-lighten", "--mud-palette-dark-rgb", "--mud-palette-dark-text",
        "--mud-palette-divider", "--mud-palette-divider-light", "--mud-palette-divider-rgb",
        "--mud-palette-drawer-background", "--mud-palette-drawer-icon", "--mud-palette-drawer-text",
        "--mud-palette-error", "--mud-palette-error-darken", "--mud-palette-error-hover",
        "--mud-palette-error-lighten", "--mud-palette-error-rgb", "--mud-palette-error-text",
        "--mud-palette-gray-dark", "--mud-palette-gray-darker", "--mud-palette-gray-default",
        "--mud-palette-gray-light", "--mud-palette-gray-lighter",
        "--mud-palette-info", "--mud-palette-info-darken", "--mud-palette-info-hover",
        "--mud-palette-info-lighten", "--mud-palette-info-rgb", "--mud-palette-info-text",
        "--mud-palette-lines-default", "--mud-palette-lines-inputs",
        "--mud-palette-overlay-dark", "--mud-palette-overlay-light",
        "--mud-palette-primary", "--mud-palette-primary-darken", "--mud-palette-primary-hover",
        "--mud-palette-primary-lighten", "--mud-palette-primary-rgb", "--mud-palette-primary-text",
        "--mud-palette-secondary", "--mud-palette-secondary-darken", "--mud-palette-secondary-hover",
        "--mud-palette-secondary-lighten", "--mud-palette-secondary-rgb", "--mud-palette-secondary-text",
        "--mud-palette-skeleton",
        "--mud-palette-success", "--mud-palette-success-darken", "--mud-palette-success-hover",
        "--mud-palette-success-lighten", "--mud-palette-success-rgb", "--mud-palette-success-text",
        "--mud-palette-surface", "--mud-palette-surface-rgb",
        "--mud-palette-table-hover", "--mud-palette-table-lines", "--mud-palette-table-striped",
        "--mud-palette-tertiary", "--mud-palette-tertiary-darken", "--mud-palette-tertiary-hover",
        "--mud-palette-tertiary-lighten", "--mud-palette-tertiary-rgb", "--mud-palette-tertiary-text",
        "--mud-palette-text-disabled", "--mud-palette-text-disabled-rgb",
        "--mud-palette-text-primary", "--mud-palette-text-primary-rgb",
        "--mud-palette-text-secondary", "--mud-palette-text-secondary-rgb",
        "--mud-palette-warning", "--mud-palette-warning-darken", "--mud-palette-warning-hover",
        "--mud-palette-warning-lighten", "--mud-palette-warning-rgb", "--mud-palette-warning-text",
        "--mud-palette-white",
        "--mud-ripple-color", "--mud-ripple-opacity", "--mud-ripple-opacity-secondary",
        "--mud-typography-body1-family", "--mud-typography-body1-letterspacing",
        "--mud-typography-body1-lineheight", "--mud-typography-body1-size",
        "--mud-typography-body1-text-transform", "--mud-typography-body1-weight",
        "--mud-typography-body2-family", "--mud-typography-body2-letterspacing",
        "--mud-typography-body2-lineheight", "--mud-typography-body2-size",
        "--mud-typography-body2-text-transform", "--mud-typography-body2-weight",
        "--mud-typography-button-family", "--mud-typography-button-letterspacing",
        "--mud-typography-button-lineheight", "--mud-typography-button-size",
        "--mud-typography-button-text-transform", "--mud-typography-button-weight",
        "--mud-typography-caption-family", "--mud-typography-caption-letterspacing",
        "--mud-typography-caption-lineheight", "--mud-typography-caption-size",
        "--mud-typography-caption-text-transform", "--mud-typography-caption-weight",
        "--mud-typography-default-family", "--mud-typography-default-letterspacing",
        "--mud-typography-default-lineheight", "--mud-typography-default-size",
        "--mud-typography-default-text-transform", "--mud-typography-default-weight",
        "--mud-typography-h1-family", "--mud-typography-h1-letterspacing",
        "--mud-typography-h1-lineheight", "--mud-typography-h1-size",
        "--mud-typography-h1-text-transform", "--mud-typography-h1-weight",
        "--mud-typography-h2-family", "--mud-typography-h2-letterspacing",
        "--mud-typography-h2-lineheight", "--mud-typography-h2-size",
        "--mud-typography-h2-text-transform", "--mud-typography-h2-weight",
        "--mud-typography-h3-family", "--mud-typography-h3-letterspacing",
        "--mud-typography-h3-lineheight", "--mud-typography-h3-size",
        "--mud-typography-h3-text-transform", "--mud-typography-h3-weight",
        "--mud-typography-h4-family", "--mud-typography-h4-letterspacing",
        "--mud-typography-h4-lineheight", "--mud-typography-h4-size",
        "--mud-typography-h4-text-transform", "--mud-typography-h4-weight",
        "--mud-typography-h5-family", "--mud-typography-h5-letterspacing",
        "--mud-typography-h5-lineheight", "--mud-typography-h5-size",
        "--mud-typography-h5-text-transform", "--mud-typography-h5-weight",
        "--mud-typography-h6-family", "--mud-typography-h6-letterspacing",
        "--mud-typography-h6-lineheight", "--mud-typography-h6-size",
        "--mud-typography-h6-text-transform", "--mud-typography-h6-weight",
        "--mud-typography-overline-family", "--mud-typography-overline-letterspacing",
        "--mud-typography-overline-lineheight", "--mud-typography-overline-size",
        "--mud-typography-overline-text-transform", "--mud-typography-overline-weight",
        "--mud-typography-subtitle1-family", "--mud-typography-subtitle1-letterspacing",
        "--mud-typography-subtitle1-lineheight", "--mud-typography-subtitle1-size",
        "--mud-typography-subtitle1-text-transform", "--mud-typography-subtitle1-weight",
        "--mud-typography-subtitle2-family", "--mud-typography-subtitle2-letterspacing",
        "--mud-typography-subtitle2-lineheight", "--mud-typography-subtitle2-size",
        "--mud-typography-subtitle2-text-transform", "--mud-typography-subtitle2-weight",
        "--mud-zindex-appbar", "--mud-zindex-dialog", "--mud-zindex-drawer",
        "--mud-zindex-popover", "--mud-zindex-snackbar", "--mud-zindex-tooltip",
    };

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
    /// Every token <c>app.css</c> references through <c>var(...)</c> is one MudBlazor's own theme
    /// generates, or <c>--font-mono</c> - the one token MudBlazor has no equivalent for. This is the
    /// mechanical successor to the retired <c>AppCss_UsesOnlyTokensDeclaredInThemeCss</c>: with
    /// <c>theme.css</c> gone, "one source of truth" for a token name means MudBlazor's own
    /// generated variable set rather than a file this repository owns.
    /// </summary>
    [Fact]
    public void AppCss_UsesOnlyMudBlazorVariables()
    {
        var referenced = CssSource.ReadReferencedTokens(AppCssPath).Distinct(StringComparer.Ordinal);

        var undeclared = referenced.Where(token => !MudBlazorVariables.Contains(token)).ToList();

        Assert.Empty(undeclared);
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
    /// counterpart of <see cref="AppCss_UsesOnlyMudBlazorVariables"/>, and the test that would have
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

        var undeclared = scopedStylesheets
            .SelectMany(CssSource.ReadReferencedTokens)
            .Distinct(StringComparer.Ordinal)
            .Where(token => !MudBlazorVariables.Contains(token))
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

    // The one scoped stylesheet the colour-literal sweep does not tokenise, and why: see the summary
    // on ScopedCss_DeclaresNoColourLiteral. It carries no exemption in ScopedCss_UsesOnlyMudBlazorVariables
    // above - see that test's own summary for why none is needed.
    private const string ExemptScopedStylesheet = "MainLayout.razor.css";

    private static string AppCssPath =>
        CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css");
}

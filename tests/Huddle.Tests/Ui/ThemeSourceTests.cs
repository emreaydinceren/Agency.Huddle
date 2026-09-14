namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Reads <c>theme.css</c> as source text - the same technique <see cref="TeammatesRazorSourceTests"/>
/// uses and for the same reason - because nothing in this suite renders a browser, and CSS custom
/// properties resolve entirely inside the browser's cascade. These tests exist to keep the base
/// layer's shape a decision someone has to confirm: exactly 39 tokens, every colour token carrying
/// both a light and a dark value, and the 4 typography tokens carrying neither.
/// </summary>
public sealed class ThemeSourceTests
{
    // The 35 colour tokens theme.css declares, grouped exactly as the file groups them.
    private static readonly string[] ColourTokens =
    [
        "--surface-base", "--surface-sidebar", "--surface-raised", "--surface-sunken",
        "--surface-muted", "--surface-hover", "--surface-selected",
        "--border-default", "--border-subtle", "--border-control", "--border-emphasis",
        "--text-primary", "--text-secondary", "--text-muted", "--text-faint", "--text-disabled",
        "--text-on-accent",
        "--accent", "--accent-hover", "--accent-indicator",
        "--status-online", "--status-offline", "--status-degraded", "--status-starting",
        "--danger-fg", "--danger-bg", "--danger-border", "--warning-fg", "--warning-bg",
        "--info-fg", "--info-bg", "--success-fg",
        "--shadow-soft", "--shadow-strong", "--scrim",
    ];

    // The 4 typography tokens theme.css declares. Named explicitly, rather than derived, so this
    // test fails the moment one is renamed without an update here.
    private static readonly string[] TypographyTokens = ["--font-ui", "--font-chat", "--font-mono", "--font-size-base"];

    /// <summary>
    /// <c>theme.css</c> declares exactly the 39 tokens item 6 and item 7 agreed on, and none twice.
    /// A hard-coded count makes adding a fortieth token a decision someone has to confirm, which is
    /// the cheapest version of item 7's "one source of truth".
    /// </summary>
    [Fact]
    public void ThemeCss_DeclaresEveryTokenOnce()
    {
        var declared = CssSource.ReadDeclaredTokens(ThemeCssPath);

        Assert.Equal(39, declared.Count);

        var distinct = declared.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(declared.Count, distinct.Count);
    }

    /// <summary>
    /// Every one of the 35 colour tokens is declared with <c>light-dark(</c> and exactly two
    /// comma-separated arguments, so a theme built on this file always gets both a light and a dark
    /// value from the base layer - the fall-through roadmap item 7 relies on.
    /// </summary>
    [Fact]
    public void ThemeCss_EveryColourTokenCarriesBothPalettes()
    {
        var text = File.ReadAllText(ThemeCssPath);

        foreach (var token in ColourTokens)
        {
            var value = GetDeclaredValue(text, token);
            Assert.True(
                value.StartsWith("light-dark(", StringComparison.Ordinal),
                $"'{token}' is declared as \"{value}\", not a light-dark(...) call.");

            var arguments = SplitTopLevelArguments(value);
            Assert.True(
                arguments.Count == 2,
                $"'{token}'s light-dark(...) call has {arguments.Count} argument(s), expected 2: \"{value}\".");
        }
    }

    /// <summary>
    /// The 4 typography tokens carry a plain value or a reference to another declared token, and
    /// never <c>light-dark()</c> - it takes <c>&lt;color&gt;</c> arguments only, and a font stack has
    /// no light and dark form.
    /// </summary>
    [Fact]
    public void ThemeCss_TypographyTokensCarryPlainValues()
    {
        var text = File.ReadAllText(ThemeCssPath);
        var declaredTokens = CssSource.ReadDeclaredTokens(ThemeCssPath);

        foreach (var token in TypographyTokens)
        {
            var value = GetDeclaredValue(text, token);

            Assert.DoesNotContain("light-dark(", value, StringComparison.Ordinal);

            if (value.StartsWith("var(", StringComparison.Ordinal))
            {
                var referencedToken = value["var(".Length..^1].Trim();
                Assert.Contains(referencedToken, declaredTokens);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"'{token}' has an empty value.");
            }
        }
    }

    /// <summary>
    /// <c>app.css</c> declares no colour literal: every colour comes from a token declared in
    /// <c>theme.css</c>, the selected theme layered over it, or the Human's own overrides. The
    /// failure message names the offending line number and its text - <see cref="CssSource.FindColourLiterals"/>
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
    /// Every token <c>app.css</c> references through <c>var(...)</c> is one of the tokens
    /// <c>theme.css</c> declares - the mechanical half of "one source of truth" for tokens;
    /// roadmap item 7 adds the mirror assertion over the same helpers.
    /// </summary>
    [Fact]
    public void AppCss_UsesOnlyTokensDeclaredInThemeCss()
    {
        var referenced = CssSource.ReadReferencedTokens(AppCssPath).Distinct(StringComparer.Ordinal);
        var declared = CssSource.ReadDeclaredTokens(ThemeCssPath).ToHashSet(StringComparer.Ordinal);

        var undeclared = referenced.Where(token => !declared.Contains(token)).ToList();

        Assert.Empty(undeclared);
    }

    /// <summary>
    /// Every scoped stylesheet under <c>Components/**/*.razor.css</c> declares no colour literal,
    /// with one documented exemption - <c>MainLayout.razor.css</c>. That file's
    /// <c>#blazor-error-ui</c> banner declares <c>color-scheme: light only</c> and a hard-coded
    /// <c>lightyellow</c> background on purpose: it is the banner Blazor shows once the circuit has
    /// already failed, the one moment a theme cannot be trusted, so it must stay legible regardless
    /// of which theme or override is in force. Enumerating the directory, rather than naming
    /// <c>ReconnectModal.razor.css</c> the way T3.2 tokenised it, is the point: a scoped stylesheet
    /// added later is covered by this test the day it appears, with no test change required.
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

    // The one scoped stylesheet this sweep does not tokenise, and why: see the summary on
    // ScopedCss_DeclaresNoColourLiteral.
    private const string ExemptScopedStylesheet = "MainLayout.razor.css";

    private static string AppCssPath =>
        CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css");

    private static string ThemeCssPath =>
        CssSource.RepoPath("src", "Huddle.App", "wwwroot", "theme.css");

    /// <summary>
    /// Finds the line declaring <paramref name="tokenName"/> (a line beginning
    /// <c>tokenName:</c> once trimmed) and returns everything between that <c>:</c> and the line's
    /// terminating <c>;</c>, trimmed. Fails the test outright if the token is not declared.
    /// </summary>
    /// <param name="cssText">The full stylesheet source.</param>
    /// <param name="tokenName">The custom property name, including its leading <c>--</c>.</param>
    private static string GetDeclaredValue(string cssText, string tokenName)
    {
        var prefix = tokenName + ":";
        foreach (var rawLine in cssText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var semicolonIndex = line.IndexOf(';');
            Assert.True(semicolonIndex > prefix.Length, $"'{tokenName}' declaration has no terminating ';': \"{line}\".");

            return line[prefix.Length..semicolonIndex].Trim();
        }

        Assert.Fail($"'{tokenName}' is not declared in theme.css.");
        return string.Empty; // Unreachable: Assert.Fail throws.
    }

    /// <summary>
    /// Splits the parenthesised argument list following <c>light-dark(</c> on its top-level commas,
    /// respecting nested parentheses so a value such as <c>rgb(0 0 0 / 12%)</c> counts as one
    /// argument rather than being split internally.
    /// </summary>
    /// <param name="lightDarkCall">A full <c>light-dark(...)</c> declaration value.</param>
    private static List<string> SplitTopLevelArguments(string lightDarkCall)
    {
        var innerStart = lightDarkCall.IndexOf('(') + 1;
        var innerEnd = lightDarkCall.LastIndexOf(')');
        var inner = lightDarkCall[innerStart..innerEnd];

        List<string> arguments = [];
        var depth = 0;
        var argumentStart = 0;
        for (var i = 0; i < inner.Length; i++)
        {
            switch (inner[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    arguments.Add(inner[argumentStart..i].Trim());
                    argumentStart = i + 1;
                    break;
            }
        }

        arguments.Add(inner[argumentStart..].Trim());
        return arguments;
    }
}

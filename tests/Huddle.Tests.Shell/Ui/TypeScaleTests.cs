using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;
using Agency.Huddle.App.Themes.Skins;
using MudBlazor;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the type scale of ADR-0056: every typography role in every built-in Theme is set in rendered pixels,
/// nothing is under 11px, only three weights are used, and prose is capped at 75ch. A browser is not rendered
/// here, so the stylesheet is pinned as source text, as <see cref="ThemeSourceTests"/> does.
/// </summary>
public sealed partial class TypeScaleTests
{
    /// <summary>The role, its size, its weight, as ADR-0056 tabulates them.</summary>
    /// <returns>One row per typography role the scale sets.</returns>
    public static TheoryData<string, string, string> Roles() => new()
    {
        { "h5", "20px", "600" },
        { "h6", "16px", "600" },
        { "subtitle1", "14px", "400" },
        { "subtitle2", "16px", "600" },
        { "body1", "14px", "400" },
        { "body2", "13px", "400" },
        { "caption", "12px", "400" },
        { "button", "13px", "500" },
    };

    /// <summary>Every built-in Theme carries the scale's size and weight for the role, in rendered pixels.</summary>
    /// <param name="role">The <c>Typo</c> name.</param>
    /// <param name="size">The expected font size.</param>
    /// <param name="weight">The expected font weight.</param>
    [Theory]
    [MemberData(nameof(Roles))]
    public void EveryBuiltInTheme_SetsTheRoleInRenderedPixels(string role, string size, string weight)
    {
        Assert.All(ThemeCatalog.BuiltIn, descriptor =>
        {
            BaseTypography typography = TypeScaleTests.RoleOf(descriptor.Theme.Typography, role);

            Assert.Equal(size, typography.FontSize);
            Assert.Equal(weight, typography.FontWeight);
        });
    }

    /// <summary>The role and its unitless line height, as ADR-0056 tabulates them.</summary>
    /// <returns>One row per typography role the scale sets.</returns>
    public static TheoryData<string, string> LineHeights() => new()
    {
        { "h5", "1.3" },
        { "h6", "1.35" },
        { "subtitle1", "1.4" },
        { "subtitle2", "1.35" },
        { "body1", "1.55" },
        { "body2", "1.4" },
        { "caption", "1.35" },
        { "button", "1.4" },
    };

    /// <summary>Every built-in Theme carries the scale's line height for the role.</summary>
    /// <param name="role">The <c>Typo</c> name.</param>
    /// <param name="lineHeight">The expected unitless line height.</param>
    [Theory]
    [MemberData(nameof(LineHeights))]
    public void EveryBuiltInTheme_SetsTheRoleLineHeight(string role, string lineHeight)
    {
        Assert.All(ThemeCatalog.BuiltIn, descriptor => Assert.Equal(lineHeight, TypeScaleTests.RoleOf(descriptor.Theme.Typography, role).LineHeight));
    }

    /// <summary>The Status and Tag chips share one shape: 24px high, a 6px corner, 0 8px padding, 12px text at 500.</summary>
    /// <param name="selector">The chip rule to inspect.</param>
    [Theory]
    [InlineData(".mud-chip.status-chip {")]
    [InlineData(".mud-chip.tag-chip {")]
    public void AppCss_ChipsHaveTheStatusShape(string selector)
    {
        string css = CssText.AppCss(CssNewlines.AsIs).Replace("\r\n", "\n", StringComparison.Ordinal);
        int rule = css.IndexOf("\n" + selector, StringComparison.Ordinal);
        Assert.True(rule >= 0, "Expected the chip rule in app.css.");

        string body = css[rule..css.IndexOf('}', rule)];

        Assert.Contains("height: 24px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("padding: 0 8px;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("border-radius: var(--hu-radius-control);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("font-size: var(--mud-typography-caption-size);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("font-weight: 500;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>The Group label (audit A4) is the caption role at 600 in Quiet ink: 12px, sentence case, never under 11px.</summary>
    [Fact]
    public void AppCss_GroupLabel_IsCaptionAtSemibold()
    {
        string css = CssText.AppCss(CssNewlines.AsIs).Replace("\r\n", "\n", StringComparison.Ordinal);
        int rule = css.IndexOf(".group-label {", StringComparison.Ordinal);
        Assert.True(rule >= 0, "Expected the .group-label rule in app.css.");

        string body = css[rule..css.IndexOf('}', rule)];

        Assert.Contains("font-size: var(--mud-typography-caption-size);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("font-weight: 600;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains("color: var(--mud-palette-text-secondary);", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>
    /// A Small button is 28px high and takes its size from the Button role (13px) and its weight from MudBlazor's button
    /// class (the role's 500): its rule names the role and sets no weight, so the role and the rule cannot disagree. The
    /// Composer's focused border is 1.5px of accent.
    /// </summary>
    [Fact]
    public void AppCss_SmallButtonAndComposerFocusMatchTheSpec()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.mud-button-filled-size-small \{[^}]*height: 28px;[^}]*font-size: var\(--mud-typography-button-size\);", css);
        Assert.DoesNotMatch(@"\.mud-button-filled-size-small \{[^}]*font-weight:", css);
        Assert.Matches(@"\.composer-input:focus-visible \{[^}]*inset 0 0 0 0\.5px var\(--mud-palette-primary\)", css);
    }

    /// <summary>
    /// The Overline is gone: no Theme sets the role (it keeps MudBlazor's own default), and no stylesheet reads an
    /// overline variable, so the Label (the <c>.group-label</c> class) is the one small-label style.
    /// </summary>
    [Fact]
    public void Overline_IsNeitherSetByTheThemesNorReadByAStylesheet()
    {
        BaseTypography untouched = new MudTheme().Typography.Overline;

        Assert.All(ThemeCatalog.BuiltIn, descriptor =>
        {
            Assert.Equal(untouched.FontSize, descriptor.Theme.Typography.Overline.FontSize);
            Assert.Equal(untouched.FontWeight, descriptor.Theme.Typography.Overline.FontWeight);
            Assert.Equal(untouched.TextTransform, descriptor.Theme.Typography.Overline.TextTransform);
        });
        Assert.DoesNotContain("typography-overline", CssText.AppCss(), StringComparison.Ordinal);
        Assert.DoesNotContain("--mud-typography-overline-size", MudVariables.All);
    }

    /// <summary>The stylesheets declare only the three weights, 400, 500 and 600, as a literal number.</summary>
    [Fact]
    public void AppCss_DeclaresOnlyThreeWeights()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotMatch(@"font-weight:\s*(?!400\b|500\b|600\b)\d", css);
    }

    /// <summary>
    /// A card title is the 14px heading at 600 through <c>.card-title</c>, because the <c>subtitle1</c> role is 400 (MudBlazor
    /// draws every input in it), so <c>app.css</c> no longer sets an input's weight back.
    /// </summary>
    [Fact]
    public void AppCss_CardTitle_IsSubtitleSizeAtSemibold()
    {
        string css = CssText.AppCss(CssNewlines.AsIs).ReplaceLineEndings("\n");

        Assert.Matches(@"\.card-title\s*\{[^}]*font-size:\s*var\(--mud-typography-subtitle1-size\);[^}]*font-weight:\s*600;", css);
        Assert.DoesNotContain(".mud-input.mud-typography-subtitle1", css, StringComparison.Ordinal);
    }

    /// <summary>No avatar label is drawn under 11px: the length classes keep an 11px floor, so no <c>max(7px</c> remains.</summary>
    [Fact]
    public void AppCss_AvatarLabels_KeepTheElevenPixelFloor()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotMatch(@"max\(\s*([0-9]|10)px,", css);
    }

    /// <summary><c>app.css</c> declares no font weight of 700 or more: three weights, 400, 500 and 600.</summary>
    [Fact]
    public void AppCss_UsesNoWeightAbove600()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotMatch(@"font-weight:\s*(700|800|900|bold)", css);
    }

    /// <summary><c>app.css</c> sets no literal font size under 11px, so nothing renders below the floor.</summary>
    [Fact]
    public void AppCss_SetsNoLiteralFontSizeUnder11px()
    {
        foreach (Match match in FontSizePx().Matches(CssText.AppCss(CssNewlines.AsIs)))
        {
            Assert.True(double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 11, match.Value);
        }
    }

    /// <summary>The blocks of prose in the shell (intro, lead, helper text, empty state) are capped at 75 characters a line.</summary>
    [Fact]
    public void AppCss_CapsProseAt75ch()
    {
        string css = CssText.AppCss(CssNewlines.AsIs).ReplaceLineEndings("\n");
        int rule = css.IndexOf(".settings-intro,\n.settings-stored-at,", StringComparison.Ordinal);
        Assert.True(rule >= 0, "Expected the shared measure rule in app.css.");

        string body = css[rule..css.IndexOf('}', rule)];

        Assert.Contains("max-width: 75ch;", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains(".empty-state", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains(".page-header-subtitle", body, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>The Transcript is one centred column of about 760px, and the Agent's plain text and the Human's bubble are each capped inside it.</summary>
    [Fact]
    public void AppCss_TranscriptIsOneColumnOf760px()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.message-list > \*\s*\{\s*max-width:\s*760px;\s*margin-inline:\s*auto;", css);
        Assert.Matches(@"\.message-content > \.message-body\s*\{\s*max-width:\s*75ch;", css);
        Assert.DoesNotContain(".message-card", css, StringComparison.Ordinal);
        Assert.Matches(@"\.message-bubble\s*\{\s*max-width:\s*min\(75%, calc\(75ch \+ 32px\)\);", css);
    }

    /// <summary>The Composer's box sits in the same centred 760px column as the Transcript above it, not across the whole pane.</summary>
    [Fact]
    public void AppCss_ComposerSharesTheTranscriptColumn()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.composer-box\s*\{[^}]*max-width:\s*760px;[^}]*margin-inline:\s*auto;", css);
    }

    /// <summary>The Room's Transcript metrics: 16px between senders, 4px within one, an 8px task dot, 13px sender names, a 12px bubble radius and the day separator's 24px/16px spacing.</summary>
    [Fact]
    public void AppCss_TranscriptMetricsFollowTheRoomSpec()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.message-row\s*\{[^}]*margin-bottom:\s*16px;", css);
        Assert.Matches(@"\.message-row\.grouped\s*\{\s*margin-top:\s*-12px;", css);
        Assert.Matches(@"\.task-ref::before\s*\{[^}]*width:\s*8px;\s*height:\s*8px;", css);
        Assert.Matches(@"\.message-sender\s*\{\s*font-size:\s*var\(--mud-typography-body2-size\);\s*font-weight:\s*600;", css);
        Assert.Matches(@"\.message-bubble\s*\{[^}]*border-radius:\s*var\(--hu-radius-panel\);", css);
        Assert.Matches(@"\.day-separator\s*\{[^}]*margin-block:\s*24px 16px;", css);
    }

    /// <summary>Picks a typography role by its <c>Typo</c> name.</summary>
    /// <param name="typography">The Theme's typography.</param>
    /// <param name="role">The <c>Typo</c> name, such as <c>h5</c>.</param>
    /// <returns>The role's settings.</returns>
    private static BaseTypography RoleOf(Typography typography, string role) => role switch
    {
        "h5" => typography.H5,
        "h6" => typography.H6,
        "subtitle1" => typography.Subtitle1,
        "subtitle2" => typography.Subtitle2,
        "body1" => typography.Body1,
        "body2" => typography.Body2,
        "caption" => typography.Caption,
        "button" => typography.Button,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Not a role the scale sets."),
    };

    /// <summary>A literal <c>font-size</c> in pixels.</summary>
    [GeneratedRegex(@"font-size:\s*([0-9.]+)px", RegexOptions.CultureInvariant)]
    private static partial Regex FontSizePx();
}

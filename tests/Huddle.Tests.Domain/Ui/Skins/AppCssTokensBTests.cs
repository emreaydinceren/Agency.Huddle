using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui.Skins;

/// <summary>
/// Phase 2, package B: the stretch of <c>app.css</c> from "Message layout: avatar gutter" to the Tasks section reads the
/// skin tokens (<c>var(--hu-*)</c>) at every site whose registered default renders exactly the value the site had
/// before. A source test, because nothing here renders a browser; the default itself is pinned once, in the registry
/// tests.
/// </summary>
public sealed class AppCssTokensBTests
{
    private const string RangeStart = "/* ---- Message layout: avatar gutter";

    private const string RangeEnd = "/* ---- Tasks ---";

    private static readonly string Range = AppCssTokensBTests.ReadRange();

    private static string ReadRange()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));
        int start = css.IndexOf(AppCssTokensBTests.RangeStart, StringComparison.Ordinal);
        int end = css.IndexOf(AppCssTokensBTests.RangeEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "app.css lost the section headers that bound package B's range.");
        return css[start..end];
    }

    /// <summary>The body of the first rule in the range whose selector is exactly <paramref name="selector"/>, whitespace collapsed.</summary>
    private static string Rule(string selector)
    {
        string wanted = AppCssTokensBTests.Collapse(selector);
        foreach (Match rule in Regex.Matches(AppCssTokensBTests.Range, @"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)))
        {
            string candidate = AppCssTokensBTests.Collapse(Regex.Replace(rule.Groups["sel"].Value, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)));
            if (string.Equals(candidate, wanted, StringComparison.Ordinal))
            {
                return AppCssTokensBTests.Collapse(rule.Groups["body"].Value);
            }
        }

        Assert.Fail($"The range has no '{selector}' rule.");
        return string.Empty;
    }

    private static string Collapse(string text) => Regex.Replace(text.Trim(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    /// <summary>The transcript body reads its line height from <c>message.line-height</c>, not the Mud typography variable.</summary>
    [Fact]
    public void MessageBody_ReadsTheMessageLineHeightToken()
    {
        string body = AppCssTokensBTests.Rule(".message-body");

        Assert.Contains("line-height: var(--hu-message-line-height);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("body1-lineheight", body, StringComparison.Ordinal);
    }

    /// <summary>The turn-detail toggle and a Teammate tile hover on the <c>hover</c> token; the tile's border is a control line.</summary>
    [Fact]
    public void Hover_TurnDetailToggleAndTeammateTile_ReadTheHoverToken()
    {
        string toggle = AppCssTokensBTests.Rule(".turn-detail-toggle:hover");
        string tile = AppCssTokensBTests.Rule(".teammates-item:hover");

        Assert.Contains("background: var(--hu-hover);", toggle, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("background: var(--hu-hover);", tile, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("border-color: var(--hu-line-control);", tile, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("action-default-hover", toggle + tile, StringComparison.Ordinal);
        Assert.DoesNotContain("lines-default", tile, StringComparison.Ordinal);
    }

    /// <summary>The composer's drop hint and a Teammate tile sit on <c>ground.raised</c>.</summary>
    [Fact]
    public void RaisedGround_DropHintAndTeammateTile_ReadTheToken()
    {
        string hint = AppCssTokensBTests.Rule(".composer-drop-hint");
        string tile = AppCssTokensBTests.Rule(".teammates-item");

        Assert.Contains("background: var(--hu-ground-raised);", hint, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("background: var(--hu-ground-raised);", tile, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("palette-surface", hint + tile, StringComparison.Ordinal);
    }

    /// <summary>A Teammate tile is a card: it reads <c>shadow.card</c> at rest and <c>shadow.card.hover</c> hovered.</summary>
    [Fact]
    public void TeammateTile_ReadsTheCardShadowTokens()
    {
        Assert.Contains("box-shadow: var(--hu-shadow-card);", AppCssTokensBTests.Rule(".teammates-item"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("box-shadow: var(--hu-shadow-card-hover);", AppCssTokensBTests.Rule(".teammates-item:hover"), StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
    }

    /// <summary>The disabled-but-probing outlined border is a control line.</summary>
    [Fact]
    public void ProbingField_BorderReadsTheControlLineToken()
    {
        string body = AppCssTokensBTests.Rule(".mud-input-control.teammate-card-probing .mud-input.mud-input-outlined.mud-disabled .mud-input-outlined-border");

        Assert.Equal("border-color: var(--hu-line-control);", body);
    }

    /// <summary>The pressed skin chip in the avatar library is a chosen item: it reads the <c>selected.*</c> tokens.</summary>
    [Fact]
    public void PressedSkinChip_ReadsTheSelectedTokens()
    {
        string body = AppCssTokensBTests.Rule(".avatar-library-skin[aria-pressed=\"true\"]");

        Assert.Contains("background-color: var(--hu-selected-background);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color: var(--hu-selected-foreground);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("color-mix", body, StringComparison.Ordinal);
    }

    /// <summary>The swatch's keyboard focus ring reads the <c>focus</c> token.</summary>
    [Fact]
    public void SwatchFocusRing_ReadsTheFocusToken()
    {
        string body = AppCssTokensBTests.Rule(".avatar-swatch-input:focus-visible + .avatar-swatch-dot");

        Assert.Contains("outline: 2px solid var(--hu-focus);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("text-primary", body, StringComparison.Ordinal);
    }

    /// <summary>The current settings section reads <c>current.*</c> for ground, text, weight, icon and shadow, and the hover ground.</summary>
    [Fact]
    public void SettingsNavCurrent_ReadsTheCurrentTokens()
    {
        string link = AppCssTokensBTests.Rule(".settings-nav .settings-nav-item-active .mud-nav-link");
        string hover = AppCssTokensBTests.Rule(".settings-nav .settings-nav-item-active .mud-nav-link:hover, .settings-nav .settings-nav-item-active .mud-nav-link:focus-visible");
        string icon = AppCssTokensBTests.Rule(".settings-nav .settings-nav-item-active .mud-nav-link .mud-nav-link-icon.mud-nav-link-icon-default");

        Assert.Contains("color: var(--hu-current-foreground);", link, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("background-color: var(--hu-current-background);", link, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("font-weight: var(--hu-current-weight);", link, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("box-shadow: var(--hu-current-shadow);", link, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("background-color: var(--hu-current-background-hover);", hover, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.Contains("color: var(--hu-current-icon);", icon, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("color-mix", link + hover + icon, StringComparison.Ordinal);
    }

    /// <summary>The Prompts panel's sticky action row sits on <c>ground.page</c>.</summary>
    [Fact]
    public void PromptsActionRow_ReadsThePageGroundToken()
    {
        string body = AppCssTokensBTests.Rule(".settings-prompts-actions");

        Assert.Contains("background: var(--hu-ground-page);", body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source
        Assert.DoesNotContain("palette-surface", body, StringComparison.Ordinal);
    }

    /// <summary>The accent-text recipe (Primary 70% toward Ink) is gone from the range: every site read a token.</summary>
    [Fact]
    public void Range_HasNoAccentInkRecipeLeft()
    {
        Assert.DoesNotContain("var(--mud-palette-primary) 70%, var(--mud-palette-text-primary)", AppCssTokensBTests.Range, StringComparison.Ordinal);
    }
}

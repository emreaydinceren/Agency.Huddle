using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Traits;

/// <summary>
/// Source assertions for the content trait variants of the theming plan (phase 3, package P3-A): the
/// transcript variant <c>message=row</c>, and the composer variants <c>composer=card</c> and <c>send=text</c>; and for the marks
/// (wiki links, references, mentions, quotes and tool summaries) that every Theme draws, so they are plain rules. Their CSS is lifted out of the Nordic pack into <c>app.css</c>, scoped to the trait marker
/// the skin renders instead of to the pack, and reading registry tokens where the pack read <c>--n-*</c>. A source
/// test, for the reason <see cref="ThemeSourceTests"/> gives: no browser renders here.
/// </summary>
public sealed partial class ContentTraitsTests
{
    private const string SectionHeader = "Traits: transcript and composer";
    private const string NextSectionHeader = "Room member health";
    private const string MarksHeader = "Marks: names, references, mentions, quotes and tool summaries";

    /// <summary>The whole of <c>app.css</c>.</summary>
    private static string AppCss => CssText.AppCss(CssNewlines.AsIs);

    /// <summary>The bodies of every rule in the section with a selector ending in <paramref name="selectorPart"/>, joined.</summary>
    /// <param name="selectorPart">The tail of a selector, whitespace-normalised.</param>
    private static string Rule(string selectorPart)
    {
        string withoutComments = CssText.StripComments(CssText.Section(AppCss, SectionHeader, NextSectionHeader));
        List<string> bodies = [];
        foreach (Match rule in RulePattern().Matches(withoutComments))
        {
            string selectors = Whitespace().Replace(rule.Groups["sel"].Value, " ");
            if (selectors.Split(',').Any(item => item.Trim().EndsWith(selectorPart, StringComparison.Ordinal))) // contains-ok: selector tail of one rule of the stylesheet source
            {
                bodies.Add(rule.Groups["body"].Value);
            }
        }

        Assert.NotEmpty(bodies);
        return string.Join("\n", bodies);
    }

    /// <summary>Asserts that a rule body holds a declaration or a token read.</summary>
    /// <param name="expected">The text the rule must hold.</param>
    /// <param name="body">The joined bodies of the rule, from <see cref="Rule"/>.</param>
    private static void Has(string expected, string body) =>
        Assert.Contains(expected, body, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source

    /// <summary>Every non-default value of the four content enums has at least one rule scoped to its marker attribute.</summary>
    [Fact]
    public void Traits_EveryValueHasCss()
    {
        string css = AppCss;
        List<string> missing = [];
        Check<MessageLayout>("message");
        Check<SendStyle>("send");
        Check<ComposerStyle>("composer");

        Assert.Empty(missing);

        void Check<TEnum>(string trait)
            where TEnum : struct, Enum
        {
            foreach (TEnum value in Enum.GetValues<TEnum>().Skip(1))
            {
                string kebab = SkinLoader.Kebab(value.ToString());
                if (!css.Contains($":root:has([data-hu-{trait}=\"{kebab}\"])", StringComparison.Ordinal)) // contains-ok: the scoped selector appears in the stylesheet source
                {
                    missing.Add($"{trait}={kebab}");
                }
            }
        }
    }

    /// <summary>Each content enum gained exactly its planned variant.</summary>
    [Fact]
    public void Traits_EnumsGainThePlannedVariants()
    {
        Assert.Equal(["Bubble", "Row"], Enum.GetNames<MessageLayout>());
        Assert.Equal(["Icon", "Text", "Square"], Enum.GetNames<SendStyle>());
        Assert.Equal(["Box", "Card", "Underlined"], Enum.GetNames<ComposerStyle>());
    }

    /// <summary>No pack token survives the lift: <c>app.css</c> holds no <c>--n-</c> variable.</summary>
    [Fact]
    public void Traits_NoPackTokens() =>
        Assert.DoesNotContain("--n-", AppCss, StringComparison.Ordinal);

    /// <summary>The section sits immediately before the Room member health header and every rule in it is trait-scoped.</summary>
    [Fact]
    public void Section_IsPlacedBeforeRoomMemberHealth_AndEveryRuleIsTraitScoped()
    {
        string withoutComments = CssText.StripComments(CssText.Section(AppCss, SectionHeader, NextSectionHeader));
        List<string> unscoped = [];
        foreach (Match rule in RulePattern().Matches(withoutComments))
        {
            foreach (string selector in rule.Groups["sel"].Value.Split(','))
            {
                if (!selector.TrimStart().StartsWith(":root:has([data-hu-", StringComparison.Ordinal)) // contains-ok: the scope prefix of a selector in the stylesheet source
                {
                    unscoped.Add(selector.Trim());
                }
            }
        }

        Assert.Empty(unscoped);
    }

    /// <summary><c>message=row</c> draws the Human's message as a left-aligned avatar-and-name row with no bubble, tinting on hover with the primary tint.</summary>
    [Fact]
    public void MessageRow_PinsRowLayoutAndReadsRegistryTokens()
    {
        Has("var(--hu-tone-primary-tint)", Rule(":root:has([data-hu-message=\"row\"]) .message-row:hover"));
        Has("display: grid;", Rule(":root:has([data-hu-message=\"row\"]) .message-row.human"));
        Has("display: block;", Rule(".message-row.human > .message-gutter"));

        string bubble = Rule(".message-bubble.message-body");
        Has("background: none;", bubble);
        Has("max-width: 75ch;", bubble);
    }

    /// <summary>The bodies of every rule in the Marks section with a selector ending in <paramref name="selectorPart"/>, joined.</summary>
    /// <param name="selectorPart">The tail of a selector, whitespace-normalised.</param>
    private static string Mark(string selectorPart)
    {
        string withoutComments = CssText.StripComments(CssText.Section(AppCss, MarksHeader, SectionHeader));
        List<string> bodies = [];
        foreach (Match rule in RulePattern().Matches(withoutComments))
        {
            string selectors = Whitespace().Replace(rule.Groups["sel"].Value, " ");
            if (selectors.Split(',').Any(item => item.Trim().EndsWith(selectorPart, StringComparison.Ordinal))) // contains-ok: selector tail of one rule of the stylesheet source
            {
                bodies.Add(rule.Groups["body"].Value);
            }
        }

        Assert.NotEmpty(bodies);
        return string.Join("\n", bodies);
    }

    /// <summary>The marks draw wiki links, references, mentions, quotes and tool summaries as chips and wells in registry tokens, in every Theme.</summary>
    [Fact]
    public void Marks_PinMarksAndReadRegistryTokens()
    {
        string link = Mark(".library-ref");
        Has("var(--hu-accent-ink)", link);
        Has("var(--hu-tone-primary-soft)", link);

        string chip = Mark(".task-ref");
        Has("var(--hu-tone-primary-tint)", chip);
        Has("var(--hu-accent-ink)", chip);
        Has("var(--mud-palette-primary)", chip);
        Has("var(--hu-line-region)", chip);
        Assert.DoesNotContain("tone-info", chip, StringComparison.Ordinal); // contains-ok: one rule of the stylesheet source

        string mention = Mark(".mention");
        Has("var(--hu-accent-ink)", mention);
        Has("var(--hu-tone-primary-tint)", mention);

        string me = Mark(".mention.mention--me");
        Has("var(--hu-tone-warning-soft)", me);
        Has("var(--hu-tone-warning-ink)", me);

        string quote = Mark(".message-body blockquote");
        Has("var(--hu-text-subtle)", quote);
        Has("var(--hu-tone-info-soft)", quote);

        string detail = Mark(".turn-detail");
        Has("var(--hu-ground-sunk)", detail);
        Has("var(--hu-text-subtle)", detail);

        Has("var(--mud-palette-text-secondary)", Mark(".message-time"));
        Has("var(--hu-tone-primary-soft)", Mark("::selection"));
    }

    /// <summary>
    /// No rule is scoped to <c>marks</c> any more. The five marks rules that a more specific rule would otherwise beat (a wiki link,
    /// its hover and missing forms, a reference chip, and the second paragraph's margin) start <c>:root:root</c>, which repeats the specificity the old guard gave
    /// them, so <c>.library-rendered .library-ref</c>, <c>p.message-heading</c> and <c>.task-ref-closed</c> keep losing to them.
    /// </summary>
    [Fact]
    public void Marks_HaveNoTraitScopeAndKeepTheirSpecificity()
    {
        Assert.DoesNotContain("data-hu-marks", AppCss, StringComparison.Ordinal);

        string section = CssText.StripComments(CssText.Section(AppCss, MarksHeader, SectionHeader));
        foreach (string selector in new[] { ".library-ref {", ".library-ref:hover {", ".library-ref.library-ref-missing {", ".message-body p + p {", "a.team-ref {" })
        {
            Assert.Contains(":root:root " + selector, section, StringComparison.Ordinal); // contains-ok: CSS declaration text of the stylesheet source
        }
    }

    /// <summary><c>composer=card</c> makes the box the paper, the input borderless and the bottom 40px a tool row, in registry tokens.</summary>
    [Fact]
    public void ComposerCard_PinsCardAndReadsRegistryTokens()
    {
        string box = Rule("[data-hu-composer=\"card\"]) .composer-box");
        Has("var(--hu-ground-raised)", box);
        Has("var(--hu-line-control)", box);
        Has("var(--hu-shadow-card-hover)", box);
        Has("padding-bottom: 40px;", box);

        string focused = Rule("[data-hu-composer=\"card\"]) .composer-box:focus-within");
        Has("border-color: var(--mud-palette-primary);", focused);
        Has("inset 0 0 0 0.5px var(--mud-palette-primary)", focused);
        Assert.DoesNotContain("--mud-palette-info", focused, StringComparison.Ordinal);
        Has("var(--hu-ground-page)", Rule("[data-hu-composer=\"card\"]) .composer"));
        Has("border: 0;", Rule(".composer-box-attach .composer-input"));
        Has("var(--mud-palette-text-secondary)", Rule(".composer-input::placeholder"));
        Has("left: 8px;", Rule(".composer-attach"));

        string picker = Rule(".composer-picker");
        Has("var(--hu-ground-raised)", picker);
        Has("var(--hu-line-control)", picker);
    }

    /// <summary><c>send=text</c> shows the word beside the icon and reads the Send tokens; the colours themselves are the skin's to set.</summary>
    [Fact]
    public void SendText_PinsLabelAndLeavesSendColoursToTheSkin()
    {
        string label = Rule("[data-hu-send=\"text\"]) .composer-send-label");
        Has("display: block;", label);
        Has("pointer-events: none;", label);
        Has("var(--hu-send-fg)", label);

        Has("padding: 0 48px 0 8px;", Rule("[data-hu-send=\"text\"]) .composer-send .mud-icon-button"));
        Has("var(--hu-accent-ink)", Rule(".composer-send .mud-icon-button:hover"));

        Assert.DoesNotContain("--hu-send-bg:", CssText.Section(AppCss, SectionHeader, NextSectionHeader), StringComparison.Ordinal);
        Assert.DoesNotContain("--hu-send-fg:", CssText.Section(AppCss, SectionHeader, NextSectionHeader), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

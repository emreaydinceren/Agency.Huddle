using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;

namespace Agency.Huddle.Tests.Ui.Traits;

/// <summary>
/// Source assertions for the four content trait variants of the theming plan (phase 3, package P3-A): the
/// transcript variants <c>message=row</c> and <c>marks=chip</c>, and the composer variants <c>composer=card</c> and
/// <c>send=text</c>. Their CSS is lifted out of the Nordic pack into <c>app.css</c>, scoped to the trait marker
/// the skin renders instead of to the pack, and reading registry tokens where the pack read <c>--n-*</c>. A source
/// test, for the reason <see cref="ThemeSourceTests"/> gives: no browser renders here.
/// </summary>
public sealed partial class ContentTraitsTests
{
    private const string SectionHeader = "/* ---- Traits: transcript and composer";
    private const string NextSectionHeader = "/* ---- Room member health";

    private static string AppCssPath => CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css");

    /// <summary>The whole of <c>app.css</c>.</summary>
    private static string AppCss => File.ReadAllText(AppCssPath);

    /// <summary>The text of the new section, from its header to the next section's header.</summary>
    private static string Section()
    {
        string css = AppCss;
        int start = css.IndexOf(SectionHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, "app.css has no 'Traits: transcript and composer' section.");
        int end = css.IndexOf(NextSectionHeader, StringComparison.Ordinal);
        Assert.True(end > start, "The traits section must come before the 'Room member health' section.");
        return css[start..end];
    }

    /// <summary>The bodies of every rule in the section with a selector ending in <paramref name="selectorPart"/>, joined.</summary>
    /// <param name="selectorPart">The tail of a selector, whitespace-normalised.</param>
    private static string Rule(string selectorPart)
    {
        string withoutComments = Comment().Replace(Section(), string.Empty);
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
        Check<MarkStyle>("marks");

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
        Assert.Equal(["Plain", "Chip"], Enum.GetNames<MarkStyle>());
    }

    /// <summary>No pack token survives the lift: <c>app.css</c> holds no <c>--n-</c> variable.</summary>
    [Fact]
    public void Traits_NoPackTokens() =>
        Assert.DoesNotContain("--n-", AppCss, StringComparison.Ordinal);

    /// <summary>The section sits immediately before the Room member health header and every rule in it is trait-scoped.</summary>
    [Fact]
    public void Section_IsPlacedBeforeRoomMemberHealth_AndEveryRuleIsTraitScoped()
    {
        string withoutComments = Comment().Replace(Section(), string.Empty);
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

    /// <summary><c>marks=chip</c> draws wiki links, references, mentions, quotes and tool summaries as chips and wells in registry tokens.</summary>
    [Fact]
    public void MarksChip_PinsMarksAndReadsRegistryTokens()
    {
        string link = Rule("[data-hu-marks=\"chip\"]) .library-ref");
        Has("var(--hu-accent-ink)", link);
        Has("var(--hu-tone-primary-soft)", link);

        string chip = Rule("[data-hu-marks=\"chip\"]) .task-ref");
        Has("var(--hu-tone-info-soft)", chip);
        Has("var(--hu-tone-info-ink)", chip);
        Has("var(--mud-palette-info)", chip);
        Has("var(--hu-line-region)", chip);

        Has("var(--hu-tone-primary-tint)", Rule("[data-hu-marks=\"chip\"]) a.room-ref"));

        string me = Rule(".mention.mention--me");
        Has("var(--hu-tone-warning-soft)", me);
        Has("var(--hu-tone-warning-ink)", me);

        string quote = Rule(".message-body blockquote");
        Has("var(--hu-text-subtle)", quote);
        Has("var(--hu-tone-info-soft)", quote);

        string detail = Rule("[data-hu-marks=\"chip\"]) .turn-detail");
        Has("var(--hu-ground-sunk)", detail);
        Has("var(--hu-text-subtle)", detail);

        Has("var(--mud-palette-text-secondary)", Rule(".message-time"));
        Has("var(--hu-tone-primary-soft)", Rule("::selection"));
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

        Has("var(--mud-palette-info)", Rule(".composer-box:focus-within"));
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

        Assert.DoesNotContain("--hu-send-bg:", Section(), StringComparison.Ordinal);
        Assert.DoesNotContain("--hu-send-fg:", Section(), StringComparison.Ordinal);
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    [GeneratedRegex(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

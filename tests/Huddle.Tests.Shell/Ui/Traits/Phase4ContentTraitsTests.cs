using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Traits;

/// <summary>
/// Source tests for the phase 4 content trait variants <c>send=square</c> and <c>composer=underlined</c>, and for the Task state and
/// priority look that <c>status=glyph</c> made the default. They read <c>app.css</c> as text: each variant is scoped to the marker
/// attribute the layout renders, reads skin tokens or palette variables, and declares no colour literal.
/// </summary>
public sealed partial class Phase4ContentTraitsTests
{
    private static readonly string RawCss = CssText.AppCss(CssNewlines.AsIs);

    private static readonly string Css = CssText.StripComments(RawCss);

    /// <summary>The two enums with their trait attribute names and the value each adds.</summary>
    public static TheoryData<string, string> NewValues() => new()
    {
        { "send", "square" },
        { "composer", "underlined" },
    };

    /// <summary>Each new value is a member of its enum and has at least one rule scoped to its marker attribute.</summary>
    /// <param name="trait">The trait attribute name without the <c>data-hu-</c> prefix.</param>
    /// <param name="value">The kebab-case value.</param>
    [Theory]
    [MemberData(nameof(NewValues))]
    public void Traits_EveryValueHasCss(string trait, string value)
    {
        Type enumType = trait switch
        {
            "send" => typeof(SendStyle),
            _ => typeof(ComposerStyle),
        };
        IEnumerable<string> names = Enum.GetNames(enumType).Select(static name => Kebab(name));

        Assert.Contains(value, names);
        Declares(Css, $":root:has([data-hu-{trait}=\"{value}\"])");
    }

    /// <summary>
    /// The default Task state and priority look (the old <c>status=glyph</c>, now every Theme's except Nordic's <c>status=pill</c>) is a
    /// transparent row: a 10px ring glyph before a label in the subtle ink, with no state icon and no chip fill. The fill and the ink are
    /// guarded to everything but pill, so Nordic keeps the tinted chip; the geometry is plain.
    /// </summary>
    [Fact]
    public void StatusDefault_PinsTransparentRowAndRing()
    {
        string row = Plain(".mud-chip.status-chip.task-status-chip");
        string priorityRow = Plain(".mud-chip.status-chip.task-priority-chip");
        string ink = Guarded(".mud-chip.task-status-chip");
        string priorityInk = Guarded(".mud-chip.task-priority-chip");
        string ring = Plain(".mud-chip.task-status-chip::before");
        string icon = Plain(".mud-chip.status-chip.task-status-chip .mud-chip-icon");

        Declares(row, "height: auto;", "padding: 0;", "border-radius: 0;");
        Declares(priorityRow, "height: auto;", "padding: 0;");
        Declares(ink, "background: transparent;", "color: var(--hu-text-subtle);");
        Declares(priorityInk, "background: transparent;");
        Declares(ring, "width: 10px;", "height: 10px;", "border-radius: 50%;");
        Declares(icon, "display: none;");
    }

    /// <summary>
    /// The default look fills the ring by state: backlog dashed in the disabled ink, to do an empty ring in the subtle
    /// ink, in progress half, review three quarters, done solid, and the closing states solid in the error hue.
    /// </summary>
    [Fact]
    public void StatusDefault_PinsFillsAndHuesPerState()
    {
        string backlog = Plain(".mud-chip.status-chip-state-backlog::before");
        string todo = Plain(".mud-chip.status-chip-state-todo::before");
        string progress = Plain(".mud-chip.status-chip-state-inprogress::before");
        string review = Plain(".mud-chip.status-chip-state-review::before");
        string done = Plain(".mud-chip.status-chip-state-done::before");
        string cancelled = Plain(".mud-chip.status-chip-state-cancelled::before");

        Declares(backlog, "border-style: dashed;", "var(--mud-palette-text-disabled)");
        Declares(todo, "var(--hu-text-subtle)");
        Declares(progress, "linear-gradient(90deg", "var(--mud-palette-info)");
        Declares(review, "conic-gradient(", "75%", "var(--mud-palette-tertiary)");
        Declares(done, "var(--mud-palette-success)");
        Declares(cancelled, "var(--mud-palette-error)");
    }

    /// <summary>
    /// The default look keeps the app's one priority icon set (#470): the priority chip's icon is not hidden and no rule draws bars or
    /// a square in its place, so every Theme shows the same marks. Urgent alone takes the error hue.
    /// </summary>
    [Fact]
    public void StatusDefault_KeepsThePriorityIconSet()
    {
        string section = DefaultStatusRules();
        string urgent = Guarded(".mud-chip.priority-urgent");

        Assert.DoesNotContain("task-priority-chip .mud-chip-icon", section, StringComparison.Ordinal);
        Assert.DoesNotContain("task-priority-chip::before", section, StringComparison.Ordinal);
        Assert.DoesNotContain("priority-high::before", section, StringComparison.Ordinal);
        Declares(urgent, "var(--mud-palette-error)");
    }

    /// <summary>Tags keep today's chip under the default look: no rule in it names a tag selector.</summary>
    [Fact]
    public void StatusDefault_LeavesTagsAlone()
    {
        string section = DefaultStatusRules();

        Assert.DoesNotContain("tag-chip", section, StringComparison.Ordinal);
        Assert.DoesNotContain("task-tag", section, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>status=glyph</c> is gone as a value and as a scope: <see cref="StatusStyle"/> holds the glyph default and the pill override
    /// only, and no rule is scoped to <c>[data-hu-status="glyph"]</c> or <c>chip</c>.
    /// </summary>
    [Fact]
    public void StatusDefault_IsTheGlyphAndHasNoGlyphOrChipScope()
    {
        Assert.Equal(["Glyph", "Pill"], Enum.GetNames<StatusStyle>());
        Assert.Equal(StatusStyle.Glyph, new SkinTraits().Status);
        Assert.DoesNotContain("data-hu-status=\"glyph\"", Css, StringComparison.Ordinal);
        Assert.DoesNotContain("data-hu-status=\"chip\"", Css, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nordic's <c>status=pill</c> keeps the tinted chip's fill and ink: every rule of the default look that clears the fill is guarded
    /// with <c>:not(:has([data-hu-status="pill"]))</c>, so pill never inherits it.
    /// </summary>
    [Fact]
    public void StatusDefault_ClearsTheFillOnlyOutsidePill()
    {
        foreach (Match rule in Rule().Matches(DefaultStatusRules()))
        {
            if (rule.Groups["body"].Value.Contains("background: transparent;", StringComparison.Ordinal)) // contains-ok: CSS declaration text
            {
                foreach (string selector in rule.Groups["sel"].Value.Split(','))
                {
                    Assert.StartsWith(":root:not(:has([data-hu-status=\"pill\"]))", Whitespace().Replace(selector, " ").Trim(), StringComparison.Ordinal); // contains-ok: CSS declaration text of the stylesheet source
                }
            }
        }
    }

    /// <summary>
    /// <c>send=square</c> is a 28px square on the shared Send tokens with the Theme radius, hovering to the darkened
    /// primary, with no label and a 44px target on a narrow screen.
    /// </summary>
    [Fact]
    public void SendSquare_PinsSizeTokensLabelAndMobile()
    {
        string button = Body("send", "square", ".composer-send .mud-icon-button");
        string hover = Body("send", "square", ".composer-send .mud-icon-button:hover");
        string label = Body("send", "square", ".composer-send-label");
        string narrow = Body("send", "square", ".composer-send .mud-icon-button", afterMedia: true);

        Declares(button, "width: 28px;", "height: 28px;", "border-radius: var(--hu-radius-card);");
        Declares(button, "background: var(--hu-send-bg);", "color: var(--hu-send-fg);", "box-shadow: none;");
        Declares(hover, "background: var(--mud-palette-primary-darken);");
        Declares(label, "display: none;");
        Declares(narrow, "44px");
    }

    /// <summary>
    /// <c>composer=underlined</c> keeps the box, adds a permanent 2px primary bottom edge on the Theme radius and the card
    /// shadow, a tool row below the field behind a 1px rule, and a focus ring that leaves the edge in place.
    /// </summary>
    [Fact]
    public void ComposerUnderlined_PinsEdgeShadowAndToolRow()
    {
        string box = Body("composer", "underlined", ".composer-box");
        string rule = Body("composer", "underlined", ".composer-box::after");
        string input = Body("composer", "underlined", ".composer-input");
        string focus = Body("composer", "underlined", ".composer-box:focus-within");
        string attach = Body("composer", "underlined", ".composer-attach");

        Declares(box, "border-bottom: 2px solid var(--mud-palette-primary);", "border-radius: var(--hu-radius-card);");
        Declares(box, "box-shadow: var(--hu-shadow-card-hover);", "padding-bottom: 40px;");
        Declares(rule, "height: 1px;", "var(--hu-line-region)");
        Declares(input, "border: 0;");
        Declares(focus, "border-bottom-color: var(--mud-palette-primary);");
        Declares(attach, "left: 8px;");
    }

    /// <summary>The rules scoped to the three phase 4 trait values add no <c>!important</c> and no pack token.</summary>
    [Fact]
    public void ContentTraitRules_AddNoImportantOrPackToken()
    {
        string section = ContentTraitRules();

        Assert.DoesNotContain("!important", section, StringComparison.Ordinal);
        Assert.DoesNotContain("--n-", section, StringComparison.Ordinal);
    }

    /// <summary>Asserts that stylesheet text holds every given declaration.</summary>
    /// <param name="haystack">Stylesheet or declaration text.</param>
    /// <param name="declarations">The exact declarations or fragments to find.</param>
    private static void Declares(string haystack, params string[] declarations)
    {
        foreach (string declaration in declarations)
        {
            Assert.Contains(declaration, haystack, StringComparison.Ordinal); // contains-ok: CSS declaration text, not markup
        }
    }

    private static string Kebab(string name) =>
        Regex.Replace(name, "(?<!^)([A-Z])", "-$1", RegexOptions.CultureInvariant).ToLowerInvariant();

    /// <summary>
    /// The merged declaration text of every rule whose selector list holds the scoped <paramref name="selector"/>; with
    /// <paramref name="afterMedia"/> only rules inside a <c>@media</c> block are read.
    /// </summary>
    private static string Body(string trait, string value, string selector, bool afterMedia = false)
    {
        string wanted = $":root:has([data-hu-{trait}=\"{value}\"]) {selector}";
        string source = afterMedia ? MediaText() : Css;
        StringBuilder merged = new();
        foreach (Match rule in Rule().Matches(source))
        {
            IEnumerable<string> selectors = rule.Groups["sel"].Value
                .Split(',')
                .Select(static part => Whitespace().Replace(part, " ").Trim());
            if (selectors.Contains(wanted, StringComparer.Ordinal))
            {
                merged.Append(Whitespace().Replace(rule.Groups["body"].Value, " ")).Append(' ');
            }
        }

        Assert.True(merged.Length > 0, $"No rule scoped to {trait}={value} for '{selector}'.");
        return merged.ToString();
    }

    /// <summary>The media blocks that hold a <c>send=square</c> rule.</summary>
    private static string MediaText() => CssText.ScopedMediaBlocks(RawCss, "[data-hu-send=\"square\"]");

    /// <summary>The comment-stripped rules scoped to <c>send=square</c> or <c>composer=underlined</c>, wherever they sit, then the default status look.</summary>
    private static string ContentTraitRules() =>
        CssText.StripComments(CssText.ScopedRules(RawCss, "[data-hu-send=\"square\"]", "[data-hu-composer=\"underlined\"]")) + DefaultStatusRules();

    /// <summary>The comment-stripped default Task state and priority look: from its header up to the Traits banner after it.</summary>
    private static string DefaultStatusRules() =>
        CssText.StripComments(CssText.Section(RawCss, "Task state and priority: a glyph then a small label", "Traits: the variants a Theme chooses for this area"));

    /// <summary>The merged declaration text of every plain rule whose selector list holds exactly <paramref name="selector"/>.</summary>
    /// <param name="selector">The whole selector, with no trait scope.</param>
    private static string Plain(string selector) => Merged(selector);

    /// <summary>The merged declarations of the default look's rules for <paramref name="selector"/>, scoped to everything but pill.</summary>
    /// <param name="selector">The selector after the scope.</param>
    private static string Guarded(string selector) =>
        Merged($":root:not(:has([data-hu-status=\"pill\"])) {selector}");

    private static string Merged(string wanted)
    {
        StringBuilder merged = new();
        foreach (Match rule in Rule().Matches(DefaultStatusRules()))
        {
            IEnumerable<string> selectors = rule.Groups["sel"].Value
                .Split(',')
                .Select(static part => Whitespace().Replace(part, " ").Trim());
            if (selectors.Contains(wanted, StringComparer.Ordinal))
            {
                merged.Append(Whitespace().Replace(rule.Groups["body"].Value, " ")).Append(' ');
            }
        }

        Assert.True(merged.Length > 0, $"No default-look rule for '{wanted}'.");
        return merged.ToString();
    }

    [GeneratedRegex(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Rule();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

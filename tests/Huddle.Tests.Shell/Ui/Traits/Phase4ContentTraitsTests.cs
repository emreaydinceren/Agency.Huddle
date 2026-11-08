using System.Text;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes.Skins;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui.Traits;

/// <summary>
/// Source tests for the phase 4 content trait variants: <c>status=glyph</c>, <c>send=square</c> and
/// <c>composer=underlined</c>. They read <c>app.css</c> as text: each variant is scoped to the marker attribute the layout
/// renders, reads skin tokens or palette variables, and declares no colour literal.
/// </summary>
public sealed partial class Phase4ContentTraitsTests
{
    private static readonly string RawCss = CssText.AppCss(CssNewlines.AsIs);

    private static readonly string Css = CssText.StripComments(RawCss);

    /// <summary>The three enums with their trait attribute names and the value each adds.</summary>
    public static TheoryData<string, string> NewValues() => new()
    {
        { "status", "glyph" },
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
            "status" => typeof(StatusStyle),
            "send" => typeof(SendStyle),
            _ => typeof(ComposerStyle),
        };
        IEnumerable<string> names = Enum.GetNames(enumType).Select(static name => Kebab(name));

        Assert.Contains(value, names);
        Declares(Css, $":root:has([data-hu-{trait}=\"{value}\"])");
    }

    /// <summary>The phase 4 section sits immediately before the chrome section and names itself as the plan says.</summary>
    [Fact]
    public void Phase4Section_SitsBeforeChrome()
    {
        int phase4 = RawCss.IndexOf("/* ---- Traits: phase 4 (status, send, composer)", StringComparison.Ordinal);
        int chrome = RawCss.IndexOf("/* ---- Traits: chrome", StringComparison.Ordinal);

        Assert.True(phase4 > 0, "The phase 4 section is missing.");
        Assert.True(phase4 < chrome, "The phase 4 section must come before Traits: chrome.");
        Assert.DoesNotContain("/* ---- ", RawCss[(phase4 + 5)..chrome], StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>status=glyph</c> turns the Task state and priority chips into a transparent row: a 10px ring glyph before a
    /// 12px label in the subtle ink, with no state icon and no chip fill.
    /// </summary>
    [Fact]
    public void StatusGlyph_PinsTransparentRowAndRing()
    {
        string row = Body("status", "glyph", ".mud-chip.task-status-chip");
        string priorityRow = Body("status", "glyph", ".mud-chip.task-priority-chip");
        string ring = Body("status", "glyph", ".mud-chip.task-status-chip::before");
        string icon = Body("status", "glyph", ".mud-chip.task-status-chip .mud-chip-icon");

        Declares(row, "background: transparent;", "color: var(--hu-text-subtle);");
        Declares(priorityRow, "background: transparent;");
        Declares(ring, "width: 10px;", "height: 10px;", "border-radius: 50%;");
        Declares(icon, "display: none;");
    }

    /// <summary>
    /// <c>status=glyph</c> fills the ring by state: backlog dashed in the disabled ink, to do an empty ring in the subtle
    /// ink, in progress half, review three quarters, done solid, and the closing states solid in the error hue.
    /// </summary>
    [Fact]
    public void StatusGlyph_PinsFillsAndHuesPerState()
    {
        string backlog = Body("status", "glyph", ".mud-chip.status-chip-state-backlog::before");
        string todo = Body("status", "glyph", ".mud-chip.status-chip-state-todo::before");
        string progress = Body("status", "glyph", ".mud-chip.status-chip-state-inprogress::before");
        string review = Body("status", "glyph", ".mud-chip.status-chip-state-review::before");
        string done = Body("status", "glyph", ".mud-chip.status-chip-state-done::before");
        string cancelled = Body("status", "glyph", ".mud-chip.status-chip-state-cancelled::before");

        Declares(backlog, "border-style: dashed;", "var(--mud-palette-text-disabled)");
        Declares(todo, "var(--hu-text-subtle)");
        Declares(progress, "linear-gradient(90deg", "var(--mud-palette-info)");
        Declares(review, "conic-gradient(", "75%", "var(--mud-palette-tertiary)");
        Declares(done, "var(--mud-palette-success)");
        Declares(cancelled, "var(--mud-palette-error)");
    }

    /// <summary>
    /// <c>status=glyph</c> keeps the app's one priority icon set (#470): the priority chip's icon is not hidden and no rule draws bars or
    /// a square in its place, so every Theme shows the same marks. Urgent alone takes the error hue.
    /// </summary>
    [Fact]
    public void StatusGlyph_KeepsThePriorityIconSet()
    {
        string section = CssText.StripComments(CssText.Section(RawCss, "Traits: phase 4", "Traits: chrome"));
        string urgent = Body("status", "glyph", ".mud-chip.priority-urgent");

        Assert.DoesNotContain("task-priority-chip .mud-chip-icon", section, StringComparison.Ordinal);
        Assert.DoesNotContain("task-priority-chip::before", section, StringComparison.Ordinal);
        Assert.DoesNotContain("priority-high::before", section, StringComparison.Ordinal);
        Declares(urgent, "var(--mud-palette-error)");
    }

    /// <summary>Tags keep today's chip under <c>status=glyph</c>: no glyph rule names a tag selector.</summary>
    [Fact]
    public void StatusGlyph_LeavesTagsAlone()
    {
        string section = CssText.StripComments(CssText.Section(RawCss, "Traits: phase 4", "Traits: chrome"));

        Assert.DoesNotContain("tag-chip", section, StringComparison.Ordinal);
        Assert.DoesNotContain("task-tag", section, StringComparison.Ordinal);
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

        Declares(button, "width: 28px;", "height: 28px;", "border-radius: var(--mud-default-borderradius);");
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

        Declares(box, "border-bottom: 2px solid var(--mud-palette-primary);", "border-radius: var(--mud-default-borderradius);");
        Declares(box, "box-shadow: var(--hu-shadow-card-hover);", "padding-bottom: 40px;");
        Declares(rule, "height: 1px;", "var(--hu-line-region)");
        Declares(input, "border: 0;");
        Declares(focus, "border-bottom-color: var(--mud-palette-primary);");
        Declares(attach, "left: 8px;");
    }

    /// <summary>The phase 4 section adds no <c>!important</c> and no pack token.</summary>
    [Fact]
    public void Phase4Section_AddsNoImportantOrPackToken()
    {
        string section = CssText.StripComments(CssText.Section(RawCss, "Traits: phase 4", "Traits: chrome"));

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

    /// <summary>The phase 4 section's media blocks only.</summary>
    private static string MediaText()
    {
        string section = CssText.StripComments(CssText.Section(RawCss, "Traits: phase 4", "Traits: chrome"));
        int media = section.IndexOf("@media", StringComparison.Ordinal);
        Assert.True(media >= 0, "The phase 4 section has no @media block.");
        return section[media..];
    }

    [GeneratedRegex(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Rule();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

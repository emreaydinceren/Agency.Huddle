using System.Text.RegularExpressions;
using Agency.Huddle.App.Themes;
using MudBlazor.Utilities;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the avatar sizes (24px rows and sidebar, 32px Transcript, 40px cards and tiles, 72px profile) and the presence badge
/// (green circle with a check, amber with a pulse, red with a dash, hollow grey ring; 10px up to 32px avatars and 12px from
/// 40px; a white glyph that keeps 3:1 on its fill in both Huddle Themes; a pulse that is static under reduced motion).
/// bUnit does not compute CSS, so these read the stylesheet source; the running app was measured separately.
/// </summary>
public sealed class AvatarSizesAndPresenceTests
{
    /// <summary>The WCAG 1.4.11 minimum for a graphic against its background.</summary>
    private const double GraphicMinimum = 3.0;

    /// <summary>The darkening every filled badge tone gets so the white glyph reads: the tone mixed this far toward black.</summary>
    private const int BadgeTowardBlackPercent = 20;

    /// <summary>The body of the first rule in <paramref name="css"/> whose selector list is exactly <paramref name="selector"/>.</summary>
    private static string RuleBody(string css, string selector)
    {
        Match match = Regex.Match(css, @"(?<![\w.-])" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        Assert.True(match.Success, $"app.css has no '{selector}' rule.");
        return match.Groups["body"].Value;
    }

    /// <summary>The source text of <c>app.css</c>.</summary>
    private static string Css() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    /// <summary>The Large avatar (a profile) is 72px, the Transcript avatar 32px, and the letters stay 40% of the diameter with a 10px floor.</summary>
    [Fact]
    public void Stylesheet_AvatarSizes_AreTheFourDiameters()
    {
        string css = Css();

        string large = RuleBody(css, ".mud-avatar.mud-avatar-large");
        string transcript = RuleBody(css, ".mud-avatar.avatar-transcript");

        Assert.Contains("width: 72px;", large, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("height: 72px;", large, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("calc(72px * 0.4)", large, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("width: 32px;", transcript, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("height: 32px;", transcript, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("max(10px, calc(32px * 0.4))", transcript, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("max(10px, calc(24px * 0.4))", RuleBody(css, ".mud-avatar.mud-avatar-small"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("max(10px, calc(40px * 0.4))", RuleBody(css, ".mud-avatar.mud-avatar-medium"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>
    /// The Transcript's avatars (a Teammate's Message, a Draft, and the Human's own Message, which a style pack shows
    /// per ADR-0057) carry the 32px class.
    /// </summary>
    [Fact]
    public void MessageList_Avatars_AreTranscriptSized()
    {
        string source = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "MessageList.razor"));

        Assert.Equal(3, Regex.Count(source, @"<ProfileAvatar [^>]*Class=""avatar-transcript""", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)));
    }

    /// <summary>The badge is 10px by default, 12px beside a Medium or Large avatar, and ringed in 2px of the <c>--hu-ring</c> token (Surface by default) on the rim.</summary>
    [Fact]
    public void Stylesheet_Badge_IsTenPixelsAndTwelveFromFortyPixelAvatars()
    {
        string css = Css();

        Assert.Contains("width: 10px;", RuleBody(css, ".agent-dot"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        string twelve = RuleBody(css, ".presence-anchor:has(> .mud-avatar-medium, > .mud-avatar-large) > .agent-dot.on-avatar");
        Assert.Contains("width: 12px;", twelve, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("height: 12px;", twelve, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("0 0 0 2px var(--hu-ring)", RuleBody(css, ".agent-dot.on-avatar"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>Awake (a Turn running) is the info colour, not amber: amber means needs attention, and only Starting keeps it.</summary>
    [Fact]
    public void Stylesheet_AwakeTone_IsInfoNotAmber()
    {
        string css = Css();

        string dot = RuleBody(css, ".agent-dot.awake");
        string card = RuleBody(css, ".task-card-presence.awake .mud-badge.mud-badge-overlap");

        Assert.Contains("color-mix(in srgb, var(--mud-palette-info) 80%, var(--mud-palette-black))", dot, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("color-mix(in srgb, var(--mud-palette-info) 80%, var(--mud-palette-black))", card, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.DoesNotContain("--mud-palette-warning", dot, StringComparison.Ordinal);
        Assert.DoesNotContain("--mud-palette-warning", card, StringComparison.Ordinal);
    }

    /// <summary>Online is green, starting amber, degraded red; offline and asleep are a hollow grey ring, never red.</summary>
    [Fact]
    public void Stylesheet_BadgeTones_AreGreenAmberRedAndAGreyRing()
    {
        string css = Css();

        Assert.Contains("--mud-palette-success", RuleBody(css, ".agent-dot.online"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("--mud-palette-warning", RuleBody(css, ".agent-dot.starting"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("--mud-palette-error", RuleBody(css, ".agent-dot.degraded"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        string ring = RuleBody(css, ".agent-dot.on-avatar.presence-glyph-ring");
        Assert.Contains("--mud-palette-text-secondary", ring, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.DoesNotContain("--mud-palette-error", ring, StringComparison.Ordinal);
    }

    /// <summary>The glyph is white, and the pulse breathes except under <c>prefers-reduced-motion</c>, where it is static.</summary>
    [Fact]
    public void Stylesheet_Glyph_IsWhiteAndThePulseIsStaticUnderReducedMotion()
    {
        string css = Css();

        Assert.Contains("background: var(--mud-palette-white);", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Assert.Contains("animation: presence-pulse 1.8s ease-in-out infinite;", css, StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
        Match reduced = Regex.Match(css, @"@media \(prefers-reduced-motion: reduce\) \{\s*\.agent-dot\.presence-glyph-pulse,[^{]*\{\s*animation: none;", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        Assert.True(reduced.Success, "app.css does not stop the presence pulse under prefers-reduced-motion.");
    }

    /// <summary>
    /// The white glyph keeps 3:1 against each filled tone (success, warning, error and info mixed 20% toward the palette's black), in
    /// Huddle Light and Huddle Dark.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BadgeTones_KeepTheWhiteGlyphAboveThreeToOne(bool dark)
    {
        MudBlazor.Palette palette = dark ? HuddleTheme.Dark() : HuddleTheme.Light();
        MudColor white = new("#ffffff");
        MudColor black = new("#000000");
        MudColor[] tones = [palette.Success, palette.Warning, palette.Error, palette.Info];

        foreach (MudColor tone in tones)
        {
            MudColor fill = AvatarColours.Mix(tone, black, BadgeTowardBlackPercent);

            double ratio = ContrastColour.Ratio(white, fill);

            Assert.True(ratio >= GraphicMinimum, $"{(dark ? "Dark" : "Light")}: {tone.ToString(MudColorOutputFormats.Hex)} gives {ratio:0.00}:1.");
        }
    }
}

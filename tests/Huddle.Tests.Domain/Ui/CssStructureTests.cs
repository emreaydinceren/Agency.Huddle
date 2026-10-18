using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source-text assertions over the stylesheet structure U3 fixes (UX-009, UX-075, UX-V06,
/// UX-V07, UX-V17, UX-V20): where the global MudBlazor overrides live, which animations have a
/// reduced-motion branch, which focus rings are gone and which hover reveals have a touch
/// fallback. Nothing in this suite renders a browser, so these read the CSS text, as
/// <see cref="CssSource"/> does for the other stylesheet suites.
/// </summary>
public sealed class CssStructureTests
{
    private const string GlobalHeader = "/* ---- Global MudBlazor overrides";

    /// <summary>
    /// <c>app.css</c> opens a "Global MudBlazor overrides" section within its first 150 lines, and
    /// the section holds the four rules that restyle MudBlazor for the whole app: the button
    /// <c>.mud-button-root</c>, <c>.mud-drawer</c>, <c>.mud-avatar img</c> and the tab typography.
    /// They used to sit in the Task List, the Task media queries and Teammates (UX-V17).
    /// </summary>
    [Fact]
    public void AppCss_HasGlobalMudBlazorOverridesSectionNearTheTop_HoldingTheFourRules()
    {
        string text = AppCss();

        int start = text.IndexOf(GlobalHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, "app.css has no '/* ---- Global MudBlazor overrides' section header.");
        Assert.True(LineOf(text, start) <= 150, "The Global MudBlazor overrides section is not near the top of app.css.");

        string section = Section(text, start);
        Assert.Matches(@"\.mud-button-root\s*\{", section);
        Assert.Matches(@"\.mud-drawer\s*\{", section);
        Assert.Matches(@"\.mud-avatar img\s*\{", section);
        Assert.Matches(@"\.mud-tab\s*\{", section);
    }

    /// <summary>
    /// None of the three moved rules is declared a second time outside the section, so the
    /// section is the one place to look (UX-V17).
    /// </summary>
    [Fact]
    public void AppCss_GlobalMudBlazorRules_AreDeclaredOnlyInsideTheSection()
    {
        string text = AppCss();
        int start = text.IndexOf(GlobalHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, "app.css has no Global MudBlazor overrides section.");
        string outside = text.Remove(start, Section(text, start).Length);

        Assert.DoesNotMatch(@"(^|\n)\.mud-button-root\s*\{", outside);
        Assert.DoesNotMatch(@"(^|\n)\.mud-drawer\s*\{", outside);
        Assert.DoesNotMatch(@"(^|\n)\.mud-avatar img\s*\{", outside);
    }

    /// <summary>
    /// The tab rail is sentence case like every other label: the global <c>.mud-tab</c> rule sets
    /// <c>text-transform: none</c> (UX-009).
    /// </summary>
    [Fact]
    public void AppCss_TabRule_SetsTextTransformNone()
    {
        string text = AppCss();
        int start = text.IndexOf(GlobalHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, "app.css has no Global MudBlazor overrides section.");

        Match rule = Regex.Match(Section(text, start), @"\.mud-tab\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

        Assert.True(rule.Success, "The section has no '.mud-tab' rule.");
        Assert.Matches(@"text-transform:\s*none\s*;", rule.Groups["body"].Value);
    }

    /// <summary>
    /// The draft caret blinks forever, so a <c>prefers-reduced-motion: reduce</c> branch in
    /// <c>app.css</c> stops it (UX-V06).
    /// </summary>
    [Fact]
    public void AppCss_DraftCaret_HasReducedMotionBranch()
    {
        string blocks = string.Join("\n", ReducedMotionBlocks(AppCss()));

        Assert.Matches(@"\.draft-caret\s*\{[^}]*animation:\s*none", blocks);
    }

    /// <summary>
    /// The reconnect modal's slide-up and its rejoining rings each have a reduced-motion branch
    /// (UX-V06).
    /// </summary>
    [Fact]
    public void ReconnectModalCss_SlideUpAndRejoiningRings_HaveReducedMotionBranch()
    {
        string blocks = string.Join("\n", ReducedMotionBlocks(File.ReadAllText(
            CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "ReconnectModal.razor.css"))));

        Assert.Matches(@"#components-reconnect-modal[^{]*\{[^}]*animation:\s*none", blocks);
        Assert.Matches(@"\.components-rejoining-animation div[^{]*\{[^}]*animation:\s*none", blocks);
    }

    /// <summary>
    /// No focus ring in <c>app.css</c> is drawn in the accent (<c>--mud-palette-primary</c>): the
    /// accent measured 1.63:1 in Huddle Dark. The single page-wide ring stands (UX-V07).
    /// </summary>
    [Fact]
    public void AppCss_FocusVisibleRules_NeverDrawAnAccentRing()
    {
        string text = AppCss();

        foreach (Match rule in Regex.Matches(text, @"(?<sel>[^{}]*:focus-visible[^{}]*)\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)))
        {
            string body = rule.Groups["body"].Value;
            bool accentRing = Regex.IsMatch(body, @"outline:[^;]*--mud-palette-primary\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            Assert.False(accentRing, $"Accent focus ring left on '{rule.Groups["sel"].Value.Trim()}'.");
        }
    }

    /// <summary>
    /// The Avatar Library thumbnail's hover is a background change, not the focus ring: it
    /// carries no <c>outline</c> (UX-V07).
    /// </summary>
    [Fact]
    public void AppCss_AvatarLibraryThumbHover_DrawsNoOutline()
    {
        Match rule = Regex.Match(AppCss(), @"\.avatar-library-thumb:hover\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

        Assert.True(rule.Success, "app.css has no '.avatar-library-thumb:hover' rule.");
        Assert.DoesNotContain("outline", rule.Groups["body"].Value, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.Contains("background", rule.Groups["body"].Value, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// The error banner sits on MudBlazor's snackbar layer, above the drawer, instead of the
    /// literal 1000 that put its first 240px under the sidebar (UX-V20).
    /// </summary>
    [Fact]
    public void MainLayoutCss_ErrorBanner_UsesSnackbarZIndexToken()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor.css"));

        Assert.Contains("z-index: var(--mud-zindex-snackbar)", text, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
        Assert.DoesNotContain("z-index: 1000", text, StringComparison.Ordinal); // contains-ok: stylesheet source text, not markup
    }

    /// <summary>
    /// One shared <c>@media (hover: none)</c> block makes every hover-revealed control visible on
    /// a touch screen: the Room menu button, the Tab close and the Tab actions (UX-075).
    /// </summary>
    /// <param name="selector">A class that is revealed on hover.</param>
    [Theory]
    [InlineData(".hover-reveal-menu-button")]
    [InlineData(".tab-close")]
    [InlineData(".tab-actions")]
    public void AppCss_HoverNoneBlock_RevealsEveryHoverRevealedControl(string selector)
    {
        string blocks = string.Join("\n", MediaBlocks(AppCss(), "(hover: none)"));

        Assert.Matches(Regex.Escape(selector) + @"[^{]*\{[^}]*opacity:\s*1", blocks);
    }

    private static string AppCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    private static int LineOf(string text, int index) =>
        text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>The text from <paramref name="start"/> to the next section header.</summary>
    private static string Section(string text, int start)
    {
        int next = text.IndexOf("/* ---- ", start + 1, StringComparison.Ordinal);
        return next < 0 ? text[start..] : text[start..next];
    }

    private static List<string> ReducedMotionBlocks(string text) =>
        MediaBlocks(text, "(prefers-reduced-motion: reduce)");

    /// <summary>The bodies of every <c>@media</c> block whose condition is <paramref name="condition"/>.</summary>
    private static List<string> MediaBlocks(string text, string condition)
    {
        List<string> bodies = [];
        string marker = "@media " + condition;
        int at = text.IndexOf(marker, StringComparison.Ordinal);
        while (at >= 0)
        {
            int open = text.IndexOf('{', at);
            int depth = 0;
            int i = open;
            for (; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
            }

            bodies.Add(text[(open + 1)..i]);
            at = text.IndexOf(marker, i, StringComparison.Ordinal);
        }

        return bodies;
    }
}

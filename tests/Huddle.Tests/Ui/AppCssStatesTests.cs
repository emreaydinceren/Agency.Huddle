using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source-text assertions over the state and utility rules U4 fixes in <c>app.css</c>: the one
/// hairline token for chrome (UX-061), the hover token (UX-064), the selected tints and the drop
/// target (UX-065, UX-066), the muting opacities (UX-067), the shared utilities (UX-063, UX-R21,
/// UX-R22, UX-069), the <c>--task-ref-dot</c> indirection (UX-V15), the dead and duplicate rules
/// (UX-R30) and the inline-confirm and empty-state padding (UX-076, UX-077). Nothing renders a
/// browser here; these read the stylesheet text, as <see cref="CssSource"/> does for the others.
/// </summary>
public sealed partial class AppCssStatesTests
{
    /// <summary>
    /// The chrome lines are one hairline in <c>lines-default</c>: the drawer edge, the room header,
    /// the top bar and the tab-group separator. Only the active tab indicator keeps a thicker inset
    /// shadow, which is an indicator and not a line (UX-061).
    /// </summary>
    [Fact]
    public void AppCss_ChromeLines_AreOneHairlineOfLinesDefault()
    {
        string css = AppCss();

        Assert.Matches(@"\.mud-drawer\s*\{[^}]*border-inline-end:\s*1px solid var\(--mud-palette-lines-default\)", css);
        Assert.Matches(@"\.room-header\s*\{[^}]*border-bottom:\s*1px solid var\(--mud-palette-lines-default\)", css);
        Assert.Matches(@"\.mud-appbar\.top-bar\s*\{[^}]*border-bottom:\s*1px solid var\(--mud-palette-lines-default\)", css);
        Assert.Matches(@"\.tab-group \+ \.tab-group\s*\{[^}]*border-left:\s*1px solid var\(--mud-palette-lines-default\)", css);
        Assert.DoesNotMatch(@"[2-9]px solid var\(--mud-palette-lines-default\)", css);
    }

    /// <summary>
    /// <c>--mud-palette-table-hover</c> is a table's own row hover. A rule that uses it outside a
    /// <c>.mud-table</c> selector takes the wrong colour in every theme; the hover of a button,
    /// tile or panel item is <c>action-default-hover</c> (UX-064).
    /// </summary>
    [Fact]
    public void AppCss_TableHoverToken_AppearsOnlyInsideATable()
    {
        foreach (Match rule in CssRules().Matches(AppCss()))
        {
            if (rule.Groups["body"].Value.Contains("--mud-palette-table-hover", StringComparison.Ordinal))
            {
                Assert.Matches("mud-table", rule.Groups["selector"].Value);
            }
        }
    }

    /// <summary>
    /// A floating panel anchored to a control (Invite, the <c>@</c> picker) is one shared class: a
    /// card border, the popover-level elevation and the popover layer (UX-063, DS "Elevation and
    /// layering").
    /// </summary>
    [Fact]
    public void AppCss_FloatingPanel_IsOneElevatedBorderedLayer()
    {
        string css = AppCss();

        Assert.Matches(@"\.floating-panel\s*\{[^}]*box-shadow:\s*var\(--mud-elevation-4\)", css);
        Assert.Matches(@"\.floating-panel\s*\{[^}]*border:\s*1px solid var\(--mud-palette-divider\)", css);
        Assert.Matches(@"\.floating-panel\s*\{[^}]*z-index:\s*var\(--mud-zindex-popover\)", css);
    }

    /// <summary>
    /// A drop target is drawn one way: a 1px dashed primary line. The composer, the ghost bucket
    /// and the library tree no longer carry their own 2px or outline treatment (UX-066).
    /// </summary>
    [Fact]
    public void AppCss_DropTarget_IsOneDashedPrimaryLine()
    {
        string css = AppCss();

        Assert.Matches(@"\.drop-target\s*\{[^}]*border:\s*1px dashed var\(--mud-palette-primary\)", css);
        Assert.DoesNotMatch("2px dashed", css);
        Assert.DoesNotMatch(@"outline:\s*1px dashed", css);
    }

    /// <summary>
    /// The three shared utilities exist with their declarations: truncation is one class, and
    /// screen-reader-only text is one class (UX-R21).
    /// </summary>
    [Fact]
    public void AppCss_TruncateAndVisuallyHidden_ExistAsGlobalUtilities()
    {
        string css = AppCss();

        Assert.Matches(@"\.truncate\s*\{[^}]*overflow:\s*hidden", css);
        Assert.Matches(@"\.truncate\s*\{[^}]*text-overflow:\s*ellipsis", css);
        Assert.Matches(@"\.truncate\s*\{[^}]*white-space:\s*nowrap", css);
        Assert.Matches(@"\.visually-hidden\s*\{[^}]*position:\s*absolute", css);
        Assert.Matches(@"\.visually-hidden\s*\{[^}]*clip:\s*rect\(0 0 0 0\)", css);
    }

    /// <summary>
    /// The repeated blocks are shared classes: the dotted reference link, the room strip, the status
    /// line, the group heading, the bordered panel card and the preformatted preview block (UX-R22).
    /// </summary>
    [Fact]
    public void AppCss_RepeatedBlocks_AreSharedClasses()
    {
        string css = AppCss();

        Assert.Matches(@"\.ref-link\s*\{", css);
        Assert.Matches(@"\.room-strip\s*\{", css);
        Assert.Matches(@"\.status-text\s*\{", css);
        Assert.Matches(@"\.group-heading\s*\{", css);
        Assert.Matches(@"\.panel-card\s*\{", css);
        Assert.Matches(@"\.preview-block\s*\{", css);
    }

    /// <summary>
    /// The task reference dot is six explicit rules, one per status, each with its own colour. The
    /// custom property <c>--task-ref-dot</c> with its fallback is gone, because every
    /// <c>var()</c> must name a MudBlazor variable (UX-V15).
    /// </summary>
    [Fact]
    public void AppCss_TaskRefDot_IsSixExplicitRulesWithoutACustomProperty()
    {
        string css = AppCss();

        Assert.DoesNotMatch("--task-ref-dot", css);
        Assert.DoesNotMatch(@"task-ref\[class\*=""task-ref-status-""\]", css);
        Assert.Matches(@"\.task-ref-status-info::before\s*\{[^}]*background:\s*var\(--mud-palette-info\)", css);
        Assert.Matches(@"\.task-ref-status-warning::before\s*\{[^}]*background:\s*var\(--mud-palette-warning\)", css);
        Assert.Matches(@"\.task-ref-status-secondary::before\s*\{[^}]*background:\s*var\(--mud-palette-secondary\)", css);
        Assert.Matches(@"\.task-ref-status-success::before\s*\{[^}]*background:\s*var\(--mud-palette-success\)", css);
        Assert.Matches(@"\.task-ref-status-error::before\s*\{[^}]*background:\s*var\(--mud-palette-error\)", css);
        Assert.Matches(@"\.task-ref-status-default::before\s*\{[^}]*background:\s*var\(--mud-palette-lines-default\)", css);
    }

    /// <summary>
    /// De-emphasis is by colour, not opacity: the 0.5, 0.7, 0.75 and 0.92 opacities are gone, and
    /// 0.6 is kept only for an archived or gone item (UX-067, DS "Colour" States).
    /// </summary>
    [Fact]
    public void AppCss_Muting_UsesColourExceptArchivedOrGoneAtPointSixOpacity()
    {
        string css = AppCss();

        Assert.DoesNotMatch(@"opacity:\s*(0\.5|0\.7|0\.75|0\.92)\b", css);
        foreach (Match rule in CssRules().Matches(css))
        {
            if (Regex.IsMatch(rule.Groups["body"].Value, @"opacity:\s*0\.6\b"))
            {
                string selector = rule.Groups["selector"].Value;
                Assert.True(
                    selector.Contains("archived", StringComparison.Ordinal) || selector.Contains("gone", StringComparison.Ordinal),
                    $"Opacity 0.6 on '{selector.Trim()}': only archived or gone items mute by opacity.");
            }
        }
    }

    /// <summary>
    /// A selected tint is one of two steps over <c>surface</c>: 14% at rest and 24% on hover. Never
    /// 16% or 30%, and never over <c>transparent</c> (UX-065, DS "Colour" States).
    /// </summary>
    [Fact]
    public void AppCss_SelectedTints_AreFourteenAndTwentyFourPercentOverSurface()
    {
        string css = AppCss();

        Assert.DoesNotMatch(@"color-mix\(in srgb, var\(--mud-palette-primary\) (16|30)%", css);
        Assert.DoesNotMatch(@"primary\) \d+%, transparent\)", css);
        Assert.Matches(@"color-mix\(in srgb, var\(--mud-palette-primary\) 24%, var\(--mud-palette-surface\)\)", css);
    }

    /// <summary>
    /// The inline confirm is styled by its callers for padding only: neither the room-list nor the
    /// team-members caller repeats the flex layout or the gap (UX-076).
    /// </summary>
    [Fact]
    public void AppCss_RoomListConfirm_SetsPaddingOnly()
    {
        string css = AppCss();

        Assert.Matches(@"\.room-list-confirm\s*\{", css);
        Assert.DoesNotMatch(@"\.room-list-confirm\s*\{[^}]*(gap:|display:)", css);
    }

    /// <summary>
    /// The full-pane empty states share one rule with one padding, so the tab and library panes
    /// cannot drift apart (UX-077, DS "Patterns worth copying").
    /// </summary>
    [Fact]
    public void AppCss_FullPaneEmptyStates_ShareOneRule()
    {
        Assert.Matches(@"\.tab-empty,\s*\.library-empty-pane\s*\{[^}]*padding:\s*2rem", AppCss());
    }

    /// <summary>
    /// The dead <c>.invite-filter</c> rule is deleted, and no run of two or more blank lines is left
    /// between rules (UX-R30).
    /// </summary>
    [Fact]
    public void AppCss_HasNoDeadRuleAndNoBlankLineRuns()
    {
        string css = AppCss();

        Assert.DoesNotMatch(@"\.invite-filter\b", css);
        Assert.DoesNotMatch(@"\r?\n[ \t]*\r?\n[ \t]*\r?\n", css);
    }

    private static string AppCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    [GeneratedRegex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")]
    private static partial Regex CssRules();
}

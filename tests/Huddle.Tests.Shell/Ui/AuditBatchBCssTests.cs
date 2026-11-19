using System.Text.RegularExpressions;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Visual audit, batch B: the Room notice column and the global alert look (A3), a Human's references under the
/// bubble (A8c), the sidebar's 24px leading slot and destination rows (A7), the Needs you row scale (A8a) and the
/// breadcrumb's start (A8b). A browser is not rendered here, so the stylesheet is pinned as source text.
/// </summary>
public sealed partial class AuditBatchBCssTests
{
    /// <summary>The severities the app draws an alert in.</summary>
    public static TheoryData<string> Severities => ["error", "warning", "info", "success"];

    /// <summary>Every <c>MudAlert</c> message is body text in <c>TextPrimary</c>, whatever its severity: only the icon and the bar carry the status colour.</summary>
    [Fact]
    public void AppCss_AlertMessage_IsTextPrimary()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".mud-alert[class*=\"mud-alert-text-\"] .mud-alert-message");

        Assert.Matches(@"color:\s*var\(--mud-palette-text-primary\)", declarations);
        Assert.Matches(@"color:\s*var\(--mud-palette-text-primary\)", AuditBatchBCssTests.DeclarationsFor(".mud-alert[class*=\"mud-alert-outlined-\"] .mud-alert-message"));
    }

    /// <summary>An alert carries its status as a 3px leading bar in the severity's palette colour, in both the text and the outlined variant.</summary>
    [Theory]
    [MemberData(nameof(Severities))]
    public void AppCss_Alert_HasA3pxLeadingBarInTheSeverityColour(string severity)
    {
        string expected = @"border-inline-start:\s*3px solid var\(--mud-palette-" + severity + @"\)";

        Assert.Matches(expected, AuditBatchBCssTests.DeclarationsFor(".mud-alert.mud-alert-text-" + severity));
        Assert.Matches(expected, AuditBatchBCssTests.DeclarationsFor(".mud-alert.mud-alert-outlined-" + severity));
    }

    /// <summary>The Offline item no longer paints its sentence in the error colour.</summary>
    [Fact]
    public void AppCss_MemberHealthItem_IsNotErrorColoured()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".member-health-alert-item");

        Assert.DoesNotContain("--mud-palette-error", declarations, StringComparison.Ordinal);
    }

    /// <summary>A notice in a Room takes the Transcript's 760px column, centred, with the same 24px inset as the Transcript and the Composer.</summary>
    [Fact]
    public void AppCss_RoomNoticeColumn_IsTheTranscriptColumn()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".room-notice-column");

        Assert.Matches(@"max-width:\s*760px", declarations);
        Assert.Matches(@"margin-inline:\s*auto", declarations);
        Assert.Matches(@"translate:\s*calc\(var\(--room-scrollbar\) / -2\) 0", declarations);
        Assert.Matches(@"width:\s*calc\(100% - 48px - var\(--room-scrollbar\)\)", declarations);
    }

    /// <summary>
    /// The Transcript, the notices, the Composer and the Reference picker share one left edge: the list always reserves its
    /// scrollbar gutter, and the Composer, the notices and the picker take that same width off their right.
    /// </summary>
    [Fact]
    public void AppCss_TranscriptNoticesComposerAndPicker_ShareOneColumn()
    {
        Assert.Matches(@"scrollbar-gutter:\s*stable", AuditBatchBCssTests.DeclarationsFor(".room-column > .message-list"));
        Assert.Matches(@"--room-scrollbar:\s*var\(--scrollbar-size\)", AuditBatchBCssTests.DeclarationsFor(":root"));
        Assert.Matches(@"padding:\s*16px calc\(24px \+ var\(--room-scrollbar\)\) 16px 24px", AuditBatchBCssTests.DeclarationsFor(".composer"));

        string picker = AuditBatchBCssTests.DeclarationsFor(".composer-picker");
        Assert.Matches(@"left:\s*max\(24px, calc\(50% - 380px - var\(--room-scrollbar\) / 2\)\)", picker);
        Assert.Matches(@"width:\s*min\(760px, calc\(100% - 48px - var\(--room-scrollbar\)\)\)", picker);
    }

    /// <summary>The Budget note is a caption (12px) in the notice column, so it starts where the Transcript does.</summary>
    [Fact]
    public void BudgetNote_IsACaptionInTheNoticeColumn()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".budget-note");
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "Chat.razor"));

        Assert.Matches(@"font-size:\s*var\(--mud-typography-caption-size\)", declarations);
        Assert.DoesNotContain("padding", declarations, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(markup, @"Class=""budget-note room-notice-column"""));
    }

    /// <summary>A picker row is one line of card with its note at the end of the line, and every kind's icon takes the same 24px slot.</summary>
    [Fact]
    public void AppCss_PickerRow_KeepsTheNoteOnTheRowAndOneTextColumn()
    {
        Assert.Matches(@"display:\s*flex", AuditBatchBCssTests.DeclarationsFor(".composer-picker-item"));
        Assert.Matches(@"width:\s*24px", AuditBatchBCssTests.DeclarationsFor(".composer-picker .search-card-icon"));

        string note = AuditBatchBCssTests.DeclarationsFor(".composer-picker-note");
        Assert.DoesNotContain("display: block", note, StringComparison.Ordinal);
        Assert.Matches(@"font-size:\s*var\(--mud-typography-caption-size\)", note);
    }

    /// <summary>The Room page puts the column class on the Offline notice, the budget prompt and the reply note.</summary>
    [Theory]
    [InlineData("member-health-alert")]
    [InlineData("budget-prompt")]
    [InlineData("reply-note")]
    public void ChatMarkup_RoomNotices_TakeTheColumnClass(string noticeClass)
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "Chat.razor"));

        Assert.Matches(@"<(?:MudAlert|Notice) [^>]*Class=""" + noticeClass + @" room-notice-column""", markup);
    }

    /// <summary>A Human's row stacks the bubble above its reference chips and right-aligns both, so the chips sit under the bubble.</summary>
    [Fact]
    public void AppCss_HumanRow_StacksBubbleAboveReferencesAtTheEnd()
    {
        string row = AuditBatchBCssTests.DeclarationsFor(".message-row.human");
        string references = AuditBatchBCssTests.DeclarationsFor(".message-row.human .message-references");

        Assert.Matches(@"flex-direction:\s*column", row);
        Assert.Matches(@"align-items:\s*flex-end", row);
        Assert.Matches(@"justify-content:\s*flex-end", references);
    }

    /// <summary>The bubble keeps its width cap, and the Human's row still holds the bubble first and the references second.</summary>
    [Fact]
    public void HumanRow_KeepsTheBubbleCapAndTheBubbleBeforeTheReferences()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "MessageList.razor"));

        Assert.Matches(@"max-width:\s*min\(75%, calc\(75ch \+ 32px\)\)", AuditBatchBCssTests.DeclarationsFor(".message-bubble"));
        Assert.Matches(@"<MarkdownView Class=""message-bubble message-body""[^\r\n]*/>\s*<MessageReferences Message=""@message"" />\s*</div>", markup);
    }

    /// <summary>An icon in any left-sidebar row, a section header's included, is centred in the 24px leading slot, the width of a small avatar, so its centre is on one line whatever its size.</summary>
    [Fact]
    public void AppCss_SidebarRowIcon_IsCentredInA24pxSlot()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".sidebar-views .mud-nav-link > .mud-nav-link-icon");

        Assert.Matches(@"box-sizing:\s*content-box", declarations);
        Assert.Matches(@"padding-inline:\s*calc\(\(var\(--nav-slot\) - 1em\) / 2\)", declarations);
        Assert.Matches(@"margin:\s*0", declarations);
        Assert.Matches(@"--nav-slot:\s*24px", CssText.AppCss(CssNewlines.AsIs));
    }

    /// <summary>
    /// A label after an icon starts one gap after the slot, a label whose mark sits inside it (a 1:1 Room's avatar) starts
    /// flush, and MudBlazor's own 12px text inset is gone. A 1:1 Room's avatar keeps 8px from its name.
    /// </summary>
    [Fact]
    public void AppCss_SidebarRowLabelAfterIcon_DropsTheTextInset()
    {
        Assert.Matches(@"margin-inline-start:\s*var\(--nav-gap\)", AuditBatchBCssTests.DeclarationsFor(".sidebar-views .mud-nav-link > .mud-nav-link-icon + .mud-nav-link-text"));
        Assert.Matches(@"margin-inline-start:\s*0", CssText.RuleBody(CssText.AppCss(CssNewlines.AsIs), ".sidebar-views .mud-nav-link > .mud-nav-link-text:has(> .room-row-avatar, > .needs-you-icons)"));
        // The 32px avatar overhangs the 24px slot's start by 4px and keeps 8px to its name; the #475 alignment with a group Room's
        // name left no gap at all, so the owner traded it for the space (2026-11-10).
        Assert.Matches(@"margin-inline:\s*calc\(\(var\(--nav-slot\) - 32px\) / 2\) 8px;", AuditBatchBCssTests.DeclarationsFor(".room-row-avatar"));
    }

    /// <summary>The destination rows (Agents, Library) keep the section header's icon geometry, so they are not indented like a group's children.</summary>
    [Fact]
    public void AppCss_DestinationRowIcon_IsNotInTheGroupSlot()
    {
        Assert.DoesNotContain(".nav-row-link .mud-nav-link .mud-nav-link-icon", CssText.AppCss(CssNewlines.AsIs), StringComparison.Ordinal);
    }

    /// <summary>A destination row (Agents, Library) is a 32px, 13px, body-colour row, not a section header.</summary>
    [Fact]
    public void AppCss_DestinationRow_IsAnOrdinaryRow()
    {
        string link = AuditBatchBCssTests.DeclarationsFor(".nav-row-link .mud-nav-link");
        string text = AuditBatchBCssTests.DeclarationsFor(".nav-row-link .mud-nav-link .mud-nav-link-text");

        Assert.Matches(@"min-height:\s*var\(--nav-row-height\)", link);
        Assert.Matches(@"font-size:\s*var\(--mud-typography-body2-size\)", text);
        Assert.Matches(@"font-weight:\s*400", text);
        Assert.Matches(@"color:\s*var\(--mud-palette-text-primary\)", text);
    }

    /// <summary>Only a group with children keeps the section-header look, and the old <c>nav-section-link</c> class is gone from the CSS and from the sidebar.</summary>
    [Fact]
    public void Sidebar_OnlyGroupsAreSectionHeaders()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotContain("nav-section-link", css, StringComparison.Ordinal);
        Assert.Matches(@"\.mud-nav-group > \.mud-nav-link \.mud-nav-link-text\s*\{[^}]*font-weight:\s*600", css);
    }

    /// <summary>The base nav label is 13px (body2), not subtitle2, so no row outside a group - the Needs you rows included - is bigger than the row scale.</summary>
    [Fact]
    public void AppCss_NavLinkText_IsBody2()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".mud-nav-link-text");

        Assert.Matches(@"font-size:\s*var\(--mud-typography-body2-size\)", declarations);
        Assert.DoesNotContain("subtitle2", declarations, StringComparison.Ordinal);
    }

    /// <summary>A Needs you row is a 13px label over a 12px meta line, whatever size the nav text rule gives its container.</summary>
    [Fact]
    public void AppCss_NeedsYouRow_FollowsTheRowScale()
    {
        Assert.Matches(@"font-size:\s*var\(--mud-typography-body2-size\)", AuditBatchBCssTests.DeclarationsFor(".needs-you-row .mud-nav-link-text"));
        Assert.Matches(@"font-size:\s*var\(--mud-typography-caption-size\)", AuditBatchBCssTests.DeclarationsFor(".needs-you-detail"));
    }

    /// <summary>The breadcrumb starts at the window's left edge, inside the toolbar's own 16px gutter: no inset past the drawer's width.</summary>
    [Fact]
    public void AppCss_TopBarStart_StartsAtTheWindowEdge()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".top-bar-start");

        Assert.DoesNotContain("padding-inline-start", declarations, StringComparison.Ordinal);
        Assert.DoesNotContain("--mud-drawer-width-left", declarations, StringComparison.Ordinal);
        // The end cell never gets less than its own content (the pill and the buttons), so the search box cannot run under them (#466).
        Assert.Matches(@"grid-template-columns:\s*minmax\(8rem, 1fr\) minmax\(8rem, 32rem\) minmax\(max-content, 1fr\)", AuditBatchBCssTests.DeclarationsFor(".top-bar-grid"));
    }

    /// <summary>Search group headings are sentence case, like the Sidebar's section headers, not upper case with tracking (#477).</summary>
    [Fact]
    public void AppCss_SearchGroupHeading_IsSentenceCase()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".search-group-heading");

        Assert.DoesNotContain("uppercase", declarations, StringComparison.Ordinal);
        Assert.DoesNotContain("letter-spacing", declarations, StringComparison.Ordinal);
    }

    /// <summary>Below 1100px the Right sidebar floats over the Workspace instead of pushing it, so the Workspace keeps its width (#466).</summary>
    [Fact]
    public void AppCss_NarrowWindow_RightSidebarOverlaysTheWorkspace()
    {
        Assert.Matches(
            @"@media \(max-width: 1099px\) \{\s*\.mud-layout\.mud-drawer-open-persistent-right \.mud-main-content\.main-column \{\s*margin-right:\s*0;",
            CssText.AppCss(CssNewlines.AsIs));
    }

    /// <summary>The New chat panel is fixed and anchored to its link, so the scrolling folder around the link cannot clip it or leave it below the window (#465).</summary>
    [Fact]
    public void AppCss_NewChatPanel_IsAnchoredAndFlipsAboveTheLink()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.Matches(@"\.new-chat\s*\{[^}]*anchor-name:\s*--new-chat", css);
        Assert.Matches(@"@supports \(position-anchor: --new-chat\) \{\s*\.new-chat-panel \{[^}]*position:\s*fixed;[^}]*top:\s*anchor\(bottom\);[^}]*position-try-fallbacks:\s*flip-block", css);
    }

    /// <summary>
    /// The declarations of every rule in <c>app.css</c> whose selector list holds exactly <paramref name="selector"/> as one
    /// of its comma-separated selectors, joined; fails when no rule does.
    /// </summary>
    private static string DeclarationsFor(string selector)
    {
        List<string> bodies = [];
        foreach (Match match in RulePattern().Matches(CssText.AppCss(CssNewlines.AsIs)))
        {
            string[] selectors = match.Groups["selectors"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (selectors.Contains(selector, StringComparer.Ordinal))
            {
                bodies.Add(match.Groups["body"].Value);
            }
        }

        Assert.True(bodies.Count > 0, $"app.css has no rule '{selector}'.");
        return string.Join("\n", bodies);
    }

    [GeneratedRegex(@"(?m)^(?<selectors>[^\s/}@][^{};]*?)\s*\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex RulePattern();
}

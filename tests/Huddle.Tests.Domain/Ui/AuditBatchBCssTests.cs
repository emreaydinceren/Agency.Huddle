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
        Assert.Matches(@"width:\s*calc\(100% - 48px\)", declarations);
    }

    /// <summary>The Room page puts the column class on the Offline notice, the budget prompt and the reply note.</summary>
    [Theory]
    [InlineData("member-health-alert")]
    [InlineData("budget-prompt")]
    [InlineData("reply-note")]
    public void ChatMarkup_RoomNotices_TakeTheColumnClass(string noticeClass)
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Pages", "Chat.razor"));

        Assert.Matches(@"<MudAlert [^>]*Class=""" + noticeClass + @" room-notice-column""", markup);
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
        Assert.Matches(@"message-bubble message-body"">[^\r\n]*</div>\s*<MessageReferences Message=""@message"" />\s*</div>", markup);
    }

    /// <summary>An icon in a sidebar row is centred in a 24px leading slot, the width of a small avatar, so labels at one level start at one x.</summary>
    [Fact]
    public void AppCss_SidebarRowIcon_IsCentredInA24pxSlot()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".mud-nav-group .mud-collapse-container .mud-nav-link .mud-nav-link-icon");

        Assert.Matches(@"box-sizing:\s*content-box", declarations);
        Assert.Matches(@"padding-inline:\s*calc\(\(24px - var\(--icon-size-row\)\) / 2\)", declarations);
        Assert.Matches(@"margin-inline-start:\s*12px", declarations);
        Assert.Matches(@"margin-inline-end:\s*8px", declarations);
    }

    /// <summary>
    /// A group row's icon slot starts where a 1:1 Room's avatar does: the avatar sits after MudBlazor's 12px text inset, so
    /// the icon takes that 12px as its own start margin and the label after an icon drops the inset.
    /// </summary>
    [Fact]
    public void AppCss_SidebarRowLabelAfterIcon_DropsTheTextInset()
    {
        string declarations = AuditBatchBCssTests.DeclarationsFor(".mud-nav-group .mud-collapse-container .mud-nav-link .mud-nav-link-icon + .mud-nav-link-text");

        Assert.Matches(@"margin-inline-start:\s*0", declarations);
    }

    /// <summary>The destination rows (Teammates, Library) keep the section header's icon geometry, so they are not indented like a group's children.</summary>
    [Fact]
    public void AppCss_DestinationRowIcon_IsNotInTheGroupSlot()
    {
        Assert.DoesNotContain(".nav-row-link .mud-nav-link .mud-nav-link-icon", CssText.AppCss(CssNewlines.AsIs), StringComparison.Ordinal);
    }

    /// <summary>A destination row (Teammates, Library) is a 32px, 13px, body-colour row, not a section header.</summary>
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

    /// <summary>Only a group with children keeps the section-header look, and the old <c>nav-section-link</c> class is gone from the CSS and from both links.</summary>
    [Fact]
    public void Sidebar_OnlyGroupsAreSectionHeaders()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));
        string library = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Library", "LibraryNavLink.razor"));

        Assert.DoesNotContain("nav-section-link", css, StringComparison.Ordinal);
        Assert.Matches(@"<NavTabLink Href=""/teammates""[^>]*Class=""nav-row-link""", layout);
        Assert.Matches(@"<NavTabLink Href=""/library""[^>]*Class=""nav-row-link""", library);
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
        Assert.Matches(@"grid-template-columns:\s*minmax\(0, 1fr\) minmax\(0, 32rem\) minmax\(0, 1fr\)", AuditBatchBCssTests.DeclarationsFor(".top-bar-grid"));
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

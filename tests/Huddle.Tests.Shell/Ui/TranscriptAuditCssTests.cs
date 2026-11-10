using System.Text.RegularExpressions;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Visual audit, area B: the Draft's caret (#483), the Question card's spacing and button height (#488), the list
/// indent in a Message (#489) and the sender and context type (#490). A browser is not rendered here, so the
/// stylesheet is pinned as source text; the browser check is the proof of the measured values.
/// </summary>
public sealed partial class TranscriptAuditCssTests
{
    /// <summary>The caret has a line-box height, so its border is painted; at 0px high it never was.</summary>
    [Fact]
    public void AppCss_DraftCaret_HasAHeight()
    {
        Assert.Matches(@"height:\s*1em", TranscriptAuditCssTests.DeclarationsFor(".draft-caret"));
    }

    /// <summary>A list in a Message is indented 24px, so its markers sit in the text column and not in the avatar gutter.</summary>
    [Fact]
    public void AppCss_MessageBodyLists_AreIndented()
    {
        Assert.Matches(@"padding-left:\s*24px", TranscriptAuditCssTests.DeclarationsFor(".message-body ul"));
        Assert.Matches(@"padding-left:\s*24px", TranscriptAuditCssTests.DeclarationsFor(".message-body ol"));
    }

    /// <summary>The sender is 13px (body2) in every Trait: the marks no longer raise it to body size.</summary>
    [Fact]
    public void AppCss_MessageSender_IsNeverRaisedAboveBody2()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        string marks = CssText.Section(css, "Marks: names, references, mentions, quotes and tool summaries", "Traits: transcript and composer");

        Assert.DoesNotContain(".message-sender", marks, StringComparison.Ordinal);
    }

    /// <summary>The Members row's context figure is weight 400 like the Transcript's, outweighing the sidebar Trait's 500.</summary>
    [Fact]
    public void AppCss_MembersContext_IsWeight400()
    {
        string declarations = TranscriptAuditCssTests.DeclarationsFor(":root .members-body .members-context.sidebar-row-meta");

        Assert.Matches(@"font-weight:\s*400", declarations);
    }

    /// <summary>An answer option is the same 28px as the Dismiss and Send buttons, and the options sit 12px under the question.</summary>
    [Fact]
    public void AppCss_QuestionCard_HasOneButtonHeightAndRoomUnderTheQuestion()
    {
        Assert.Matches(@"min-height:\s*28px", TranscriptAuditCssTests.DeclarationsFor(".question-card-option"));
        Assert.Matches(@"margin-top:\s*12px", TranscriptAuditCssTests.DeclarationsFor(".question-card-options"));
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

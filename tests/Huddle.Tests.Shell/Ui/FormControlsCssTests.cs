using System.Text.RegularExpressions;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Visual audit, area I: the value of an outlined select (#463) and the options of a select (#492). A browser is not
/// rendered here, so the stylesheet is pinned as source text; the browser check is the proof of the measured values.
/// </summary>
public sealed partial class FormControlsCssTests
{
    private const string SelectValueSelector = ".mud-input.mud-input-outlined > div.mud-input-slot.mud-input-root-outlined.mud-select-input";

    /// <summary>A select's value is centred in the field, so it no longer sits on the top border; one rule serves every field height and needs no !important.</summary>
    [Fact]
    public void AppCss_SelectValue_IsCentredInTheField()
    {
        string declarations = FormControlsCssTests.DeclarationsFor(FormControlsCssTests.SelectValueSelector);

        Assert.Matches(@"align-content:\s*center", declarations);
        Assert.DoesNotContain("!important", declarations, StringComparison.Ordinal);
    }

    /// <summary>The Task edit page no longer carries its own select-centring rule: the global one covers it.</summary>
    [Fact]
    public void AppCss_TaskEditPage_HasNoSelectCentringRuleOfItsOwn()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotContain(".task-edit-page .task-detail-prop-value .mud-input.mud-input-outlined div.mud-select-input.mud-input-slot", css, StringComparison.Ordinal);
    }

    /// <summary>A select option is a dense row with body2 text, the size of a menu item, not a 46px body1 row.</summary>
    [Fact]
    public void AppCss_SelectOptions_AreDenseBody2Rows()
    {
        Assert.Matches(@"padding-block:\s*2px", FormControlsCssTests.DeclarationsFor(".mud-popover .mud-list-item[role=\"option\"]"));

        string text = FormControlsCssTests.DeclarationsFor(".mud-popover .mud-list-item[role=\"option\"] .mud-list-item-text .mud-typography");
        Assert.Matches(@"font-size:\s*var\(--mud-typography-body2-size\)", text);
        Assert.Matches(@"line-height:\s*var\(--mud-typography-body2-lineheight\)", text);
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

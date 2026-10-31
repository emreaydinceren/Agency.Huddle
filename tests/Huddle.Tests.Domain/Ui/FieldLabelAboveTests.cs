using System.Text.RegularExpressions;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins alignment decision D6 (markup M4) in <c>app.css</c>, for every Theme: an outlined field's label sits above the
/// box instead of floating into its border, and the contexts that hide the label behind a visible one elsewhere keep
/// hiding it. Also pins that the Top bar's "Tasks need you" pill (M5) is hidden for a Theme with no style pack.
/// </summary>
public sealed partial class FieldLabelAboveTests
{
    /// <summary>The selector of the rule that lifts the label out of the field, excluded contexts included.</summary>
    private const string AboveSelector =
        ".mud-input-control:not(.toolbar-compact):not(.task-detail-prop-value *):not(.task-detail-description-field *) > .mud-input-control-input-container > label.mud-input-label.mud-input-label-inputcontrol.mud-input-label-outlined";

    /// <summary>
    /// The label leaves MudBlazor's absolute, transformed position and is laid out first in the field's column, at caption
    /// size and 600, so it reads above the box at rest, focused and filled alike.
    /// </summary>
    [Fact]
    public void AppCss_OutlinedLabel_SitsAboveTheField()
    {
        string body = CssText.RuleBodyLoose(CssText.AppCss(CssNewlines.AsIs), AboveSelector, false);

        Assert.Equal("static", Declaration(body, "position"));
        Assert.Equal("-1", Declaration(body, "order"));
        Assert.Equal("none", Declaration(body, "transform"));
        Assert.Equal("var(--mud-typography-caption-size)", Declaration(body, "font-size"));
        Assert.Equal("600", Declaration(body, "font-weight"));
    }

    /// <summary>With the label above, no notch is cut in the border for it.</summary>
    [Fact]
    public void AppCss_OutlinedBorder_CutsNoNotch()
    {
        string body = CssText.RuleBodyLoose(CssText.AppCss(CssNewlines.AsIs), ".mud-input.mud-input-outlined > .mud-input-outlined-border legend", false);

        Assert.Equal("none", Declaration(body, "display"));
    }

    /// <summary>
    /// The contexts that visually hide a field's label keep hiding it, so a Task's property rows and the compact toolbar
    /// do not grow a second, visible label. The lifting rule excludes each by name (its exact selector is pinned by
    /// <see cref="AppCss_OutlinedLabel_SitsAboveTheField"/>), so these rules are not overridden.
    /// </summary>
    /// <param name="selector">A hiding rule's selector.</param>
    /// <param name="property">The declaration that hides the label.</param>
    /// <param name="value">Its value.</param>
    [Theory]
    [InlineData(".task-detail-prop-value .mud-input-label", "clip-path", "inset(50%)")]
    [InlineData(".task-detail-description-field .mud-input-label", "clip-path", "inset(50%)")]
    [InlineData(".mud-input-control.toolbar-compact .mud-input-label", "display", "none")]
    public void AppCss_HiddenLabelContexts_KeepHiding(string selector, string property, string value)
    {
        Assert.Equal(value, Declaration(CssText.RuleBodyLoose(CssText.AppCss(CssNewlines.AsIs), selector, false), property));
    }

    /// <summary>The Top bar's "Tasks need you" pill shows in every Theme, including the flat-surface ones (Fluent, Cobalt Soft), which used to hide it.</summary>
    [Fact]
    public void AppCss_TasksNeedYouPill_ShowsInEveryTheme()
    {
        Assert.Equal("inline-flex", Declaration(CssText.RuleBodyLoose(CssText.AppCss(CssNewlines.AsIs), ".tasks-need-you-pill", false), "display"));
    }

    /// <summary>The value of <paramref name="property"/> in a rule body, or an empty string when it is not declared.</summary>
    /// <param name="body">A rule body.</param>
    /// <param name="property">The property name.</param>
    private static string Declaration(string body, string property)
    {
        Match match = DeclarationPattern().Matches(body)
            .FirstOrDefault(candidate => string.Equals(candidate.Groups["prop"].Value, property, StringComparison.Ordinal))
            ?? Match.Empty;
        return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
    }

    [GeneratedRegex(@"(?<prop>[a-z-]+)\s*:\s*(?<value>[^;]+);", RegexOptions.CultureInvariant)]
    private static partial Regex DeclarationPattern();
}

using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins the <c>.composer-drop-hint</c> rule in <c>app.css</c> so the overlay can never be visible without a drag.</summary>
public sealed class ComposerDropHintCssTests
{
    /// <summary>The hint's base rule declares <c>display</c> exactly once, as <c>none</c>; a later <c>display: flex</c> would leave "Drop to attach" over the box permanently.</summary>
    [Fact]
    public void DropHint_BaseRuleIsHiddenUntilDropping()
    {
        string text = CssText.AppCss(CssNewlines.AsIs);
        int start = text.IndexOf(".composer-drop-hint {", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected a .composer-drop-hint rule in app.css.");
        int end = text.IndexOf('}', start);
        string[] displays = [.. text[start..end]
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("display:", StringComparison.Ordinal))];

        Assert.Equal(["display: none;"], displays);
    }
}

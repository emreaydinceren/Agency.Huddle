namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source-text assertions over <c>ReconnectModal.razor.css</c> - the established pattern in this
/// suite (<see cref="CssSource"/>, and its use in <c>ThemeSourceTests</c> and
/// <c>AppStylesheetTests</c>) for a suite that renders no browser. Pins the four fixes from the
/// reconnect-modal stylesheet task: the first-attempt paragraph's selector is exclusive of the
/// retrying state so the two do not display at once, the two selectors that forced the dialog
/// element itself to <c>display: block</c> are gone, the <c>&amp;[open]</c> nested rule is written
/// as an ordinary single-line block, and the app's own <c>background-color</c> carries
/// <c>!important</c> so it beats MudBlazor's same-origin <c>!important</c> override in dark mode.
/// </summary>
public sealed class ReconnectModalCssTests
{
    /// <summary>
    /// The first-attempt paragraph's selector excludes the retrying state. Blazor adds
    /// <c>components-reconnect-retrying</c> alongside <c>components-reconnect-show</c> rather than
    /// replacing it, so without this exclusion both "Rejoining the server..." and the
    /// "trying again in N seconds" paragraph display at the same time.
    /// </summary>
    [Fact]
    public void DisplayBlockRule_FirstAttemptVisible_IsExclusiveOfRetrying()
    {
        string text = File.ReadAllText(CssPath);

        Assert.Contains(
            "#components-reconnect-modal.components-reconnect-show:not(.components-reconnect-retrying) .components-reconnect-first-attempt-visible",
            text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Neither the retrying nor the failed state selects <c>#components-reconnect-modal</c> on its
    /// own inside the <c>display: block</c> rule group. Those two selectors forced the native
    /// <c>&lt;dialog&gt;</c> element itself to <c>display: block</c> whenever either class was
    /// present, regardless of the dialog's own <c>open</c> attribute - overriding its default
    /// hidden state instead of toggling a child paragraph.
    /// </summary>
    [Fact]
    public void DisplayBlockRule_DoesNotSetDisplayBlockOnTheModalElementItself()
    {
        IReadOnlyList<string> lines = File.ReadAllLines(CssPath);

        List<string> bareModalSelectors = lines
            .Select(line => line.Trim())
            .Where(line =>
                string.Equals(line, "#components-reconnect-modal.components-reconnect-retrying,", StringComparison.Ordinal) ||
                string.Equals(line, "#components-reconnect-modal.components-reconnect-failed,", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(bareModalSelectors);
    }

    /// <summary>
    /// The <c>&amp;[open]</c> nested rule opens its block on the same line as the selector, with no
    /// brace standing alone on its own line. The previous formatting split <c>&amp;[open]</c> and
    /// <c>{</c> across three lines (with a stray closing brace at the end), which read as malformed
    /// even though it parsed as valid CSS nesting.
    /// </summary>
    [Fact]
    public void OpenNestedRule_OpensItsBlockOnTheSameLine()
    {
        string text = File.ReadAllText(CssPath);
        IReadOnlyList<string> lines = File.ReadAllLines(CssPath);

        Assert.Contains("&[open] {", text, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => string.Equals(line.Trim(), "{", StringComparison.Ordinal));
    }

    /// <summary>
    /// The app's own <c>background-color</c> on <c>#components-reconnect-modal</c> carries
    /// <c>!important</c>. MudBlazor ships
    /// <c>#components-reconnect-modal{background-color:var(--mud-palette-background) !important}</c>
    /// in the same origin, and an <c>!important</c> declaration always beats one without it
    /// regardless of specificity - so without this, the modal's background resolves to the dark
    /// palette's <c>Background</c> colour, which is identical to the page body behind it and leaves
    /// the panel with no visible edge.
    /// </summary>
    [Fact]
    public void BackgroundColor_CarriesImportant_ToBeatMudBlazorsSameOriginOverride()
    {
        string text = File.ReadAllText(CssPath);

        Assert.Contains(
            "background-color: var(--mud-palette-surface) !important;",
            text,
            StringComparison.Ordinal);
    }

    private static string CssPath =>
        CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "ReconnectModal.razor.css");
}

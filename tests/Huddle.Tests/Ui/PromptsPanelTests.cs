using Bunit;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="PromptsPanel"/> on its own from a plain <see cref="PromptFieldGroup"/> list, using
/// <see cref="MudBunitContext"/> rather than the <c>HtmlRenderer</c> this suite used before the
/// MudBlazor migration. Unlike <c>HtmlRenderer</c>, bUnit can dispatch a real click, so the Reset
/// button's callback is now exercised end to end rather than only its disabled state; the rest of
/// these tests still assert on rendered markup, exactly as they did before, because none of the rest
/// of this panel needs a click to be visible. Every test disposes the context with <c>await using</c>
/// rather than <c>using</c>: MudBlazor's <c>KeyInterceptorService</c> (registered by
/// <c>AddMudServices</c>) implements only <see cref="IAsyncDisposable"/>, so a synchronous
/// <c>Dispose</c> throws once a MudBlazor component that needs it has actually been rendered.
/// </summary>
public sealed class PromptsPanelTests
{
    /// <summary>An unmodified field's Reset button renders disabled.</summary>
    [Fact]
    public async Task UnmodifiedField_ResetButtonIsDisabled()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false))));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    /// <summary>
    /// A modified field's Reset button renders enabled, and clicking it - something only bUnit, not
    /// the old <c>HtmlRenderer</c>-based version of this test, can actually do - raises
    /// <see cref="PromptsPanel.ResetRequested"/> carrying that field's key.
    /// </summary>
    [Fact]
    public async Task ModifiedField_ResetButtonIsEnabled_AndClickingRaisesResetRequestedWithTheFieldKey()
    {
        await using MudBunitContext ctx = new();
        string? requestedKey = null;

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: true, hasUnsavedChange: false)))
            .Add(p => p.ResetRequested, key => requestedKey = key));

        var button = cut.Find("button");
        Assert.False(button.HasAttribute("disabled"));

        await cut.InvokeAsync(() => button.ClickAsync());

        Assert.Equal("turn.roomLabel", requestedKey);
    }

    /// <summary>The "Modified" badge shows only when <see cref="PromptFieldState.IsModified"/> is true.</summary>
    [Fact]
    public async Task IsModified_ShowsTheModifiedBadge()
    {
        await using MudBunitContext ctx = new();

        var modified = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: true, hasUnsavedChange: false))));
        var unmodified = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false))));

        Assert.Contains("Modified", modified.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Modified", unmodified.Markup, StringComparison.Ordinal);
    }

    /// <summary>The "Unsaved" badge shows only when <see cref="PromptFieldState.HasUnsavedChange"/> is true.</summary>
    [Fact]
    public async Task HasUnsavedChange_ShowsTheUnsavedBadge()
    {
        await using MudBunitContext ctx = new();

        var unsaved = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: true))));
        var saved = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false))));

        Assert.Contains("Unsaved", unsaved.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Unsaved", saved.Markup, StringComparison.Ordinal);
    }

    /// <summary>The "Next session" badge shows only for a <see cref="PromptTiming.NextSession"/> field.</summary>
    [Fact]
    public async Task NextSessionTiming_ShowsTheNextSessionBadge()
    {
        await using MudBunitContext ctx = new();

        var next = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("systemPrompt.identity", isModified: false, hasUnsavedChange: false, timing: PromptTiming.NextSession))));
        var live = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false, timing: PromptTiming.Live))));

        Assert.Contains("Next session", next.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Next session", live.Markup, StringComparison.Ordinal);
    }

    /// <summary>A field's issues render as alerts, distinguishing an error from a warning by MudBlazor's own severity styling.</summary>
    [Fact]
    public async Task Issues_RenderWithSeverityDistinguishedByClass()
    {
        await using MudBunitContext ctx = new();
        var definition = PromptCatalog.Get("turn.roomLabel");
        var issues = new List<PromptIssue>
        {
            new(definition.Key, PromptIssueSeverity.Error, "This is missing a required placeholder."),
            new(definition.Key, PromptIssueSeverity.Warning, "This token looks like a typo."),
        };
        var field = MakeField(definition.Key, isModified: true, hasUnsavedChange: true, issues: issues);

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(field)));

        var alerts = cut.FindAll(".mud-alert").ToList();
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, alert => alert.TextContent.Contains("This is missing a required placeholder.", StringComparison.Ordinal) && alert.ClassList.Any(c => c.Contains("error", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(alerts, alert => alert.TextContent.Contains("This token looks like a typo.", StringComparison.Ordinal) && alert.ClassList.Any(c => c.Contains("warning", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>A field with no issues renders no alert at all.</summary>
    [Fact]
    public async Task NoIssues_RendersNoIssueList()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false))));

        Assert.Empty(cut.FindAll(".mud-alert"));
    }

    /// <summary>The field's value is edited through a real, non-readonly text field rendered as a multiline textarea.</summary>
    [Fact]
    public async Task TextField_RendersAsATextareaAndIsEditable()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false))));

        var textarea = cut.Find("textarea");
        Assert.False(textarea.HasAttribute("readonly"));
    }

    /// <summary>The field's pending value is what the text field shows, not necessarily the default.</summary>
    [Fact]
    public async Task TextField_ShowsTheFieldsPendingValue()
    {
        await using MudBunitContext ctx = new();
        var field = MakeField("turn.roomLabel", isModified: true, hasUnsavedChange: true, value: "a pending edit not yet saved");

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(field)));

        Assert.Contains("a pending edit not yet saved", cut.Find("textarea").TextContent, StringComparison.Ordinal);
    }

    /// <summary>Builds a single <see cref="PromptFieldState"/> for <see cref="PromptCatalog.Get(string)"/>'s <paramref name="key"/>, overriding only the flags and value a given test cares about.</summary>
    private static PromptFieldState MakeField(
        string key,
        bool isModified,
        bool hasUnsavedChange,
        PromptTiming timing = PromptTiming.Live,
        string? value = null,
        IReadOnlyList<PromptIssue>? issues = null)
    {
        var definition = PromptCatalog.Get(key);
        return new PromptFieldState(
            Key: definition.Key,
            Label: definition.Label,
            HelperText: definition.HelperText,
            Value: value ?? definition.Default,
            DefaultValue: definition.Default,
            Placeholders: definition.Placeholders,
            Timing: timing,
            IsModified: isModified,
            HasUnsavedChange: hasUnsavedChange,
            Issues: issues ?? []);
    }

    /// <summary>Wraps a single field in its own one-field group, for tests that do not care about grouping.</summary>
    private static IReadOnlyList<PromptFieldGroup> SingleGroup(PromptFieldState field) =>
        [new PromptFieldGroup("Test group", [field])];
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Hooks;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="HooksPanel"/> on its own from a plain <see cref="HookFieldGroup"/> list, the
/// same <c>HtmlRenderer</c> + <c>ParameterView.FromDictionary</c> approach <see cref="TeammateCardTests"/>
/// uses for <c>TeammateCard</c>. <c>HtmlRenderer</c> cannot dispatch a click or change an element, so
/// these tests only ever assert on rendered HTML - the Reset button's disabled state, which badges
/// appear, and that the textarea is editable rather than the actual click/input behaviour, which
/// <see cref="HookFieldFactoryTests"/> and <see cref="SettingsPageTests"/> cover from the other two
/// angles this suite has available.
/// </summary>
public sealed class HooksPanelTests
{
    /// <summary>An unmodified field's Reset button renders disabled.</summary>
    [Fact]
    public async Task UnmodifiedField_ResetButtonIsDisabled()
    {
        var html = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false)));

        Assert.Contains("disabled", ExtractElement(html, "button"), StringComparison.Ordinal);
    }

    /// <summary>A modified field's Reset button renders enabled.</summary>
    [Fact]
    public async Task ModifiedField_ResetButtonIsEnabled()
    {
        var html = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: true, hasUnsavedChange: false)));

        Assert.DoesNotContain("disabled", ExtractElement(html, "button"), StringComparison.Ordinal);
    }

    /// <summary>The "Modified" badge shows only when <see cref="HookFieldState.IsModified"/> is true.</summary>
    [Fact]
    public async Task IsModified_ShowsTheModifiedBadge()
    {
        var modified = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: true, hasUnsavedChange: false)));
        var unmodified = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false)));

        Assert.Contains("hooks-field-badge-modified", modified, StringComparison.Ordinal);
        Assert.DoesNotContain("hooks-field-badge-modified", unmodified, StringComparison.Ordinal);
    }

    /// <summary>The "Unsaved" badge shows only when <see cref="HookFieldState.HasUnsavedChange"/> is true.</summary>
    [Fact]
    public async Task HasUnsavedChange_ShowsTheUnsavedBadge()
    {
        var unsaved = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: true)));
        var saved = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false)));

        Assert.Contains("hooks-field-badge-unsaved", unsaved, StringComparison.Ordinal);
        Assert.DoesNotContain("hooks-field-badge-unsaved", saved, StringComparison.Ordinal);
    }

    /// <summary>The "Next session" badge shows only for a <see cref="HookTiming.NextSession"/> field.</summary>
    [Fact]
    public async Task NextSessionTiming_ShowsTheNextSessionBadge()
    {
        var next = await RenderAsync(SingleGroup(MakeField("systemPrompt.identity", isModified: false, hasUnsavedChange: false, timing: HookTiming.NextSession)));
        var live = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false, timing: HookTiming.Live)));

        Assert.Contains("Next session", next, StringComparison.Ordinal);
        Assert.DoesNotContain("Next session", live, StringComparison.Ordinal);
    }

    /// <summary>A field's issues render, distinguishing an error from a warning by CSS class.</summary>
    [Fact]
    public async Task Issues_RenderWithSeverityDistinguishedByClass()
    {
        var definition = HookCatalog.Get("turn.roomLabel");
        var issues = new List<HookIssue>
        {
            new(definition.Key, HookIssueSeverity.Error, "This is missing a required placeholder."),
            new(definition.Key, HookIssueSeverity.Warning, "This token looks like a typo."),
        };
        var field = MakeField(definition.Key, isModified: true, hasUnsavedChange: true, issues: issues);

        var html = await RenderAsync(SingleGroup(field));

        Assert.Contains("This is missing a required placeholder.", html, StringComparison.Ordinal);
        Assert.Contains("This token looks like a typo.", html, StringComparison.Ordinal);
        Assert.Contains("hooks-field-issue-error", html, StringComparison.Ordinal);
        Assert.Contains("hooks-field-issue-warning", html, StringComparison.Ordinal);
    }

    /// <summary>A field with no issues renders no issue list at all.</summary>
    [Fact]
    public async Task NoIssues_RendersNoIssueList()
    {
        var html = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false)));

        Assert.DoesNotContain("hooks-field-issues", html, StringComparison.Ordinal);
    }

    /// <summary>Stage 3 drops the "readonly" attribute Stage 2 shipped: the textarea is now editable.</summary>
    [Fact]
    public async Task Textarea_IsNoLongerReadonly()
    {
        var html = await RenderAsync(SingleGroup(MakeField("turn.roomLabel", isModified: false, hasUnsavedChange: false)));

        Assert.DoesNotContain("readonly", html, StringComparison.Ordinal);
        Assert.Contains("<textarea", html, StringComparison.Ordinal);
    }

    /// <summary>The field's pending value is what the textarea shows, not necessarily the default.</summary>
    [Fact]
    public async Task Textarea_ShowsTheFieldsPendingValue()
    {
        var field = MakeField("turn.roomLabel", isModified: true, hasUnsavedChange: true, value: "a pending edit not yet saved");

        var html = await RenderAsync(SingleGroup(field));

        Assert.Contains("a pending edit not yet saved", html, StringComparison.Ordinal);
    }

    private static HookFieldState MakeField(
        string key,
        bool isModified,
        bool hasUnsavedChange,
        HookTiming timing = HookTiming.Live,
        string? value = null,
        IReadOnlyList<HookIssue>? issues = null)
    {
        var definition = HookCatalog.Get(key);
        return new HookFieldState(
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

    private static IReadOnlyList<HookFieldGroup> SingleGroup(HookFieldState field) =>
        [new HookFieldGroup("Test group", [field])];

    private static async Task<string> RenderAsync(IReadOnlyList<HookFieldGroup> groups)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<HooksPanel>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Groups"] = groups }));

            return output.ToHtmlString();
        });
    }

    /// <summary>Pulls out the first element with the given tag name, for asserting attributes on it in isolation from the rest of the page.</summary>
    /// <param name="html">The full rendered HTML.</param>
    /// <param name="tagName">The tag to find, e.g. <c>"button"</c>.</param>
    private static string ExtractElement(string html, string tagName)
    {
        var start = html.IndexOf($"<{tagName}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No <{tagName}> element found in the rendered HTML.");

        var end = html.IndexOf('>', start);
        Assert.True(end >= 0, $"Unterminated <{tagName}> element in the rendered HTML.");

        return html[start..(end + 1)];
    }
}

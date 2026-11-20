using Bunit;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="PromptsPanel"/> on its own from a plain <see cref="PromptFieldGroup"/> list, using
/// <see cref="MudBunitContext"/>. The panel is a master-detail: a list of prompts and ONE editor, so the
/// tests choose the prompt they want by clicking its list row. Every test disposes the context with
/// <c>await using</c> rather than <c>using</c>: MudBlazor's <c>KeyInterceptorService</c> (registered by
/// <c>AddMudServices</c>) implements only <see cref="IAsyncDisposable"/>, so a synchronous
/// <c>Dispose</c> throws once a MudBlazor component that needs it has actually been rendered.
/// </summary>
public sealed class PromptsPanelTests
{
    private const string RoomLabel = "turn.roomLabel";
    private const string Identity = "systemPrompt.identity";

    /// <summary>An unmodified field's Reset button renders disabled.</summary>
    [Fact]
    public async Task UnmodifiedField_ResetButtonIsDisabled()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: false))));

        Assert.True(ActionButton(cut, "Reset").HasAttribute("disabled"));
    }

    /// <summary>
    /// A modified field's Reset button renders enabled, and clicking it raises
    /// <see cref="PromptsPanel.ResetRequested"/> carrying that field's key.
    /// </summary>
    [Fact]
    public async Task ModifiedField_ResetButtonIsEnabled_AndClickingRaisesResetRequestedWithTheFieldKey()
    {
        await using MudBunitContext ctx = new();
        string? requestedKey = null;

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: true, hasUnsavedChange: false)))
            .Add(p => p.ResetRequested, key => requestedKey = key));

        var button = ActionButton(cut, "Reset");
        Assert.False(button.HasAttribute("disabled"));

        await cut.InvokeAsync(() => button.ClickAsync());

        Assert.Equal(RoomLabel, requestedKey);
    }

    /// <summary>The list is under its group headings (<c>h3</c>, one level under the tab's own <c>h2</c> title) and has one row per prompt.</summary>
    [Fact]
    public async Task List_ShowsGroupHeadingsAndOneRowPerPrompt()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        Assert.Equal(["Group A", "Group B"], cut.FindAll(".prompts-list h3").Select(h => h.TextContent.Trim()));
        Assert.Equal(2, cut.FindAll(".prompts-list-item").Count);
        Assert.Empty(cut.FindAll("h2"));
    }

    /// <summary>Only one editor is drawn, and until the Human chooses it is the first prompt's.</summary>
    [Fact]
    public async Task Editor_IsOnePromptAtATime_StartingWithTheFirst()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        Assert.Single(cut.FindAll("textarea"));
        Assert.Equal(PromptCatalog.Get(RoomLabel).Label, cut.Find(".prompts-detail-pane h3").TextContent.Trim());
        Assert.Equal("true", cut.Find($"[id='{PromptsPanel.ListItemId(RoomLabel)}']").GetAttribute("aria-current"));
    }

    /// <summary>Clicking a row opens that prompt in the editor and marks the row as current.</summary>
    [Fact]
    public async Task ClickingARow_SelectsThatPrompt()
    {
        await using MudBunitContext ctx = new();
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        await cut.InvokeAsync(() => cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").ClickAsync());

        Assert.Equal(PromptCatalog.Get(Identity).Label, cut.Find(".prompts-detail-pane h3").TextContent.Trim());
        Assert.Equal("true", cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").GetAttribute("aria-current"));
        Assert.NotNull(cut.Find($"[id='{PromptsPanel.PromptFieldId(Identity)}']"));
        Assert.Empty(cut.FindAll($"[id='{PromptsPanel.PromptFieldId(RoomLabel)}']"));
    }

    /// <summary>Typing in the search box keeps only the prompts whose title or key matches, and drops a group left empty.</summary>
    [Fact]
    public async Task Search_FiltersTheListByTitleOrKey()
    {
        await using MudBunitContext ctx = new();
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        cut.Find(".prompts-list-pane input").Input("systemPrompt.ident");

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".prompts-list-item"));
            Assert.Equal(["Group B"], cut.FindAll(".prompts-list h3").Select(h => h.TextContent.Trim()));
        });
    }

    /// <summary>A search that matches nothing shows an empty state and no rows, and the editor stays on what was open.</summary>
    [Fact]
    public async Task Search_WithNoMatch_ShowsAnEmptyStateAndKeepsTheEditor()
    {
        await using MudBunitContext ctx = new();
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        cut.Find(".prompts-list-pane input").Input("zzz-no-such-prompt");

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".prompts-list-item"));
            Assert.Contains("No prompts match", cut.Find(".prompts-list").TextContent, StringComparison.Ordinal);
            Assert.Single(cut.FindAll("textarea"));
        });
    }

    /// <summary>The filter chips carry counts, and choosing Changed lists only the prompts with an unsaved change.</summary>
    [Fact]
    public async Task ChangedFilter_ShowsOnlyChangedPrompts_AndBothChipsCarryCounts()
    {
        await using MudBunitContext ctx = new();
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        var radios = cut.FindAll("[role='radio']");
        Assert.Contains("All 2", Squash(radios[0].TextContent), StringComparison.Ordinal);
        Assert.Contains("Changed 1", Squash(radios[1].TextContent), StringComparison.Ordinal);

        await cut.InvokeAsync(() => radios[1].ClickAsync());

        var rows = cut.FindAll(".prompts-list-item");
        Assert.Single(rows);
        Assert.Equal(PromptsPanel.ListItemId(Identity), rows[0].Id);
    }

    /// <summary>A prompt edited and not saved wears a "Changed" dot in the list; an untouched one does not.</summary>
    [Fact]
    public async Task ChangedPrompt_WearsADotInTheList()
    {
        await using MudBunitContext ctx = new();
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        Assert.Empty(cut.Find($"[id='{PromptsPanel.ListItemId(RoomLabel)}']").QuerySelectorAll(".prompts-changed-dot"));
        Assert.Single(cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").QuerySelectorAll(".prompts-changed-dot"));
    }

    /// <summary>A prompt with an error issue is marked in the list, and its issues show on the detail pane when it is open.</summary>
    [Fact]
    public async Task ErrorIssue_MarksTheRow_AndShowsOnTheDetailPane()
    {
        await using MudBunitContext ctx = new();
        var issues = new List<PromptIssue> { new(Identity, PromptIssueSeverity.Error, "This is missing a required placeholder.") };
        IReadOnlyList<PromptFieldGroup> groups =
        [
            new PromptFieldGroup("Group A", [MakeField(RoomLabel, isModified: false, hasUnsavedChange: false)]),
            new PromptFieldGroup("Group B", [MakeField(Identity, isModified: true, hasUnsavedChange: true, issues: issues)]),
        ];
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, groups));

        Assert.Empty(cut.Find($"[id='{PromptsPanel.ListItemId(RoomLabel)}']").QuerySelectorAll(".prompts-list-error"));
        Assert.Single(cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").QuerySelectorAll(".prompts-list-error"));
        Assert.Empty(cut.FindAll(".mud-alert"));

        await cut.InvokeAsync(() => cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").ClickAsync());

        Assert.Contains("This is missing a required placeholder.", Assert.Single(cut.FindAll(".mud-alert")).TextContent, StringComparison.Ordinal);
    }

    /// <summary>Every prompt's textarea has the same fixed number of rows, whatever its text, and carries the shared long-text class.</summary>
    [Fact]
    public async Task Editor_HasOneFixedHeightForEveryPrompt()
    {
        await using MudBunitContext ctx = new();
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        Assert.NotNull(cut.Find(".long-text-field textarea"));
        Assert.Equal("8", cut.Find("textarea").GetAttribute("rows"));

        await cut.InvokeAsync(() => cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").ClickAsync());

        Assert.Equal("8", cut.Find("textarea").GetAttribute("rows"));
    }

    /// <summary>The header row is followed directly by the editor, and the description, warnings and actions all come after it, so nothing above the textarea varies in height.</summary>
    [Fact]
    public async Task DetailPane_PutsTheEditorRightAfterTheHeader_AndEverythingElseBelow()
    {
        await using MudBunitContext ctx = new();
        var issues = new List<PromptIssue> { new(Identity, PromptIssueSeverity.Error, "Missing a placeholder.") };
        var field = MakeField(Identity, isModified: true, hasUnsavedChange: true, issues: issues);

        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, SingleGroup(field)));

        var children = cut.Find(".prompts-detail-pane .prompts-field").Children;
        Assert.Contains("prompts-field-header", children[0].ClassList);
        Assert.NotNull(children[1].QuerySelector("textarea"));
        Assert.Contains("prompts-field-below", children[2].ClassList);
        Assert.Equal(3, children.Length);
        Assert.NotNull(children[2].QuerySelector(".prompts-field-description"));
        Assert.NotNull(children[2].QuerySelector(".mud-alert"));
        Assert.NotNull(children[2].QuerySelector(".prompts-field-actions"));
    }

    /// <summary>The placeholders are a table: one row per token in a <c>code</c> cell, its description beside it, and "Required" on exactly the tokens the text must keep.</summary>
    [Fact]
    public async Task Placeholders_AreATableWithDescriptionsAndARequiredMarker()
    {
        await using MudBunitContext ctx = new();
        var field = MakeField(RoomLabel, isModified: false, hasUnsavedChange: false) with
        {
            Placeholders = ["{{roomLabel}}", "{{count}}"],
            RequiredPlaceholders = ["{{roomLabel}}"],
        };

        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, SingleGroup(field)));

        var rows = cut.FindAll(".prompts-placeholder-table tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Equal("{{roomLabel}}", rows[0].QuerySelector("td code")?.TextContent);
        Assert.Equal(PlaceholderCatalog.Describe("{{roomLabel}}"), rows[0].QuerySelectorAll("td")[1].TextContent.Trim());
        Assert.Equal("Required", rows[0].QuerySelectorAll("td")[2].TextContent.Trim());
        Assert.Equal(string.Empty, rows[1].QuerySelectorAll("td")[2].TextContent.Trim());
        Assert.Empty(cut.FindAll(".prompts-field-placeholders"));
    }

    /// <summary>A prompt with no placeholders draws no table, and the table sits in the region under the textarea.</summary>
    [Fact]
    public async Task PlaceholderTable_IsAbsentWithNoPlaceholders_AndLivesUnderTheTextarea()
    {
        await using MudBunitContext ctx = new();
        var none = MakeField(RoomLabel, isModified: false, hasUnsavedChange: false) with { Placeholders = [] };
        var some = MakeField(RoomLabel, isModified: false, hasUnsavedChange: false) with { Placeholders = ["{{count}}"] };

        var empty = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, SingleGroup(none)));
        var filled = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, SingleGroup(some)));

        Assert.Empty(empty.FindAll(".prompts-placeholder-table"));
        Assert.Single(filled.FindAll(".prompts-field-below .prompts-placeholder-table"));
    }

    /// <summary>Each list row is two lines: the label, then the first sentence of the prompt's description, and no more of it.</summary>
    [Fact]
    public async Task ListRow_ShowsTheLabelAndTheFirstSentenceOfTheDescription()
    {
        await using MudBunitContext ctx = new();
        var field = MakeField(RoomLabel, isModified: false, hasUnsavedChange: false) with { HelperText = "First sentence here. A second sentence that stays out of the list." };

        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, SingleGroup(field)));

        var row = cut.Find($"[id='{PromptsPanel.ListItemId(RoomLabel)}']");
        Assert.Equal(field.Label, row.QuerySelector(".prompts-list-label")?.TextContent.Trim());
        Assert.Equal("First sentence here.", row.QuerySelector(".prompts-list-description")?.TextContent.Trim());
    }

    /// <summary>Searching for words that appear only in a prompt's description finds that prompt.</summary>
    [Fact]
    public async Task Search_FindsAPromptByItsDescription()
    {
        await using MudBunitContext ctx = new();
        var other = MakeField(Identity, isModified: false, hasUnsavedChange: false) with { HelperText = "Something else entirely." };
        var target = MakeField(RoomLabel, isModified: false, hasUnsavedChange: false) with { HelperText = "Zebra crossing wording." };
        IReadOnlyList<PromptFieldGroup> groups = [new PromptFieldGroup("G", [other, target])];
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, groups));

        cut.Find(".prompts-list-pane input").Input("zebra crossing");

        cut.WaitForAssertion(() => Assert.Equal(PromptsPanel.ListItemId(RoomLabel), Assert.Single(cut.FindAll(".prompts-list-item")).Id));
    }

    /// <summary>Show default reveals the shipped text read-only for comparison, and Hide default puts it away again.</summary>
    [Fact]
    public async Task ShowDefault_TogglesTheShippedText()
    {
        await using MudBunitContext ctx = new();
        var field = MakeField(RoomLabel, isModified: true, hasUnsavedChange: true, value: "my own wording");
        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, SingleGroup(field)));
        Assert.Empty(cut.FindAll(".prompts-default"));

        await cut.InvokeAsync(() => ActionButton(cut, "Show default").ClickAsync());

        Assert.Equal(field.DefaultValue, cut.Find(".prompts-default").TextContent);

        await cut.InvokeAsync(() => ActionButton(cut, "Hide default").ClickAsync());

        Assert.Empty(cut.FindAll(".prompts-default"));
    }

    /// <summary>The "Modified" badge shows only when <see cref="PromptFieldState.IsModified"/> is true.</summary>
    [Fact]
    public async Task IsModified_ShowsTheModifiedBadge()
    {
        await using MudBunitContext ctx = new();

        var modified = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: true, hasUnsavedChange: false))));
        var unmodified = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: false))));

        Assert.Contains("Modified", modified.Find(".prompts-detail-pane").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Modified", unmodified.Find(".prompts-detail-pane").TextContent, StringComparison.Ordinal);
    }

    /// <summary>The "Changed" tag shows on the detail pane only when <see cref="PromptFieldState.HasUnsavedChange"/> is true.</summary>
    [Fact]
    public async Task HasUnsavedChange_ShowsTheChangedTag()
    {
        await using MudBunitContext ctx = new();

        var unsaved = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: true))));
        var saved = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: false))));

        Assert.Contains("Changed", unsaved.Find(".prompts-detail-pane .prompts-field-header").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Changed", saved.Find(".prompts-detail-pane .prompts-field-header").TextContent, StringComparison.Ordinal);
    }

    /// <summary>The "Next session" badge shows only for a <see cref="PromptTiming.NextSession"/> field.</summary>
    [Fact]
    public async Task NextSessionTiming_ShowsTheNextSessionBadge()
    {
        await using MudBunitContext ctx = new();

        var next = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(Identity, isModified: false, hasUnsavedChange: false, timing: PromptTiming.NextSession))));
        var live = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: false, timing: PromptTiming.Live))));

        Assert.Contains("Next session", next.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Next session", live.Markup, StringComparison.Ordinal);
    }

    /// <summary>A field's issues render as alerts, distinguishing an error from a warning by MudBlazor's own severity styling.</summary>
    [Fact]
    public async Task Issues_RenderWithSeverityDistinguishedByClass()
    {
        await using MudBunitContext ctx = new();
        var definition = PromptCatalog.Get(RoomLabel);
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
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: false))));

        Assert.Empty(cut.FindAll(".mud-alert"));
    }

    /// <summary>The field's value is edited through a real, non-readonly text field rendered as a multiline textarea.</summary>
    [Fact]
    public async Task TextField_RendersAsATextareaAndIsEditable()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(MakeField(RoomLabel, isModified: false, hasUnsavedChange: false))));

        Assert.False(cut.Find("textarea").HasAttribute("readonly"));
    }

    /// <summary>Typing in the editor raises <see cref="PromptsPanel.ValueChanged"/> with the open prompt's key and the new text.</summary>
    [Fact]
    public async Task Typing_RaisesValueChangedWithTheOpenPromptsKey()
    {
        await using MudBunitContext ctx = new();
        PromptEdit? edit = null;
        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, TwoGroups())
            .Add(p => p.ValueChanged, received => edit = received));
        await cut.InvokeAsync(() => cut.Find($"[id='{PromptsPanel.ListItemId(Identity)}']").ClickAsync());

        cut.Find("textarea").Input("new wording");

        Assert.NotNull(edit);
        Assert.Equal(Identity, edit.Key);
        Assert.Equal("new wording", edit.Text);
    }

    /// <summary>The field's pending value is what the text field shows, not necessarily the default.</summary>
    [Fact]
    public async Task TextField_ShowsTheFieldsPendingValue()
    {
        await using MudBunitContext ctx = new();
        var field = MakeField(RoomLabel, isModified: true, hasUnsavedChange: true, value: "a pending edit not yet saved");

        var cut = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, SingleGroup(field)));

        Assert.Contains("a pending edit not yet saved", cut.Find("textarea").TextContent, StringComparison.Ordinal);
    }

    /// <summary>The narrow form's select lists every prompt, with its group, so below 960px the Human can still choose one.</summary>
    [Fact]
    public async Task NarrowSelect_IsInTheMarkup_ForEveryPrompt()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, TwoGroups()));

        Assert.Single(cut.FindAll(".prompts-narrow-select"));
    }

    /// <summary>Collapses runs of whitespace so a chip's label and its count compare as one line.</summary>
    private static string Squash(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Finds a detail-pane action button by its words.</summary>
    private static AngleSharp.Dom.IElement ActionButton(IRenderedComponent<PromptsPanel> cut, string text) =>
        cut.FindAll(".prompts-field-actions button").Single(button => string.Equals(button.TextContent.Trim(), text, StringComparison.Ordinal));

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
            Issues: issues ?? [],
            RequiredPlaceholders: definition.RequiredPlaceholders);
    }

    /// <summary>Wraps a single field in its own one-field group, for tests that do not care about grouping.</summary>
    private static IReadOnlyList<PromptFieldGroup> SingleGroup(PromptFieldState field) =>
        [new PromptFieldGroup("Test group", [field])];

    /// <summary>Two groups of one prompt each: Group A's is untouched, Group B's has an unsaved change.</summary>
    private static IReadOnlyList<PromptFieldGroup> TwoGroups() =>
    [
        new PromptFieldGroup("Group A", [MakeField(RoomLabel, isModified: false, hasUnsavedChange: false)]),
        new PromptFieldGroup("Group B", [MakeField(Identity, isModified: true, hasUnsavedChange: true)]),
    ];
}

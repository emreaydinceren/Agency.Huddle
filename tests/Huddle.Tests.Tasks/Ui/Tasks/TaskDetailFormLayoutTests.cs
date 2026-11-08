namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using MudBlazor;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins DS-9, the Task detail form as two headed groups beside a strip of label | value properties: the group headings, a visible
/// label on every property, and the grid class the app.css container query hangs on.
/// </summary>
public sealed class TaskDetailFormLayoutTests
{
    /// <summary>The form is two Group labels (12px/600, Quiet ink), in order: Planning, Dates - Status and Priority are in the strip under the header (audit B4i).</summary>
    [Fact]
    public async Task Form_HasTheFourGroupHeadingsInOrder()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        string[] headings = [.. cut.FindAll(".task-detail-group > .group-label").Select(h => h.TextContent.Trim())];

        Assert.Equal(["Planning", "Dates"], headings);
        Assert.Empty(cut.FindAll(".task-detail-group > .group-heading"));
    }

    /// <summary>The properties sit in the grid class the container query turns into a two-column label | value layout.</summary>
    [Fact]
    public async Task Form_PropertiesUseTheGridClasses()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Single(cut.FindAll(".task-detail-groups"));
        Assert.Equal(2, cut.FindAll(".task-detail-group .task-detail-props").Count);
        Assert.All(cut.FindAll(".task-detail-prop"), prop =>
        {
            Assert.Single(prop.QuerySelectorAll(".task-detail-prop-label"));
            Assert.Single(prop.QuerySelectorAll(".task-detail-prop-value"));
        });
    }

    /// <summary>Every text, select, picker and date input in the form is labelled by a MudBlazor label, and has a visible row label beside it.</summary>
    [Fact]
    public async Task Form_EveryInputHasALabel()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        IReadOnlyList<IElement> inputs = [.. cut.FindAll(".task-detail-prop-value input, .task-detail-prop-value textarea, .task-detail-prop-value div[role=combobox]").Where(i => !string.Equals(i.GetAttribute("type"), "hidden", StringComparison.Ordinal))];
        Assert.NotEmpty(inputs);
        foreach (IElement input in inputs)
        {
            string? ariaLabel = input.GetAttribute("aria-label");
            string? id = input.GetAttribute("id");
            bool hasLabelFor = id is not null && cut.FindAll("label").Any(l => string.Equals(l.GetAttribute("for"), id, StringComparison.Ordinal) && l.TextContent.Trim().Length > 0);
            Assert.True(!string.IsNullOrWhiteSpace(ariaLabel) || hasLabelFor, $"Unlabelled input: {input.OuterHtml}");
        }

        foreach (IElement prop in cut.FindAll(".task-detail-prop"))
        {
            Assert.False(string.IsNullOrWhiteSpace(prop.QuerySelector(".task-detail-prop-label")?.TextContent));
        }
    }

    /// <summary>The Team and Project controls, unlabelled before, each sit in a row labelled with their own name.</summary>
    [Fact]
    public async Task Form_TeamAndProjectRowsAreLabelled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        Assert.Equal("Team", RowLabel(cut.Find(".task-detail-team")));
        Assert.Equal("Project", RowLabel(cut.Find(".task-detail-project")));
    }

    /// <summary>Start and Due date are the same kind of field and wear the same adornment (#491): the picker's own calendar at the end, neither overridden.</summary>
    [Fact]
    public async Task Form_BothDateFieldsHaveTheSameAdornment()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        var cut = RenderDetail(ctx, task.Id);

        IReadOnlyList<MudDatePicker> pickers = [.. cut.FindComponents<MudDatePicker>().Select(picker => picker.Instance)];

        Assert.Equal(2, pickers.Count);
        Assert.All(pickers, picker => Assert.Equal(Adornment.End, picker.Adornment));
        Assert.Equal(pickers[0].AdornmentIcon, pickers[1].AdornmentIcon);
        Assert.DoesNotContain(Icons.Material.Filled.Warning, pickers.Select(picker => picker.AdornmentIcon));
    }

    /// <summary>The Description is headed like a section (body1's size, 14px/600), and the property strip and the groups share one label size and one field size (#491).</summary>
    [Fact]
    public void AppCss_EditPageFields_ShareOneLabelColumnAndOneFieldSize()
    {
        string css = Agency.Huddle.Tests.Css.CssText.AppCss(Agency.Huddle.Tests.Css.CssNewlines.AsIs);
        string strip = Agency.Huddle.Tests.Css.CssText.RuleBody(css, ".task-detail-strip-label");
        string heading = Agency.Huddle.Tests.Css.CssText.RuleBody(css, ".task-detail-description-header .group-label");

        // contains-ok: source-fact test, no CSS parser - the rule's own text is what's pinned.
        Assert.Contains("flex: 0 0 96px;", strip, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-body2-size);", strip, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-body1-size);", heading, StringComparison.Ordinal);
        Assert.Contains("font-weight: 600;", heading, StringComparison.Ordinal);
        Assert.DoesNotContain(".task-edit-page .task-detail-strip .task-detail-prop-value .mud-input.mud-input-outlined {", css, StringComparison.Ordinal);
    }

    /// <summary>The visible label of the property row that holds <paramref name="control"/>.</summary>
    /// <param name="control">A control somewhere inside a <c>.task-detail-prop</c> or a strip item.</param>
    private static string RowLabel(IElement control)
    {
        IElement? row = control.Closest(".task-detail-prop, .task-detail-strip-item");
        Assert.NotNull(row);
        IElement? label = row.QuerySelector(".task-detail-prop-label, .task-detail-strip-label");
        Assert.NotNull(label);
        return label.TextContent.Trim();
    }
}

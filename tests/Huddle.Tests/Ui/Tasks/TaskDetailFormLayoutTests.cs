namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins DS-9, the Task detail form as four headed groups of label | value properties: the group headings, a visible
/// label on every property, and the grid class the app.css container query hangs on.
/// </summary>
public sealed class TaskDetailFormLayoutTests
{
    /// <summary>The form is four <c>subtitle2</c> headings, in order: Status and priority, People, Planning, Dates.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Form_HasTheFourGroupHeadingsInOrder(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        string[] headings = [.. cut.FindAll(".task-detail-group > .group-heading").Select(h => h.TextContent.Trim())];

        Assert.Equal(["Status and priority", "People", "Planning", "Dates"], headings);
        Assert.All(cut.FindAll(".task-detail-group > .group-heading"), h => Assert.True(h.ClassList.Contains("mud-typography-subtitle2")));
    }

    /// <summary>The properties sit in the grid class the container query turns into a two-column label | value layout.</summary>
    [Fact]
    public async Task Form_PropertiesUseTheGridClasses()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Single(cut.FindAll(".task-detail-groups"));
        Assert.Equal(4, cut.FindAll(".task-detail-group .task-detail-props").Count);
        Assert.All(cut.FindAll(".task-detail-prop"), prop =>
        {
            Assert.Single(prop.QuerySelectorAll(".task-detail-prop-label"));
            Assert.Single(prop.QuerySelectorAll(".task-detail-prop-value"));
        });
    }

    /// <summary>Every text, select, picker and date input in the form is labelled by a MudBlazor label, and has a visible row label beside it.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Form_EveryInputHasALabel(TaskDetailMode mode)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        IReadOnlyList<IElement> inputs = [.. cut.FindAll(".task-detail-prop-value input, .task-detail-prop-value textarea").Where(i => !string.Equals(i.GetAttribute("type"), "hidden", StringComparison.Ordinal))];
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
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal("Team", RowLabel(cut.Find(".task-detail-team")));
        Assert.Equal("Project", RowLabel(cut.Find(".task-detail-project")));
    }

    /// <summary>The visible label of the property row that holds <paramref name="control"/>.</summary>
    /// <param name="control">A control somewhere inside a <c>.task-detail-prop</c>.</param>
    private static string RowLabel(IElement control)
    {
        IElement? row = control.Closest(".task-detail-prop");
        Assert.NotNull(row);
        IElement? label = row.QuerySelector(".task-detail-prop-label");
        Assert.NotNull(label);
        return label.TextContent.Trim();
    }
}

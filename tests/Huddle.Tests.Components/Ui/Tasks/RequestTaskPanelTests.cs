namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins Task 72 (RQ-1, RQ-3, Screen 4): a Request Task shows its kind, requesting Team, triage state, requester,
/// decline reason and confirmer in the Task panel, a non-Request Task shows none of it, and the Tasks list can
/// filter down to Requests.
/// </summary>
public sealed class RequestTaskPanelTests
{
    /// <summary>Creates a Task whose frontmatter carries the six Request keys.</summary>
    private static TaskItem CreateRequest(TaskToolHarness harness, string triage, string declineReason, string confirmedBy, TaskState status)
    {
        TaskDraft draft = new("Fix login", "Platform", null, Status: status)
        {
            ExtraFields =
            [
                new("request", "bug"),
                new("from_team", "Web Team"),
                new("requester", "Joe"),
                new("triage", triage),
                new("decline_reason", declineReason),
                new("confirmed_by", confirmedBy),
            ],
        };
        TaskResult result = harness.Service.Create(draft, TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>The header chip names the kind and the requesting Team.</summary>
    [Fact]
    public async Task RequestTask_ShowsHeaderChipWithKindAndTeam()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateRequest(harness, string.Empty, string.Empty, string.Empty, TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal("Request · Bug · from Web Team", TextOf(cut, ".task-detail-request-chip"));
        Assert.Equal("Joe", TextOf(cut, ".task-detail-request-requester"));
    }

    /// <summary>Each triage state shows its chip; an untriaged Backlog Request reads Needs triage.</summary>
    [Theory]
    [InlineData("", "Needs triage")]
    [InlineData("needs-info", "Needs info")]
    [InlineData("accepted", "Accepted")]
    [InlineData("declined", "Declined")]
    public async Task RequestTask_ShowsTriageChipPerState(string triage, string expected)
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateRequest(harness, triage, string.Empty, string.Empty, TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal(expected, TextOf(cut, ".task-detail-request-triage"));
    }

    /// <summary>An untriaged Request that has left the Backlog shows no triage chip.</summary>
    [Fact]
    public async Task UntriagedRequest_OutsideBacklog_ShowsNoTriageChip()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateRequest(harness, string.Empty, string.Empty, string.Empty, TaskState.ToDo);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Empty(cut.FindAll(".task-detail-request-triage"));
        Assert.Single(cut.FindAll(".task-detail-request-chip"));
    }

    /// <summary>The decline reason and the confirmer show when set.</summary>
    [Fact]
    public async Task RequestTask_ShowsDeclineReasonAndConfirmedBy()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateRequest(harness, "declined", "Out of scope", "Ana", TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Equal("Out of scope", TextOf(cut, ".task-detail-request-decline-reason"));
        Assert.Equal("Ana", TextOf(cut, ".task-detail-request-confirmed-by"));
    }

    /// <summary>Unset decline reason and confirmer render nothing.</summary>
    [Fact]
    public async Task RequestTask_WithoutDeclineReasonOrConfirmer_ShowsNeither()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateRequest(harness, "accepted", string.Empty, string.Empty, TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Empty(cut.FindAll(".task-detail-request-decline-reason"));
        Assert.Empty(cut.FindAll(".task-detail-request-confirmed-by"));
    }

    /// <summary>An ordinary Task shows none of the Request elements.</summary>
    [Fact]
    public async Task OrdinaryTask_ShowsNoRequestElements()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness, status: TaskState.Backlog);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        Assert.Empty(cut.FindAll("[class*='task-detail-request']"));
    }

    /// <summary>The Requests filter keeps only Request Tasks.</summary>
    [Fact]
    public void RequestsFilter_ReturnsOnlyRequests()
    {
        using TaskToolHarness harness = new();
        TaskItem request = CreateRequest(harness, string.Empty, string.Empty, string.Empty, TaskState.Backlog);
        TaskItem ordinary = CreateTask(harness, status: TaskState.Backlog);

        IReadOnlyList<TaskItem> all = TaskQuery.Filter(
            [request, ordinary], ViewScope.Active, new TaskFilter(), null, "Human");
        IReadOnlyList<TaskItem> requests = TaskQuery.Filter(
            [request, ordinary], ViewScope.Active, new TaskFilter { RequestsOnly = true }, null, "Human");

        Assert.Equal(2, all.Count);
        Assert.Equal([request.Id], requests.Select(t => t.Id));
    }

    /// <summary>The toolbar's Requests chip raises the View with the filter on.</summary>
    [Fact]
    public async Task ToolbarRequestsChip_TurnsTheRequestsFilterOn()
    {
        using TempDataDir dir = new();
        using ViewStore store = new(dir.Options(), NullLogger<ViewStore>.Instance);
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton<ISprintCatalog>(new Agency.Huddle.Tests.Tasks.EmptySprintCatalog());
        ctx.Services.AddSingleton(_ => new Agency.Huddle.Tests.Acp.Tools.TaskToolHarness());
        ctx.Services.AddSingleton(sp => sp.GetRequiredService<Agency.Huddle.Tests.Acp.Tools.TaskToolHarness>().Store);
        ctx.Services.AddSingleton(sp => sp.GetRequiredService<Agency.Huddle.Tests.Acp.Tools.TaskToolHarness>().Personas);
        TaskView? raised = null;
        TaskView view = new() { Id = "v1", Name = "v1", Kind = ViewKind.List };
        var root = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskToolbar>(0);
            builder.AddAttribute(1, nameof(TaskToolbar.EffectiveView), view);
            builder.AddAttribute(2, nameof(TaskToolbar.EffectiveViewChanged), Microsoft.AspNetCore.Components.EventCallback.Factory.Create<TaskView>(this, v => raised = v));
            builder.CloseComponent();
        });

        Assert.Equal("Requests", root.Find(".task-toolbar-requests").TextContent.Trim());
        await root.InvokeAsync(() => root.Find(".task-toolbar-requests").ClickAsync());

        Assert.True(raised?.Filter.RequestsOnly);
    }
}

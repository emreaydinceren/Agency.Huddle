namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins the Task Timeline (<see cref="TaskTimeline"/>, T15g to T15i and the edit page's equivalents, Spec §7.7, ADR-0086) in both of its
/// hosts: entries oldest first, each kind drawn as its own shape, no control on any entry, and the comment box that writes only the
/// timeline.
/// </summary>
public sealed class TaskTimelineTests
{
    private const string LogPath = @"C:\Huddle\logs\tool-calls\2026-11-14.jsonl";

    private static readonly DateTimeOffset Start = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A Task with a comment, an Evidence run and a later change, each five minutes after the one before, so the time order is unambiguous.</summary>
    /// <param name="harness">The Tasks stack, built on <paramref name="clock"/>.</param>
    /// <param name="clock">The clock the harness reads; advanced between writes.</param>
    private static TaskItem CreateTimelineTask(TaskToolHarness harness, ManualTimeProvider clock)
    {
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskActor nova = new(TaskActorKind.Agent, "Nova", null);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Ship the thing", "Platform", null), human)).Task;
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddComment(task.Id, "Looks **risky** to me.", human));
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, new EvidenceLine(default, "Nova", "dotnet test", 0, LogPath, 12, string.Empty, "all green"), nova));
        clock.Advance(TimeSpan.FromMinutes(5));
        task = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        return Assert.IsType<TaskResult.Saved>(harness.Service.Update(task.Id, new TaskPatch { Priority = TaskPriority.Urgent }, task.Version, human)).Task;
    }

    /// <summary>Renders the read view for <paramref name="id"/>, with popovers (its actions menu is one).</summary>
    private static IRenderedComponent<ContainerFragment> RenderReadView(MudBunitContext ctx, TaskId id) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskReadView>(0);
            builder.AddAttribute(1, nameof(TaskReadView.Id), id);
            builder.CloseComponent();
        });

    /// <summary>Renders the edit form (<see cref="TaskDetail"/>) for <paramref name="id"/>, with popovers.</summary>
    private static IRenderedComponent<ContainerFragment> RenderEditForm(MudBunitContext ctx, TaskId id) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.CloseComponent();
        });

    /// <summary>Types <paramref name="text"/> into the comment box, as the Human does.</summary>
    private static void Type(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.Find(".task-timeline-comment-field textarea").Input(text);

    /// <summary>Clicks Comment and waits for the box to be redrawn.</summary>
    private static Task ClickCommentAsync(IRenderedComponent<ContainerFragment> cut) =>
        cut.InvokeAsync(() => cut.Find(".task-timeline-comment-send").ClickAsync());

    /// <summary>The kind words of the timeline's rows, in the order they are drawn.</summary>
    private static IReadOnlyList<string> Kinds(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".task-detail-history-row").Select(static row => row.ClassList.Single(static name => name.StartsWith("task-timeline-", StringComparison.Ordinal)))];

    /// <summary>The timeline lists a comment, an Evidence run and events oldest first, in both hosts - the time order of an issue tracker, not newest first.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_MixedEntries_AreOldestFirst(bool editForm)
    {
        ManualTimeProvider clock = new(Start);
        using TaskToolHarness harness = new(clock);
        TaskItem task = CreateTimelineTask(harness, clock);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = editForm ? RenderEditForm(ctx, task.Id) : RenderReadView(ctx, task.Id);

        Assert.Equal("Timeline (4)", cut.Find(".task-detail-history-heading").TextContent.Trim());
        Assert.Equal(["task-timeline-event", "task-timeline-comment", "task-timeline-evidence", "task-timeline-event"], Kinds(cut));
        Assert.Equal(["created", "priority: Medium → Urgent"], cut.FindAll(".task-timeline-event .task-detail-history-summary").Select(static summary => summary.TextContent.Trim()));
    }

    /// <summary>An Evidence entry is marked with the Evidence chip and shows the command in monospace, the exit code as a success chip, the full log path and the note.</summary>
    [Fact]
    public async Task Render_EvidenceEntry_ShowsChipCommandExitLogAndNote()
    {
        ManualTimeProvider clock = new(Start);
        using TaskToolHarness harness = new(clock);
        TaskItem task = CreateTimelineTask(harness, clock);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        IElement row = cut.Find(".task-timeline-evidence");
        Assert.Equal("Evidence", row.QuerySelector(".task-timeline-evidence-chip")?.TextContent.Trim());
        Assert.Equal("dotnet test", row.QuerySelector("code.task-timeline-evidence-command")?.TextContent.Trim());
        IElement exit = row.QuerySelector(".task-timeline-evidence-exit") ?? throw new InvalidOperationException("No exit chip.");
        Assert.Equal("exit 0", exit.TextContent.Trim());
        Assert.Contains("status-chip-success", exit.ClassList);
        Assert.Equal($"{LogPath}#L12", row.QuerySelector(".task-timeline-evidence-log")?.TextContent.Trim());
        Assert.Equal("all green", row.QuerySelector(".task-timeline-evidence-note")?.TextContent.Trim());
        Assert.Equal("Nova", row.QuerySelector(".task-detail-history-actor")?.TextContent.Trim());
        IElement body = row.QuerySelector(".task-timeline-evidence-body") ?? throw new InvalidOperationException("No Evidence body.");
        Assert.Equal(["task-timeline-evidence-run", "task-timeline-evidence-log", "doc-history-summary task-timeline-evidence-note"], body.Children.Select(static part => part.ClassName));
    }

    /// <summary>A failing run's exit code is an error chip, and a note-only Evidence entry shows the Evidence chip and its note with no command or exit code.</summary>
    [Fact]
    public async Task Render_FailedRunAndNoteOnlyEvidence_ShowErrorChipAndNote()
    {
        ManualTimeProvider clock = new(Start);
        using TaskToolHarness harness = new(clock);
        TaskActor nova = new(TaskActorKind.Agent, "Nova", null);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Check it", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, new EvidenceLine(default, "Nova", "dotnet build", 1, null, null, string.Empty), nova));
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddEvidence(task.Id, new EvidenceLine(default, "Nova", string.Empty, null, null, null, "Reviewed by eye."), nova));
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        IReadOnlyList<IElement> rows = cut.FindAll(".task-timeline-evidence");
        Assert.Equal(2, rows.Count);
        Assert.Contains("status-chip-error", rows[0].QuerySelector(".task-timeline-evidence-exit")?.ClassList ?? throw new InvalidOperationException("No exit chip."));
        Assert.Equal("Evidence", rows[1].QuerySelector(".task-timeline-evidence-chip")?.TextContent.Trim());
        Assert.Equal("Reviewed by eye.", rows[1].QuerySelector(".task-timeline-evidence-note")?.TextContent.Trim());
        Assert.Empty(rows[1].QuerySelectorAll("code, .task-timeline-evidence-exit"));
    }

    /// <summary>A comment's text is rendered as Markdown inside the bordered comment body.</summary>
    [Fact]
    public async Task Render_Comment_RendersMarkdownInItsBody()
    {
        ManualTimeProvider clock = new(Start);
        using TaskToolHarness harness = new(clock);
        TaskItem task = CreateTimelineTask(harness, clock);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        Assert.Equal("risky", cut.Find(".task-timeline-comment .task-timeline-comment-body strong").TextContent);
        Assert.Equal("you", cut.Find(".task-timeline-comment .task-detail-history-pill").TextContent.Trim());
    }

    /// <summary>Entries are immutable: no row of the timeline holds a button, a menu or a link-less control to edit or delete it.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_Entries_HaveNoEditOrDeleteControl(bool editForm)
    {
        ManualTimeProvider clock = new(Start);
        using TaskToolHarness harness = new(clock);
        TaskItem task = CreateTimelineTask(harness, clock);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = editForm ? RenderEditForm(ctx, task.Id) : RenderReadView(ctx, task.Id);

        Assert.NotEmpty(cut.FindAll(".task-detail-history-row"));
        Assert.Empty(cut.FindAll(".task-detail-history-list button, .task-detail-history-list .mud-menu, .task-detail-history-list input, .task-detail-history-list textarea"));
    }

    /// <summary>The Human's name, and an Agent's, wear the avatar stored for them (#461): the Human reads "ME" here, as in the Transcript, not "YO" built from the name.</summary>
    [Fact]
    public async Task Render_Authors_WearTheirStoredAvatars()
    {
        ManualTimeProvider clock = new(Start);
        using TaskToolHarness harness = new(clock);
        harness.Avatars.Save(harness.Options.Value.HumanName, new Avatar(Label: "ME", Image: null, Background: null));
        harness.Avatars.Save("Nova", new Avatar(Label: "NV", Image: null, Background: null));
        TaskItem task = CreateTimelineTask(harness, clock);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        Assert.Equal(["ME", "ME", "NV", "ME"], cut.FindAll(".task-detail-history-row .doc-history-actor .mud-avatar").Select(static avatar => avatar.TextContent.Trim()));
    }

    /// <summary>Typing and clicking Comment adds the comment through the real service: it appears in the timeline as the Human's, and the box clears.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Comment_TypedAndSent_AppearsInTheTimelineAndClearsTheBox(bool editForm)
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Talk about it", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = editForm ? RenderEditForm(ctx, task.Id) : RenderReadView(ctx, task.Id);

        Type(cut, "Second **opinion** needed.");
        await ClickCommentAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("Timeline (2)", cut.Find(".task-detail-history-heading").TextContent.Trim()));
        Assert.Equal("opinion", cut.Find(".task-timeline-comment .task-timeline-comment-body strong").TextContent);
        Assert.Equal(harness.Options.Value.HumanName, cut.Find(".task-timeline-comment .task-detail-history-actor").TextContent.Trim());
        Assert.Equal(string.Empty, cut.Find(".task-timeline-comment-field textarea").TextContent.Trim());
        TimelineEntry stored = Assert.Single(harness.Store.Get(task.Id)?.Comments ?? []);
        Assert.Equal("Second **opinion** needed.", stored.Text);
    }

    /// <summary>With no text, or only spaces, Comment stays disabled; typing real text enables it.</summary>
    [Fact]
    public async Task Comment_BlankText_KeepsTheButtonDisabled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Quiet", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);
        Assert.True(cut.Find(".task-timeline-comment-send").HasAttribute("disabled"));

        Type(cut, "   ");
        Assert.True(cut.Find(".task-timeline-comment-send").HasAttribute("disabled"));

        Type(cut, "hello");
        Assert.False(cut.Find(".task-timeline-comment-send").HasAttribute("disabled"));
    }

    /// <summary>A comment over the length limit is refused: the refusal shows under the field, the text stays for editing, and nothing is added.</summary>
    [Fact]
    public async Task Comment_TooLong_ShowsTheRefusalAndAddsNothing()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Wordy", "Platform", null), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderReadView(ctx, task.Id);

        Type(cut, new string('x', TaskService.MaxCommentLength + 1));
        await ClickCommentAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("A comment can be at most 4000 characters; this one has 4001.", cut.Find(".task-timeline-comment-box .mud-input-helper-text").TextContent.Trim()));
        Assert.Empty(harness.Store.Get(task.Id)?.Comments ?? []);
        Assert.Equal("Timeline (1)", cut.Find(".task-detail-history-heading").TextContent.Trim());
    }

    /// <summary>
    /// On the edit form a comment is independent of the pending edits: it keeps the unsaved description, writes only the timeline, and a
    /// later Save still merges (the timeline is not a field) so the file holds both the comment and the new description.
    /// </summary>
    [Fact]
    public async Task EditForm_CommentWithPendingEdit_KeepsTheEditAndASubsequentSaveMerges()
    {
        using TaskToolHarness harness = new();
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Edit and talk", "Platform", null, Description: "Old description."), TaskActors.Human(harness.Options.Value))).Task;
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderEditForm(ctx, task.Id);
        IRenderedComponent<MudTextField<string>> description = cut.FindComponents<MudTextField<string>>().Single(static field => (field.Instance.Class ?? string.Empty).Contains("task-detail-description-field", StringComparison.Ordinal));
        await cut.InvokeAsync(() => description.Instance.ValueChanged.InvokeAsync("New description."));
        Assert.Equal("Unsaved edits", cut.TextOf(".task-detail-unsaved-text"));

        Type(cut, "A remark on the way.");
        await ClickCommentAsync(cut);

        cut.WaitForAssertion(() => Assert.Equal("Timeline (2)", cut.Find(".task-detail-history-heading").TextContent.Trim()));
        Assert.Equal("Old description.", harness.Store.Get(task.Id)?.Description);
        Assert.Equal("Unsaved edits", cut.TextOf(".task-detail-unsaved-text"));
        Assert.Equal("New description.", cut.Find(".task-detail-description-field textarea").TextContent.Trim());

        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved edits", cut.Markup, StringComparison.Ordinal));
        TaskItem saved = harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task missing.");
        Assert.Equal("New description.", saved.Description);
        Assert.Equal("A remark on the way.", Assert.Single(saved.Comments).Text);
        Assert.Empty(cut.FindAll(".task-detail-conflict, .task-detail-save-error-problem"));
    }

    /// <summary>The service itself: a Save made from a version older than a comment merges rather than conflicting, because the diff ignores the timeline.</summary>
    [Fact]
    public void Update_FromAVersionBeforeAComment_Merges()
    {
        using TaskToolHarness harness = new();
        TaskActor human = TaskActors.Human(harness.Options.Value);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Merge me", "Platform", null, Description: "Before."), human)).Task;
        _ = Assert.IsType<TaskResult.Saved>(harness.Service.AddComment(task.Id, "In between.", human));

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Description = "After." }, task.Version, human);

        TaskItem saved = Assert.IsType<TaskResult.Saved>(result).Task;
        Assert.Equal("After.", saved.Description);
        Assert.Equal("In between.", Assert.Single(saved.Comments).Text);
    }
}

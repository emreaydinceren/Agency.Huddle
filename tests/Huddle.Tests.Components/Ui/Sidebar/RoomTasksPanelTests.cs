using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins the Tasks in this chat panel (Spec "Tasks in this chat", D-8): rows, state, the jump, and the refresh events.</summary>
public sealed class RoomTasksPanelTests
{
    private const string RoomId = "room-1";

    /// <summary>A resolver that counts its calls, to prove a disposed panel stops listening.</summary>
    private sealed class CountingResolver(FakeTaskReferenceResolver inner) : ITaskReferenceResolver
    {
        /// <summary>How many times <see cref="Resolve"/> ran.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public TaskReference? Resolve(TaskId id)
        {
            this.Calls++;
            return inner.Resolve(id);
        }
    }

    /// <summary>The services the panel reads, over a real <see cref="FileChatStore"/>.</summary>
    private sealed class Setup : IAsyncDisposable
    {
        private readonly TempDataDir dir = new();

        public MudBunitContext Ctx { get; } = new();

        public FileChatStore Store { get; }

        public RoomEvents Events { get; } = new(NullLogger<RoomEvents>.Instance);

        public TaskEvents TaskEvents { get; } = new();

        public FakeTaskReferenceResolver Resolver { get; } = new();

        public CountingResolver Counting { get; }

        public TabService Tabs { get; } = new();

        public SidebarPanelContext Panel { get; } = new();

        public Setup()
        {
            this.Store = this.dir.CreateChatStore();
            this.Counting = new CountingResolver(this.Resolver);
            this.Ctx.Services.AddSingleton<IChatStore>(this.Store);
            this.Ctx.Services.AddSingleton(this.Events);
            this.Ctx.Services.AddSingleton(this.TaskEvents);
            this.Ctx.Services.AddSingleton<ITaskReferenceResolver>(this.Counting);
            this.Ctx.Services.AddSingleton(this.Tabs);
        }

        public async Task SayAsync(string id, string text, CancellationToken ct, string roomId = RoomId)
        {
            ChatMessage message = new(id, DateTimeOffset.UtcNow, "human", "You", text);
            await this.Store.AppendAsync(roomId, message, ct);
        }

        public IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(string tabId = "t1") =>
            this.Ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
                .Add(c => c.Value, this.Panel)
                .Add(c => c.IsFixed, true)
                .AddChildContent<Agency.Huddle.App.Components.Sidebar.RoomTasksPanel>(s => s
                    .Add(c => c.Subject, new RoomSubject(RoomId))
                    .Add(c => c.TabId, tabId)));

        public async ValueTask DisposeAsync()
        {
            await this.Ctx.DisposeAsync();
            this.dir.Dispose();
        }
    }

    private static TaskReference Ref(string id, string title, TaskState state = TaskState.InProgress)
    {
        _ = TaskId.TryParse(id, out TaskId taskId);
        return new TaskReference(taskId, title, state.IsTerminal(), state);
    }

    /// <summary>A mentioned Task is listed with its id, its current title and its state.</summary>
    [Fact]
    public async Task Tasks_ListsIdTitleAndState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login"));
        await setup.SayAsync("m1", "please look at SUPP-0010", ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();

        cut.WaitForAssertion(() => Assert.Equal("Fix login", cut.Find(".room-tasks-row a").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.Equal("SUPP-0010", cut.Find(".room-tasks-row .room-task-id").TextContent.Trim());
        Assert.Equal("In Progress", cut.Find(".room-tasks-row .task-state-dot").GetAttribute("aria-label"));
        Assert.Equal(1, setup.Panel.Count);
    }

    /// <summary>A Backlog Task's dot is the hollow ring, so Backlog and To Do differ by shape and not only by hue (B11c).</summary>
    [Fact]
    public async Task Tasks_BacklogState_DrawsTheHollowDot()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login", TaskState.Backlog));
        await setup.SayAsync("m1", "SUPP-0010", ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();

        cut.WaitForAssertion(() => Assert.Equal("task-state-dot task-state-dot-default task-state-dot-backlog", cut.Find(".room-tasks-row .task-state-dot").ClassName), TimeSpan.FromSeconds(5));
        Assert.Equal("Backlog", cut.Find(".room-tasks-row .task-state-dot").GetAttribute("title"));
    }

    /// <summary>A Closed Task's row carries the closed class.</summary>
    [Fact]
    public async Task Tasks_ClosedTask_IsMarked()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login", TaskState.Done));
        await setup.SayAsync("m1", "SUPP-0010", ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".room-tasks-row.room-task-closed")), TimeSpan.FromSeconds(5));
    }

    /// <summary>A Task the resolver does not know is listed as plain text marked unavailable.</summary>
    [Fact]
    public async Task Tasks_Gone_IsListedAsUnavailable()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        await setup.SayAsync("m1", "SUPP-0099 is gone", ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();

        cut.WaitForAssertion(() => Assert.Equal("SUPP-0099 (unavailable)", cut.Find(".room-tasks-row").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.Empty(cut.FindAll(".room-tasks-row a"));
    }

    /// <summary>A Message posted in this Room that mentions a Task adds a row.</summary>
    [Fact]
    public async Task Tasks_MessagePostedInThisRoom_Adds()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login"));
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();
        cut.WaitForAssertion(() => Assert.Equal(0, setup.Panel.Count), TimeSpan.FromSeconds(5));

        await setup.SayAsync("m1", "see SUPP-0010", ct);
        setup.Events.PublishMessagePosted(Posted(RoomId, "m1", "see SUPP-0010"));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".room-tasks-row")), TimeSpan.FromSeconds(5));
    }

    /// <summary>A Message posted in another Room does not reload this panel.</summary>
    [Fact]
    public async Task Tasks_MessagePostedInAnotherRoom_DoesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login"));
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();
        cut.WaitForAssertion(() => Assert.Equal(0, setup.Panel.Count), TimeSpan.FromSeconds(5));

        await setup.SayAsync("m1", "see SUPP-0010", ct);
        setup.Events.PublishMessagePosted(Posted("room-2", "m1", "see SUPP-0010"));
        await Task.Delay(200, ct);

        Assert.Empty(cut.FindAll(".room-tasks-row"));
        Assert.Equal(0, setup.Panel.Count);
    }

    /// <summary>A Task change re-resolves the state shown.</summary>
    [Fact]
    public async Task Tasks_TaskChanged_RefreshesState()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login", TaskState.InProgress));
        await setup.SayAsync("m1", "SUPP-0010", ct);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();
        cut.WaitForAssertion(() => Assert.Equal("In Progress", cut.Find(".room-tasks-row .task-state-dot").GetAttribute("aria-label")), TimeSpan.FromSeconds(5));

        setup.Resolver.Add(Ref("SUPP-0010", "Fix login", TaskState.Review));
        TaskItem item = TestTasks.Make(id: "SUPP-0010", status: TaskState.Review);
        TaskActor actor = new(TaskActorKind.Human, "Human", null);
        setup.TaskEvents.RaiseTaskChanged(new TaskChange(item, item, [], actor, new ChangeLogEntry(DateTimeOffset.UtcNow, "Human", "Status")));

        cut.WaitForAssertion(() => Assert.Equal("Review", cut.Find(".room-tasks-row .task-state-dot").GetAttribute("aria-label")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The Task's link opens its page by address.</summary>
    [Fact]
    public async Task Tasks_Click_OpensTheTask()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login"));
        await setup.SayAsync("m1", "SUPP-0010", ct);

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();

        cut.WaitForAssertion(() => Assert.Equal("/tasks/item/SUPP-0010", cut.Find(".room-tasks-row a").GetAttribute("href")), TimeSpan.FromSeconds(5));
    }

    /// <summary>The jump button navigates the same Tab to the newest Message that mentioned the Task.</summary>
    [Fact]
    public async Task Tasks_JumpButton_NavigatesTheSameTab()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        Tab tab = setup.Tabs.Open("/rooms/" + RoomId);
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login"));
        await setup.SayAsync("m1", "SUPP-0010 first", ct);
        await setup.SayAsync("m2", "SUPP-0010 again", ct);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render(tab.Id);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button[aria-label='Show where it was mentioned']")), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Show where it was mentioned']").ClickAsync());

        Assert.Equal("/rooms/" + RoomId + "?message=m2", setup.Tabs.Active?.Url);
        Assert.Equal(tab.Id, setup.Tabs.Active?.Id);
    }

    /// <summary>With no Task mentioned the panel says so and counts zero.</summary>
    [Fact]
    public async Task Tasks_None_ShowsEmptyLine()
    {
        await using Setup setup = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();

        cut.WaitForAssertion(() => Assert.Equal("No Tasks mentioned in this chat yet.", cut.Find("[role='status']").TextContent.Trim()), TimeSpan.FromSeconds(5));
        Assert.Equal(0, setup.Panel.Count);
    }

    /// <summary>A disposed panel no longer re-resolves on a Task change.</summary>
    [Fact]
    public async Task Tasks_Disposed_Unsubscribes()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Setup setup = new();
        setup.Resolver.Add(Ref("SUPP-0010", "Fix login"));
        await setup.SayAsync("m1", "SUPP-0010", ct);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = setup.Render();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".room-tasks-row")), TimeSpan.FromSeconds(5));

        await setup.Ctx.DisposeComponentsOnDispatcherAsync();
        int before = setup.Counting.Calls;
        TaskItem item = TestTasks.Make(id: "SUPP-0010");
        setup.TaskEvents.RaiseTaskChanged(new TaskChange(item, item, [], new TaskActor(TaskActorKind.Human, "Human", null), new ChangeLogEntry(DateTimeOffset.UtcNow, "Human", "x")));
        setup.TaskEvents.RaiseTasksReloaded();
        await Task.Delay(200, ct);

        Assert.Equal(before, setup.Counting.Calls);
    }

    private static MessagePostedEvent Posted(string roomId, string id, string text) =>
        new(new Room(roomId, "room", DateTimeOffset.UtcNow), new ChatMessage(id, DateTimeOffset.UtcNow, "human", "You", text), [], [], new RoomBudget(0, 40));
}

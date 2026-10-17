namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins U13 T13.4 and T13.5 (UX-042, UX-043, UX-044, UX-045, UX-046, UX-047, UX-050a, UX-R07): the
/// Board card and the List adopt the field components and one field renderer, the card stops
/// repeating priority and assignee and shows no status chip (OP-11), and overdue marks the date and
/// its icon only.
/// </summary>
public sealed class TaskFieldAdoptionTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> RoomNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["room-1"] = "Launch room",
    };

    private readonly TempDataDir dataDir = new();
    private readonly Agency.Huddle.App.Acp.PersonaStore personas;
    private readonly TaskStore store;

    /// <summary>Builds the empty stores the List and the card read.</summary>
    public TaskFieldAdoptionTests()
    {
        this.personas = TestTaskStore.CreatePersonaStore(this.dataDir);
        this.store = TestTaskStore.CreateTaskStore(this.dataDir, this.personas);
    }

    /// <summary>Releases the stores and the temporary data directory.</summary>
    public void Dispose()
    {
        this.store.Dispose();
        this.personas.Dispose();
        this.dataDir.Dispose();
    }

    /// <summary>The shared field renderer words an empty assignee "Unassigned" and an empty project "No project"; other empty fields stay blank.</summary>
    [Theory]
    [InlineData("assignee", "Unassigned")]
    [InlineData("project", "No project")]
    [InlineData("due_date", null)]
    [InlineData("tags", null)]
    public void FieldView_EmptyText_IsOneRuleForBothViews(string field, string? expected)
    {
        Assert.Equal(expected, TaskFieldView.EmptyText(field));
    }

    /// <summary>The Origin field is the Room's name, never the raw id (UX-047); an unknown or absent Room gives nothing.</summary>
    [Fact]
    public void FieldView_OriginText_IsTheRoomNameOrNull()
    {
        Assert.Equal("Launch room", TaskFieldView.OriginText("room-1", RoomNames));
        Assert.Null(TaskFieldView.OriginText("room-2", RoomNames));
        Assert.Null(TaskFieldView.OriginText(null, RoomNames));
        Assert.Null(TaskFieldView.OriginText("room-1", null));
    }

    /// <summary>A card whose View lists priority and assignee shows each once: the header chip and the assignee control, not a second line (UX-042).</summary>
    [Fact]
    public async Task Card_PriorityAndAssignee_AppearOnceEvenWhenTheViewListsThem()
    {
        TaskItem task = TestTasks.Make(priority: TaskPriority.High, assignee: "Nova");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, task, fields: ["priority", "assignee"]);

        Assert.Empty(cut.FindAll(".task-card-field-priority"));
        Assert.Empty(cut.FindAll(".task-card-field-assignee"));
        string text = cut.Find("div.task-card").TextContent;
        Assert.Equal(1, CountOf(text, "High"));
        Assert.Equal(1, CountOf(text, "Nova"));
    }

    /// <summary>The card's priority chip is tinted by priority (UX-045): <c>status-chip-error</c> for Urgent, never a solid fill.</summary>
    [Fact]
    public async Task Card_PriorityChip_IsColouredByPriority()
    {
        TaskItem task = TestTasks.Make(priority: TaskPriority.Urgent);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, task);

        Assert.Contains("status-chip-error", cut.Find(".task-priority-chip").ClassList, StringComparer.Ordinal);
    }

    /// <summary>The card draws its assignee with <c>TaskAssignee</c> and passes the presence it was given; with no assignee there is none.</summary>
    [Fact]
    public async Task Card_Assignee_IsTaskAssigneeWithPresence()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(assignee: "Nova"), presence: PresenceState.Awake);

        TaskAssignee assignee = cut.FindComponent<TaskAssignee>().Instance;
        Assert.Equal("Nova", assignee.Name);
        Assert.Equal(PresenceState.Awake, assignee.Presence);

        await using MudBunitContext other = new();
        IRenderedComponent<ContainerFragment> none = this.RenderCard(other, TestTasks.Make(assignee: null));
        Assert.Empty(none.FindComponents<TaskAssignee>());
    }

    /// <summary>OP-11: a Board column names the state, so the card shows no status chip and no status line, even when the View lists status.</summary>
    [Fact]
    public async Task Card_StatusInFields_ShowsNoChipAndNoLine()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(status: TaskState.Review), fields: ["status"]);

        Assert.Empty(cut.FindAll(".task-status-chip"));
        Assert.Empty(cut.FindAll(".task-card-field-status"));
    }

    /// <summary>An empty project reads "No project" on the card, as it does in the List (UX-047).</summary>
    [Fact]
    public async Task Card_EmptyProject_ReadsNoProject()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(), fields: ["project"]);

        Assert.Equal("No project", cut.Find(".task-card-field-project").TextContent.Trim());
    }

    /// <summary>Tags are quiet tag chips on the card, as in the List (S4).</summary>
    [Fact]
    public async Task Card_Tags_AreTagChips()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(tags: ["alpha", "beta"]), fields: ["tags"]);

        Assert.Equal(2, cut.FindComponents<MudChip<string>>().Count(chip => chip.Instance.Variant == Variant.Text && chip.Instance.Class?.Contains("task-tag", StringComparison.Ordinal) is true));
        Assert.Equal(2, cut.FindAll(".task-tag.tag-chip").Count);
        Assert.Equal(["alpha", "beta"], cut.FindAll(".task-tag").Select(chip => chip.TextContent.Trim()));
    }

    /// <summary>The card's Origin is the Room's name, not its raw id (UX-047).</summary>
    [Fact]
    public async Task Card_Origin_IsTheRoomName()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(originRoomId: "room-1"), fields: ["origin"]);

        Assert.Equal("Launch room", cut.Find(".task-card-field-origin").TextContent.Trim());
        Assert.DoesNotContain("room-1", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The card's menu is "Actions for {id}" with Move to, Copy id and Copy link; the old "Move {id}" name is gone (UX-050a).</summary>
    [Fact]
    public async Task Card_Menu_IsTaskActionsMenuWithMoveTo()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(id: "PLAT-0001"));

        Assert.True(cut.FindComponent<TaskActionsMenu>().Instance.ShowMoveTo);
        Assert.Empty(cut.FindAll("button[aria-label='Move PLAT-0001']"));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());
        List<string> items = [.. cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim())];
        Assert.Equal(["Copy id", "Copy link"], items[^2..]);
    }

    /// <summary>Choosing Copy link on the card raises <c>OnCopyLink</c>.</summary>
    [Fact]
    public async Task Card_CopyLink_RaisesOnCopyLink()
    {
        int copied = 0;

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderCard(ctx, TestTasks.Make(id: "PLAT-0001"), onCopyLink: () => copied++);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Copy link", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal(1, copied);
    }

    /// <summary>An overdue card draws the warning icon before its date inside the one <c>task-due-overdue</c> span and nothing else; an on-time card marks neither (S4).</summary>
    [Fact]
    public async Task Card_Overdue_MarksIconAndDateOnly()
    {
        TaskItem late = TestTasks.Make(status: TaskState.ToDo, dueDate: new DateOnly(2000, 1, 1));
        TaskItem onTime = TestTasks.Make(status: TaskState.ToDo, dueDate: new DateOnly(2100, 1, 1));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> lateCut = this.RenderCard(ctx, late, fields: ["due_date"]);
        Assert.Single(lateCut.FindAll("[aria-label='Overdue']"));
        Assert.Equal("task-card-field-due-date task-due-overdue", lateCut.Find(".task-card-field-due-date").ClassName);
        Assert.NotNull(lateCut.Find(".task-card-field-due-date > .task-due-icon[aria-label='Overdue']"));
        Assert.Empty(lateCut.FindAll(".task-card-open > [aria-label='Overdue']"));
        Assert.Empty(lateCut.FindAll(".task-card-title.task-due-overdue, .task-card-id.task-due-overdue"));

        await using MudBunitContext other = new();
        IRenderedComponent<ContainerFragment> onTimeCut = this.RenderCard(other, onTime, fields: ["due_date"]);
        Assert.Empty(onTimeCut.FindAll(".task-due-overdue"));
        Assert.Empty(onTimeCut.FindAll("[aria-label='Overdue']"));
    }

    /// <summary>The List's Status and Priority cells are the shared chips: state colour for status, priority colour for priority.</summary>
    [Fact]
    public async Task List_StatusAndPriorityCells_UseTheSharedChips()
    {
        TaskItem task = TestTasks.Make(status: TaskState.ToDo, priority: TaskPriority.High);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderList(ctx, [task], fields: ["status", "priority"]);

        Assert.Equal(TaskState.ToDo, cut.FindComponent<TaskStatusChip>().Instance.Status);
        Assert.Contains("status-chip-default", cut.Find(".task-status-chip").ClassList, StringComparer.Ordinal);
        Assert.Equal(TaskPriority.High, cut.FindComponent<TaskPriorityChip>().Instance.Priority);
        Assert.Contains("status-chip-warning", cut.Find(".task-priority-chip").ClassList, StringComparer.Ordinal);
    }

    /// <summary>The List's Assignee cell is <c>TaskAssignee</c> with no presence dot (the List has no presence), and an empty one reads "Unassigned".</summary>
    [Fact]
    public async Task List_AssigneeCell_IsTaskAssigneeAndUnassignedComesFromTheSharedRule()
    {
        TaskItem named = TestTasks.Make(id: "PLAT-0001", assignee: "Amy");
        TaskItem unassigned = TestTasks.Make(id: "PLAT-0002", assignee: null);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderList(ctx, [named, unassigned], fields: ["assignee"]);

        Assert.Equal(["Amy"], cut.FindAll(".task-assignee-name").Select(name => name.TextContent.Trim()));
        Assert.Equal("Unassigned", cut.FindAll(".task-assignee")[1].TextContent.Trim());
        Assert.Empty(cut.FindComponents<MudBadge>());
    }

    /// <summary>The List draws tags with the same Label chips as the card, an empty project as "No project", and the Origin as the Room name.</summary>
    [Fact]
    public async Task List_TagsProjectAndOrigin_UseTheSharedRenderer()
    {
        TaskItem task = TestTasks.Make(tags: ["alpha", "beta"], originRoomId: "room-1");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderList(ctx, [task], fields: ["tags", "project", "origin"]);

        Assert.Equal(["alpha", "beta"], cut.FindAll(".task-tag").Select(chip => chip.TextContent.Trim()));
        List<string> cells = [.. cut.FindAll("td").Select(cell => cell.TextContent.Trim())];
        Assert.Contains("No project", cells);
        Assert.Contains("Launch room", cells);
        Assert.DoesNotContain("room-1", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The List row's menu is "Actions for {id}" holding Copy id and Copy link, with no Move to; Copy link raises <c>OnCopyLink</c> with the row's id.</summary>
    [Fact]
    public async Task List_Menu_IsTaskActionsMenuWithoutMoveTo()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001");
        List<TaskId> linked = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderList(ctx, [task], onCopyLink: linked.Add);

        Assert.False(cut.FindComponent<TaskActionsMenu>().Instance.ShowMoveTo);
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());
        Assert.Equal(["Copy id", "Copy link"], cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim()));
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Copy link", StringComparison.Ordinal)).ClickAsync());
        Assert.Equal([task.Id], linked);
    }

    /// <summary>The List's overdue date and icon are marked (Error, <c>task-due-overdue</c>); the Title cell is not.</summary>
    [Fact]
    public async Task List_Overdue_MarksDateAndIconOnly()
    {
        TaskItem late = TestTasks.Make(title: "Late one", status: TaskState.ToDo, dueDate: new DateOnly(2000, 1, 1));

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderList(ctx, [late], fields: ["due_date"]);

        Assert.Equal("Jan 1, 2000", cut.Find(".task-due-overdue").TextContent.Trim());
        Assert.Equal("Sat, Jan 1, 2000", cut.Find(".task-due-overdue").GetAttribute("title"));
        Assert.Equal(Color.Error, OverdueIcon(cut).Instance.Color);
        Assert.Empty(cut.FindAll(".task-list-title.task-due-overdue"));
    }

    /// <summary>The CSS no longer turns a whole overdue card orange: <c>.task-overdue</c> sets no warning colour, and the date is the error colour mixed toward body text, at weight 600, with the icon before it as the one marker (S4).</summary>
    [Fact]
    public void AppCss_OverdueMarksTheDateInErrorNotTheWholeCardInWarning()
    {
        string css = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        int card = css.IndexOf(".task-overdue {", StringComparison.Ordinal);
        if (card >= 0)
        {
            string cardRule = css[card..(css.IndexOf('}', card) + 1)];
            Assert.DoesNotContain("--mud-palette-warning", cardRule, StringComparison.Ordinal);
        }

        int date = css.IndexOf(".task-due-overdue", StringComparison.Ordinal);
        Assert.True(date >= 0, "Expected a .task-due-overdue rule in app.css.");
        string dateRule = css[date..(css.IndexOf('}', date) + 1)];
        Assert.Contains("font-weight: 600;", dateRule, StringComparison.Ordinal);
        Assert.Contains("color: color-mix(in srgb, var(--mud-palette-error) 70%, var(--mud-palette-text-primary));", dateRule, StringComparison.Ordinal);
    }

    /// <summary>Finds the icon whose accessible name is "Overdue".</summary>
    /// <param name="cut">The rendered tree to search.</param>
    private static IRenderedComponent<MudIcon> OverdueIcon(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudIcon>().First(icon => icon.Instance.UserAttributes.TryGetValue("aria-label", out object? label) && string.Equals(label?.ToString(), "Overdue", StringComparison.Ordinal));

    /// <summary>Counts the non-overlapping occurrences of <paramref name="word"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="word">The word to count.</param>
    private static int CountOf(string text, string word)
    {
        int count = 0;
        int index = text.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(word, index + word.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>Renders <see cref="TaskCard"/> with a fixed clock, the avatar store, an empty sprint catalog and the Room names.</summary>
    private IRenderedComponent<ContainerFragment> RenderCard(
        MudBunitContext ctx,
        TaskItem task,
        IReadOnlyList<string>? fields = null,
        PresenceState? presence = null,
        Action? onCopyLink = null)
    {
        ctx.Services.AddSingleton<TimeProvider>(new ManualTimeProvider() { UtcNow = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero) });
        ctx.Services.AddSingleton(new AvatarStore(this.dataDir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<ISprintCatalog>(new EmptySprintCatalog());

        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskCard>(0);
            builder.AddAttribute(1, nameof(TaskCard.Task), task);
            builder.AddAttribute(2, nameof(TaskCard.Fields), fields ?? []);
            builder.AddAttribute(3, nameof(TaskCard.Presence), presence);
            builder.AddAttribute(4, nameof(TaskCard.RoomNames), RoomNames);
            if (onCopyLink is not null)
            {
                builder.AddAttribute(5, nameof(TaskCard.OnCopyLink), EventCallback.Factory.Create(onCopyLink, onCopyLink));
            }

            builder.CloseComponent();
        });
    }

    /// <summary>Renders <see cref="TaskListView"/> for a List View showing <paramref name="fields"/>.</summary>
    private IRenderedComponent<ContainerFragment> RenderList(
        MudBunitContext ctx,
        IReadOnlyList<TaskItem> tasks,
        IReadOnlyList<string>? fields = null,
        Action<TaskId>? onCopyLink = null)
    {
        ctx.Services.AddSingleton<TimeProvider>(new ManualTimeProvider() { UtcNow = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero) });
        ctx.Services.AddSingleton(this.store);
        ctx.Services.AddSingleton<ISprintCatalog>(new EmptySprintCatalog());
        ctx.Services.AddSingleton(new AvatarStore(this.dataDir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton(this.personas);
        TaskView view = new() { Id = "v1", Name = "Test View", Kind = ViewKind.List, Fields = fields ?? [], Grouping = [] };

        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskListView>(0);
            builder.AddAttribute(1, nameof(TaskListView.View), view);
            builder.AddAttribute(2, nameof(TaskListView.Tasks), tasks);
            builder.AddAttribute(3, nameof(TaskListView.RoomNames), RoomNames);
            if (onCopyLink is not null)
            {
                builder.AddAttribute(4, nameof(TaskListView.OnCopyLink), EventCallback.Factory.Create(onCopyLink, onCopyLink));
            }

            builder.CloseComponent();
        });
    }
}

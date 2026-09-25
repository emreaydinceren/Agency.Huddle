namespace Agency.Huddle.Tests.Ui.Tasks;

using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins the Spec §13.4 <c>Cards</c> paragraph and corrections-B5 D12-1/D12-2 for <see cref="TaskCard"/>:
/// the root is a <c>&lt;div class="task-card"&gt;</c>, the open control is a plain
/// <c>&lt;button type="button"&gt;</c> that does NOT nest the ⋮ <c>MudMenu</c> or the Awake
/// <c>MudLink</c> - both are siblings of that button. It shows the id, the title (highlighted while
/// searching), the priority chip, the assignee's presence badge, the Awake line while a Turn is
/// running, overdue styling, the state-colour left border, one field named by the View, and a ⋮ menu
/// listing every state under a flat "Move to" heading, then a divider, then "Copy id".
/// </summary>
public sealed class TaskCardTests : IDisposable
{
    private readonly TempDataDir dataDir = new();
    private readonly AvatarStore avatarStore;

    /// <summary>Builds a real, empty <see cref="AvatarStore"/> - every Name resolves to <see cref="Avatar.None"/> - since <see cref="TaskCard"/> injects it to draw <c>TeammateAvatar</c> (corrections-B5 D12-3).</summary>
    public TaskCardTests()
    {
        this.avatarStore = new AvatarStore(this.dataDir.Options(), NullLogger<AvatarStore>.Instance);
    }

    /// <summary>Releases the temporary data directory backing <see cref="avatarStore"/>.</summary>
    public void Dispose() => this.dataDir.Dispose();

    /// <summary>
    /// corrections-B5 D12-1: the component's own root is a <c>&lt;div class="task-card"&gt;</c>, not a
    /// <c>&lt;button&gt;</c> (the Spec's original sketch), because the ⋮ menu and Awake link must sit
    /// outside the clickable open control. Checks the class list contains the token rather than
    /// equalling it verbatim: the root also always carries a <c>task-col-edge-{colour}</c> token
    /// (Spec §13.10, pinned separately by <see cref="LeftBorder_UsesStateColourClass"/>), so an exact
    /// match here would be broken by that other, equally-real requirement.
    /// </summary>
    [Fact]
    public async Task Root_IsADivWithClassTaskCard()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Ship the thing");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        IElement card = cut.Find("div.task-card");
        Assert.Contains("task-card", (card.ClassName ?? string.Empty).Split(' '), StringComparer.Ordinal);
    }

    /// <summary>The open control - the element that raises <see cref="TaskCard.OnOpen"/> - is a plain <c>&lt;button type="button"&gt;</c>, per corrections-B5 D12-1 and the Spec's keyboard-focus rationale (<c>Teammates.razor:85-90</c>).</summary>
    [Fact]
    public async Task OpenControl_IsAButtonTypeButton()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Ship the thing");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        IElement openButton = cut.Find("div.task-card > button.task-card-open");
        Assert.Equal("button", openButton.TagName, ignoreCase: true);
        Assert.Equal("button", openButton.GetAttribute("type"));
    }

    /// <summary>corrections-B5 D12-1: the ⋮ <c>MudMenu</c>'s activator and the Awake <c>MudLink</c> are siblings of the open button, never nested inside it - otherwise the open control and the menu/link would be two interactive elements fighting over the same click.</summary>
    [Fact]
    public async Task MenuAndAwakeLink_AreSiblingsOfTheOpenButton_NotNestedInsideIt()
    {
        TaskId id = ParseId("PLAT-0001");
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Ship the thing", assignee: "Nova");
        WakeRecord lastWake = new(id, "Nova", "room-1", "General", WakeOutcome.Woken, DateTimeOffset.UtcNow);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, presence: PresenceState.Awake, lastWake: lastWake, busy: true);

        IElement card = cut.Find("div.task-card");
        IElement openButton = cut.Find("div.task-card > button.task-card-open");
        IElement menuButton = cut.Find($"button[aria-label='Move {task.Id}']");
        IElement awakeLink = cut.Find("a.task-card-awake-link");

        Assert.True(card.Contains(menuButton), "The ⋮ menu's activator must be inside the card.");
        Assert.False(openButton.Contains(menuButton), "The ⋮ menu's activator must not be nested inside the open button.");
        Assert.False(openButton.Contains(awakeLink), "The Awake link must not be nested inside the open button.");
    }

    /// <summary>Plan 12.1.t bullet 2: the card shows the Task's id and title.</summary>
    [Fact]
    public async Task ShowsIdAndTitle()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0042", title: "Refactor the widget");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        Assert.Contains("PLAT-0042", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Refactor the widget", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Plan 12.1.t bullet 2 and Spec §13.10: the priority chip's <c>Color</c> and <c>Icon</c> follow <see cref="TaskColors"/>, for every <see cref="TaskPriority"/>.</summary>
    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    [InlineData(TaskPriority.Urgent)]
    public async Task PriorityChip_ColorAndIconMatchTaskColors(TaskPriority priority)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", priority: priority);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        IRenderedComponent<MudChip<string>> chip = cut.FindComponent<MudChip<string>>();
        Assert.Equal(TaskColors.For(priority), chip.Instance.Color);
        Assert.Equal(TaskColors.Icon(priority), chip.Instance.Icon);
        Assert.Contains(priority.ToWire(), cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Spec §13.4 Cards: the title is wrapped by <c>MudHighlighter</c> while a search term is active.</summary>
    [Fact]
    public async Task SearchTerm_TitleIsWrappedByMudHighlighter()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "Widget Alpha Release");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, search: "Alpha");

        Assert.Contains("<mark>Alpha</mark>", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Plan 12.1.t bullet 3 and Spec §13.10: the presence badge's colour and its words (via <see cref="MudBadge.BadgeAriaLabel"/> - a <c>MudTooltip</c>'s hover text is invisible to bUnit's static markup) follow the assignee's <see cref="PresenceState"/>.</summary>
    [Theory]
    [InlineData(PresenceState.Awake)]
    [InlineData(PresenceState.Asleep)]
    [InlineData(PresenceState.Offline)]
    public async Task PresenceBadge_ColourAndWordsFollowPresence(PresenceState presence)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", assignee: "Nova");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, presence: presence);

        IRenderedComponent<MudBadge> badge = cut.FindComponent<MudBadge>();
        Assert.Equal(TaskColors.For(presence), badge.Instance.Color);
        Assert.True(badge.Instance.Dot);
        Assert.True(badge.Instance.Overlap);
        Assert.NotNull(badge.Instance.BadgeAriaLabel);
        Assert.Contains("Nova", badge.Instance.BadgeAriaLabel, StringComparison.Ordinal);
        Assert.Contains(presence.ToString(), badge.Instance.BadgeAriaLabel, StringComparison.Ordinal);
        Assert.Contains("mud-avatar", badge.Markup, StringComparison.Ordinal);
    }

    /// <summary>A Task with no assignee (or an assignee whose <see cref="PresenceState"/> could not be resolved) shows no presence badge at all - there is no one to show presence for.</summary>
    [Fact]
    public async Task NoAssignee_NoPresenceBadgeOrAvatarRendered()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", assignee: null);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, presence: null);

        Assert.Empty(cut.FindComponents<MudBadge>());
    }

    /// <summary>Plan 12.1.t bullet 4: the Awake chip and its "active in Room {name}" line, linking to <c>/rooms/{id}</c>, appear only when <see cref="TaskCard.Busy"/> is true.</summary>
    [Fact]
    public async Task Busy_AwakeLineLinksToRoom()
    {
        TaskId id = ParseId("PLAT-0001");
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", assignee: "Nova");
        WakeRecord lastWake = new(id, "Nova", "room-42", "General", WakeOutcome.Woken, DateTimeOffset.UtcNow);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, presence: PresenceState.Awake, lastWake: lastWake, busy: true);

        Assert.Contains("Awake", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("active in Room General", cut.Markup, StringComparison.Ordinal);
        IElement link = cut.Find("a.task-card-awake-link");
        Assert.Equal("/rooms/room-42", link.GetAttribute("href"));
    }

    /// <summary>Even with a <see cref="WakeRecord"/> present, no Awake line or link renders when <see cref="TaskCard.Busy"/> is false - the Room's Turn has already ended.</summary>
    [Fact]
    public async Task NotBusy_NoAwakeLineEvenWithLastWake()
    {
        TaskId id = ParseId("PLAT-0001");
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", assignee: "Nova");
        WakeRecord lastWake = new(id, "Nova", "room-42", "General", WakeOutcome.Woken, DateTimeOffset.UtcNow);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, presence: PresenceState.Asleep, lastWake: lastWake, busy: false);

        Assert.DoesNotContain("active in Room", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("a.task-card-awake-link"));
    }

    /// <summary>Plan 12.1.t bullet 5 and Spec §13.4: a non-terminal Task overdue against the clock gets <c>task-overdue</c> and a warning icon.</summary>
    [Fact]
    public async Task OverdueNonTerminal_HasOverdueClassAndWarningIcon()
    {
        DateOnly longPastDue = new(2000, 1, 1);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", status: TaskState.ToDo, dueDate: longPastDue);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        IElement card = cut.Find("div.task-card");
        Assert.Contains("task-overdue", card.ClassName, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[aria-label='Overdue']"));
    }

    /// <summary>The same overdue date on a terminal Task (Done) never gets <c>task-overdue</c> - colour is never the only signal, but a finished Task is never late either.</summary>
    [Fact]
    public async Task TerminalOverdue_NoOverdueClass()
    {
        DateOnly longPastDue = new(2000, 1, 1);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", status: TaskState.Done, dueDate: longPastDue);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        IElement card = cut.Find("div.task-card");
        Assert.DoesNotContain("task-overdue", card.ClassName, StringComparison.Ordinal);
    }

    /// <summary>Spec §13.4 Cards ("A left border in the column's colour class") and §13.10: the card carries the same <c>task-col-edge-{colour}</c> class the Board's column uses, pinned already by <c>AppCssTasksTests.AppCss_DeclaresTaskColEdge_ForEveryStateFamily</c>.</summary>
    [Fact]
    public async Task LeftBorder_UsesStateColourClass()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", status: TaskState.InProgress);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        IElement card = cut.Find("div.task-card");
        string expected = string.Create(CultureInfo.InvariantCulture, $"task-col-edge-{TaskColors.For(TaskState.InProgress).ToString().ToLowerInvariant()}");
        Assert.Contains(expected, card.ClassName, StringComparison.Ordinal);
    }

    /// <summary>Spec §13.4 Cards ("The View's extra fields"): a field named in <see cref="TaskCard.Fields"/> shows its value, formatted the same way <c>TaskListView</c> formats a date ("d", invariant).</summary>
    [Fact]
    public async Task Fields_DueDateShownWhenInFields()
    {
        DateOnly dueDate = new(2026, 10, 5);
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", status: TaskState.ToDo, dueDate: dueDate);

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, fields: ["due_date"]);

        Assert.Contains(dueDate.ToString("d", CultureInfo.InvariantCulture), cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>corrections-B5 D12-2: the ⋮ menu is flat - a "Move to" heading, all eight <see cref="TaskState"/>s as <c>MudMenuItem</c>s in declaration order, a divider, then "Copy id".</summary>
    [Fact]
    public async Task MoveToMenu_ListsAllEightStatesThenDividerThenCopyId()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T");

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task);

        cut.Find($"button[aria-label='Move {task.Id}']").Click();

        IReadOnlyList<IElement> items = cut.FindAll("div.mud-menu-item");
        List<string> expected = [.. TaskStates.All.Select(state => state.ToWire()), "Copy id"];
        Assert.Equal(expected, items.Select(item => item.TextContent.Trim()));

        int moveToIndex = cut.Markup.IndexOf("Move to", StringComparison.Ordinal);
        int backlogIndex = cut.Markup.IndexOf(TaskState.Backlog.ToWire(), StringComparison.Ordinal);
        int rejectedIndex = cut.Markup.IndexOf(TaskState.Rejected.ToWire(), StringComparison.Ordinal);
        int dividerIndex = cut.Markup.IndexOf("mud-divider", rejectedIndex, StringComparison.Ordinal);
        int copyIdIndex = cut.Markup.IndexOf("Copy id", StringComparison.Ordinal);

        Assert.True(moveToIndex >= 0 && moveToIndex < backlogIndex, "The 'Move to' heading must precede the state items.");
        Assert.True(dividerIndex > rejectedIndex && dividerIndex < copyIdIndex, "A divider must separate the state items from 'Copy id'.");
    }

    /// <summary>Choosing a state from the ⋮ menu raises <see cref="TaskCard.OnMoveTo"/> with that state - the same path a drop takes (Spec §13.4 Keyboard).</summary>
    [Fact]
    public async Task MoveToMenuItem_Click_RaisesOnMoveToWithThatState()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T", status: TaskState.ToDo);
        List<TaskState> moved = [];

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, onMoveTo: state => moved.Add(state));

        cut.Find($"button[aria-label='Move {task.Id}']").Click();
        cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Done", StringComparison.Ordinal)).Click();

        Assert.Single(moved);
        Assert.Equal(TaskState.Done, moved[0]);
    }

    /// <summary>Choosing "Copy id" from the ⋮ menu raises <see cref="TaskCard.OnCopyId"/>.</summary>
    [Fact]
    public async Task CopyIdMenuItem_Click_RaisesOnCopyId()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T");
        int copyCount = 0;

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, onCopyId: () => copyCount++);

        cut.Find($"button[aria-label='Move {task.Id}']").Click();
        cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Copy id", StringComparison.Ordinal)).Click();

        Assert.Equal(1, copyCount);
    }

    /// <summary>Spec §13.4 ("Clicking a card opens the detail panel"): clicking the open button raises <see cref="TaskCard.OnOpen"/>.</summary>
    [Fact]
    public async Task OpenButtonClick_RaisesOnOpen()
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", title: "T");
        int openCount = 0;

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.Render(ctx, task, onOpen: () => openCount++);

        cut.Find("div.task-card > button.task-card-open").Click();

        Assert.Equal(1, openCount);
    }

    /// <summary>Parses a fixture id string into a <see cref="TaskId"/>, the same way <see cref="TestTasks.Make"/> does internally.</summary>
    private static TaskId ParseId(string id)
    {
        _ = TaskId.TryParse(id, out TaskId parsed);
        return parsed;
    }

    /// <summary>Renders <see cref="TaskCard"/> with the popover provider present (its ⋮ <c>MudMenu</c> portals there) and a fixed <see cref="TimeProvider"/> so overdue checks are deterministic, registering the shared <see cref="avatarStore"/>.</summary>
    private IRenderedComponent<ContainerFragment> Render(
        MudBunitContext ctx,
        TaskItem task,
        IReadOnlyList<string>? fields = null,
        PresenceState? presence = null,
        WakeRecord? lastWake = null,
        bool busy = false,
        string? search = null,
        Action<TaskState>? onMoveTo = null,
        Action? onOpen = null,
        Action? onCopyId = null)
    {
        ManualTimeProvider clock = new() { UtcNow = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero) };
        ctx.Services.AddSingleton<TimeProvider>(clock);
        ctx.Services.AddSingleton(this.avatarStore);

        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskCard>(0);
            builder.AddAttribute(1, nameof(TaskCard.Task), task);
            builder.AddAttribute(2, nameof(TaskCard.Fields), fields ?? []);
            builder.AddAttribute(3, nameof(TaskCard.Presence), presence);
            builder.AddAttribute(4, nameof(TaskCard.LastWake), lastWake);
            builder.AddAttribute(5, nameof(TaskCard.Busy), busy);
            builder.AddAttribute(6, nameof(TaskCard.Search), search);
            if (onMoveTo is not null)
            {
                builder.AddAttribute(7, nameof(TaskCard.OnMoveTo), EventCallback.Factory.Create<TaskState>(onMoveTo, onMoveTo));
            }

            if (onOpen is not null)
            {
                builder.AddAttribute(8, nameof(TaskCard.OnOpen), EventCallback.Factory.Create(onOpen, onOpen));
            }

            if (onCopyId is not null)
            {
                builder.AddAttribute(9, nameof(TaskCard.OnCopyId), EventCallback.Factory.Create(onCopyId, onCopyId));
            }

            builder.CloseComponent();
        });
    }
}

namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Pins U13 T13.2 and T13.5 (UX-044, UX-045, UX-046, UX-050a, UX-R07): <c>TaskStatusChip</c>,
/// <c>TaskPriorityChip</c>, <c>TaskAssignee</c> and <c>TaskActionsMenu</c>, each drawn one way for
/// the Board card, the List and search.
/// </summary>
public sealed class TaskFieldComponentsTests : IDisposable
{
    private readonly TempDataDir dataDir = new();

    /// <summary>Releases the temporary data directory backing the avatar store.</summary>
    public void Dispose() => this.dataDir.Dispose();

    /// <summary>The status chip is a Small tinted chip in the state's colour family, with the state's icon and wire name.</summary>
    [Theory]
    [InlineData(TaskState.Backlog, Color.Default)]
    [InlineData(TaskState.ToDo, Color.Info)]
    [InlineData(TaskState.InProgress, Color.Warning)]
    [InlineData(TaskState.Review, Color.Secondary)]
    [InlineData(TaskState.Done, Color.Success)]
    [InlineData(TaskState.Rejected, Color.Error)]
    public async Task StatusChip_SmallChipInStateColourWithWireName(TaskState status, Color expected)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskStatusChip>(0);
            builder.AddAttribute(1, nameof(TaskStatusChip.Status), status);
            builder.CloseComponent();
        });

        IRenderedComponent<MudChip<string>> chip = cut.FindComponent<MudChip<string>>();
        Assert.Contains($"status-chip-{expected.ToString().ToLowerInvariant()}", cut.Find(".mud-chip").ClassList, StringComparer.Ordinal);
        Assert.Equal(Variant.Text, chip.Instance.Variant);
        Assert.Equal(Size.Small, chip.Instance.Size);
        Assert.Equal(TaskColors.Icon(status), chip.Instance.Icon);
        Assert.Equal(status.ToWire(), cut.Find(".mud-chip").TextContent.Trim());
        Assert.Contains("task-status-chip", cut.Find(".mud-chip").ClassList, StringComparer.Ordinal);
    }

    /// <summary>The priority chip is a Small tinted chip in <c>TaskColors.For(priority)</c>'s family (UX-045), with the priority icon and the wire name.</summary>
    [Theory]
    [InlineData(TaskPriority.Low, Color.Default)]
    [InlineData(TaskPriority.Medium, Color.Info)]
    [InlineData(TaskPriority.High, Color.Warning)]
    [InlineData(TaskPriority.Urgent, Color.Error)]
    public async Task PriorityChip_SmallChipInPriorityColourWithIconAndWord(TaskPriority priority, Color expected)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskPriorityChip>(0);
            builder.AddAttribute(1, nameof(TaskPriorityChip.Priority), priority);
            builder.CloseComponent();
        });

        IRenderedComponent<MudChip<string>> chip = cut.FindComponent<MudChip<string>>();
        Assert.Contains($"status-chip-{expected.ToString().ToLowerInvariant()}", cut.Find(".mud-chip").ClassList, StringComparer.Ordinal);
        Assert.Equal(Variant.Text, chip.Instance.Variant);
        Assert.Equal(Size.Small, chip.Instance.Size);
        Assert.Equal(TaskColors.Icon(priority), chip.Instance.Icon);
        Assert.Equal(priority.ToWire(), cut.Find(".mud-chip").TextContent.Trim());
        Assert.Contains("task-priority-chip", cut.Find(".mud-chip").ClassList, StringComparer.Ordinal);
    }

    /// <summary>A named assignee is a Small avatar then the name, once.</summary>
    [Fact]
    public async Task Assignee_Named_ShowsSmallAvatarThenName()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderAssignee(ctx, "Nova", presence: null);

        Assert.Equal(Size.Small, cut.FindComponent<TeammateAvatar>().Instance.Size);
        Assert.Equal("Nova", cut.Find(".task-assignee-name").TextContent.Trim());
        Assert.Single(cut.FindAll(".mud-avatar"));
    }

    /// <summary>No assignee reads "Unassigned" and draws no avatar.</summary>
    [Fact]
    public async Task Assignee_NoName_ShowsUnassignedWithoutAvatar()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderAssignee(ctx, null, presence: null);

        Assert.Equal("Unassigned", cut.Find(".task-assignee").TextContent.Trim());
        Assert.Empty(cut.FindAll(".mud-avatar"));
    }

    /// <summary>A known presence adds a dot in <c>TaskColors.For(presence)</c> whose words are "{name} is {presence}".</summary>
    [Theory]
    [InlineData(PresenceState.Awake)]
    [InlineData(PresenceState.Asleep)]
    [InlineData(PresenceState.Offline)]
    public async Task Assignee_KnownPresence_ShowsDotWithColourAndWords(PresenceState presence)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderAssignee(ctx, "Nova", presence);

        IRenderedComponent<MudBadge> badge = cut.FindComponent<MudBadge>();
        Assert.True(badge.Instance.Dot);
        Assert.Equal(TaskColors.For(presence), badge.Instance.Color);
        Assert.Equal($"Nova is {presence}", badge.Instance.BadgeAriaLabel);
    }

    /// <summary>The one rule for unknown presence (UX-046): no dot, and never an "Offline" fallback.</summary>
    [Fact]
    public async Task Assignee_UnknownPresence_ShowsNoDotAndNoOfflineFallback()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = this.RenderAssignee(ctx, "Nova", presence: null);

        Assert.Empty(cut.FindComponents<MudBadge>());
        Assert.DoesNotContain("Offline", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>The actions button is named "Actions for {id}" wherever it appears (UX-050a).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActionsMenu_Button_IsNamedActionsForId(bool showMoveTo)
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderMenu(ctx, showMoveTo);

        IRenderedComponent<MudIconButton> button = cut.FindComponent<MudIconButton>();
        Assert.Equal(Size.Small, button.Instance.Size);
        Assert.Equal(ActionIcons.MoreActions, button.Instance.Icon);
        Assert.NotNull(cut.Find("button[aria-label='Actions for PLAT-0001']"));
    }

    /// <summary>With Move to (the card): the "Move to" heading, all eight states, a divider, then Copy id and Copy link.</summary>
    [Fact]
    public async Task ActionsMenu_WithMoveTo_ListsStatesThenCopyIdAndCopyLink()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderMenu(ctx, showMoveTo: true);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());

        List<string> expected = [.. TaskStates.All.Select(state => state.ToWire()), "Copy id", "Copy link"];
        Assert.Equal(expected, cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim()));
        Assert.Equal(["Move to"], cut.FindAll(".mud-typography-overline").Select(heading => heading.TextContent.Trim()));
        Assert.NotEmpty(cut.FindAll("hr.mud-divider"));
    }

    /// <summary>Without Move to (the List): only Copy id and Copy link, no heading and no divider.</summary>
    [Fact]
    public async Task ActionsMenu_WithoutMoveTo_ListsOnlyCopyIdAndCopyLink()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderMenu(ctx, showMoveTo: false);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());

        Assert.Equal(["Copy id", "Copy link"], cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim()));
        Assert.DoesNotContain("Move to", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Copy id and Copy link wear the shared <c>ActionIcons</c> (UX-R26).</summary>
    [Fact]
    public async Task ActionsMenu_CopyItems_UseActionIcons()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderMenu(ctx, showMoveTo: false);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());

        IReadOnlyList<IRenderedComponent<MudMenuItem>> items = cut.FindComponents<MudMenuItem>();
        Assert.Equal(ActionIcons.Copy, items.First(item => item.Markup.Contains("Copy id", StringComparison.Ordinal)).Instance.Icon);
        Assert.Equal(ActionIcons.CopyLink, items.First(item => item.Markup.Contains("Copy link", StringComparison.Ordinal)).Instance.Icon);
    }

    /// <summary>Each item raises its own callback: a state raises OnMoveTo with that state, Copy id raises OnCopyId, Copy link raises OnCopyLink.</summary>
    [Fact]
    public async Task ActionsMenu_Items_RaiseTheirCallbacks()
    {
        List<TaskState> moved = [];
        int copyId = 0;
        int copyLink = 0;

        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = RenderMenu(ctx, showMoveTo: true, moved.Add, () => copyId++, () => copyLink++);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Done", StringComparison.Ordinal)).ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Copy id", StringComparison.Ordinal)).ClickAsync());
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Actions for PLAT-0001']").ClickAsync());
        await cut.InvokeAsync(() => cut.FindAll("div.mud-menu-item").First(item => string.Equals(item.TextContent.Trim(), "Copy link", StringComparison.Ordinal)).ClickAsync());

        Assert.Equal([TaskState.Done], moved);
        Assert.Equal(1, copyId);
        Assert.Equal(1, copyLink);
    }

    /// <summary>Renders <c>TaskAssignee</c> with a real, empty avatar store.</summary>
    private IRenderedComponent<ContainerFragment> RenderAssignee(MudBunitContext ctx, string? name, PresenceState? presence)
    {
        ctx.Services.AddSingleton(new AvatarStore(this.dataDir.Options(), NullLogger<AvatarStore>.Instance));
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskAssignee>(0);
            builder.AddAttribute(1, nameof(TaskAssignee.Name), name);
            builder.AddAttribute(2, nameof(TaskAssignee.Presence), presence);
            builder.CloseComponent();
        });
    }

    /// <summary>Renders <c>TaskActionsMenu</c> for PLAT-0001 with optional callbacks.</summary>
    private static IRenderedComponent<ContainerFragment> RenderMenu(
        MudBunitContext ctx,
        bool showMoveTo,
        Action<TaskState>? onMoveTo = null,
        Action? onCopyId = null,
        Action? onCopyLink = null)
    {
        _ = TaskId.TryParse("PLAT-0001", out TaskId id);
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskActionsMenu>(0);
            builder.AddAttribute(1, nameof(TaskActionsMenu.Id), id);
            builder.AddAttribute(2, nameof(TaskActionsMenu.ShowMoveTo), showMoveTo);
            if (onMoveTo is not null)
            {
                builder.AddAttribute(3, nameof(TaskActionsMenu.OnMoveTo), EventCallback.Factory.Create(onMoveTo, onMoveTo));
            }

            if (onCopyId is not null)
            {
                builder.AddAttribute(4, nameof(TaskActionsMenu.OnCopyId), EventCallback.Factory.Create(onCopyId, onCopyId));
            }

            if (onCopyLink is not null)
            {
                builder.AddAttribute(5, nameof(TaskActionsMenu.OnCopyLink), EventCallback.Factory.Create(onCopyLink, onCopyLink));
            }

            builder.CloseComponent();
        });
    }
}

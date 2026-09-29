using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the FAB menu beside the Tasks page's title: <c>Edit task view</c> for every View and
/// <c>Delete task view</c> for a non-built-in one, with the same confirm wording and navigate-away rule
/// as <c>TaskViewNav</c>'s row menu and <c>ViewEditorDrawer</c>'s own Delete button.
/// </summary>
public sealed class TasksPageViewMenuTests
{
    private const string EditLabel = "Edit task view";
    private const string DeleteLabel = "Delete task view";

    /// <summary>A saved, non-built-in View gets both actions in the menu beside its title, opening downward.</summary>
    [Fact]
    public async Task ViewMenu_ForACustomView_OffersEditAndDelete_OpeningDownward()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        Assert.NotNull(cut.Find(".tasks-header .tasks-view-menu"));
        Assert.Equal([EditLabel, DeleteLabel], ItemLabels(cut));
        Assert.Contains("mud-fab-menu-direction-bottom", cut.Find(".tasks-view-menu .mud-fab-menu").ClassName, StringComparison.Ordinal);
    }

    /// <summary>A built-in View (here All Tasks) can be edited but never deleted, so the menu offers only Edit.</summary>
    [Fact]
    public async Task ViewMenu_ForABuiltInView_OffersOnlyEdit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, ViewStore.AllTasksId);

        Assert.Equal([EditLabel], ItemLabels(cut));
    }

    /// <summary>Edit task view opens the page's hosted <see cref="ViewEditorDrawer"/> on the View whose title the menu sits beside.</summary>
    [Fact]
    public async Task EditTaskView_OpensTheEditorDrawerForThatView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");
        Assert.False(cut.FindComponent<ViewEditorDrawer>().Instance.Open);

        await cut.InvokeAsync(() => ItemFor(cut, EditLabel).ClickAsync());

        ViewEditorDrawer drawer = cut.FindComponent<ViewEditorDrawer>().Instance;
        Assert.True(drawer.Open);
        Assert.Equal("custom1", drawer.Id);
    }

    /// <summary>Delete task view asks through <c>ShowMessageBoxAsync</c> with the exact wording the other two Delete entry points use.</summary>
    [Fact]
    public async Task DeleteTaskView_MessageBox_HasTheExactTitleTextAndButtons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        _ = cut.InvokeAsync(() => ItemFor(cut, DeleteLabel).Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        Assert.Equal("Delete View", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Delete 'Sprint Board'? Tasks are not affected.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(["Cancel", "Delete"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());
    }

    /// <summary>Cancelling the confirm keeps the View in <see cref="ViewStore"/> and leaves the URL alone.</summary>
    [Fact]
    public async Task DeleteTaskView_Cancelled_KeepsTheView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        _ = cut.InvokeAsync(() => ItemFor(cut, DeleteLabel).Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());

        Assert.NotNull(harness.Views.Get("custom1"));
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>Confirming removes the View from <see cref="ViewStore"/> and replaces the URL with <c>/tasks</c>, since the View just deleted is the one on screen.</summary>
    [Fact]
    public async Task DeleteTaskView_Confirmed_RemovesTheView_AndNavigatesToTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tasks/custom1");
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        _ = cut.InvokeAsync(() => ItemFor(cut, DeleteLabel).Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => DialogButton(cut, "Delete").ClickAsync());

        Assert.Null(harness.Views.Get("custom1"));
        Assert.EndsWith("/tasks", navigation.Uri, StringComparison.Ordinal); // contains-ok: a full URI, whose scheme/host prefix bUnit's FakeNavigationManager owns; only the path is this test's concern.
    }

    /// <summary>With <c>views.json</c> unreadable the store refuses every write, so Edit and Delete are both disabled rather than offered and then silently failing.</summary>
    [Fact]
    public async Task ViewMenu_WhileViewsJsonHasALoadError_DisablesEditAndDelete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });
        await File.WriteAllTextAsync(Path.Combine(harness.Options.Value.DataDir, "views.json"), "{ not json", ct);
        harness.Views.Dispose();
        using ViewStore broken = new(harness.Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<ViewStore>.Instance);

        await using MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton(broken);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, ViewStore.AllTasksId);

        Assert.All(cut.FindAll(".tasks-view-menu .mud-fab-menu-item"), item => Assert.True(item.HasAttribute("disabled")));
    }

    /// <summary>Registers the harness's services into a fresh <see cref="MudBunitContext"/>.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderPage(MudBunitContext ctx, string viewId) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), viewId);
            builder.CloseComponent();
        });

    /// <summary>The menu items' visible labels, in order.</summary>
    private static List<string> ItemLabels(IRenderedComponent<ContainerFragment> cut) =>
        [.. cut.FindAll(".tasks-view-menu .mud-fab-menu-item").Select(item => item.TextContent.Trim())];

    /// <summary>The menu item whose label is <paramref name="label"/>.</summary>
    private static IElement ItemFor(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll(".tasks-view-menu .mud-fab-menu-item").Single(item => string.Equals(item.TextContent.Trim(), label, StringComparison.Ordinal));

    private static IElement DialogButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));
}

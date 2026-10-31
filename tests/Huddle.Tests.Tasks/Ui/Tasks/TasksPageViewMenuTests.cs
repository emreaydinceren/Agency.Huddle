using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins the MoreVert menu (T2a, UX-050b) beside the Tasks page's title: <c>Rename</c> and <c>Fields…</c> for every View and
/// <c>Delete view</c> for a non-built-in one, with the same confirm wording and navigate-away rule
/// as <c>TaskViewNav</c>'s row menu.
/// </summary>
public sealed class TasksPageViewMenuTests
{
    private const string RenameLabel = "Rename…";
    private const string FieldsLabel = "Fields…";
    private const string DeleteLabel = "Delete view…";

    /// <summary>A saved, non-built-in View gets both actions in the MoreVert menu beside its title.</summary>
    [Fact]
    public async Task ViewMenu_ForACustomView_OffersRenameFieldsAndDelete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        await OpenMenuAsync(cut);

        Assert.Equal([RenameLabel, FieldsLabel, DeleteLabel], ItemLabels(cut));
    }

    /// <summary>A built-in View (here All Tasks) can be renamed but never deleted, so the menu offers no Delete.</summary>
    [Fact]
    public async Task ViewMenu_ForABuiltInView_OffersRenameAndFieldsButNotDelete()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, ViewStore.AllTasksId);
        await OpenMenuAsync(cut);

        Assert.Equal([RenameLabel, FieldsLabel], ItemLabels(cut));
    }

    /// <summary>Delete view asks through the shared confirm (UX-R06) with the exact wording the other two Delete entry points use.</summary>
    [Fact]
    public async Task DeleteTaskView_MessageBox_HasTheExactTitleTextAndButtons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        await OpenMenuAsync(cut);
        Task deleting = await OpenDeleteDialogAsync(cut);

        Assert.Equal("Delete \"Sprint Board\"?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("This removes the View. Tasks are not affected.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(["Cancel", "Delete"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());
        await deleting;
    }

    /// <summary>Cancelling the confirm keeps the View in <see cref="ViewStore"/> and leaves the URL alone.</summary>
    [Fact]
    public async Task DeleteTaskView_Cancelled_KeepsTheView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        await OpenMenuAsync(cut);
        Task deleting = await OpenDeleteDialogAsync(cut);
        await cut.InvokeAsync(() => DialogButton(cut, "Cancel").ClickAsync());
        await deleting;

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

        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tasks/custom1");
        IRenderedComponent<ContainerFragment> cut = RenderPage(ctx, "custom1");

        await OpenMenuAsync(cut);
        Task deleting = await OpenDeleteDialogAsync(cut);
        await cut.InvokeAsync(() => DialogButton(cut, "Delete").ClickAsync());
        await deleting;

        Assert.Null(harness.Views.Get("custom1"));
        Assert.EndsWith("/tasks", navigation.Uri, StringComparison.Ordinal); // contains-ok: a full URI, whose scheme/host prefix bUnit's FakeNavigationManager owns; only the path is this test's concern.
    }

    /// <summary>With <c>views.json</c> unreadable the store refuses every write, so the page shows no View (and so no View menu) rather than offering Edit and Delete that silently fail.</summary>
    [Fact]
    public async Task ViewMenu_WhileViewsJsonHasALoadError_IsNotOffered()
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

        Assert.Empty(cut.FindAll(".tasks-header button[aria-label^='Actions for ']"));
    }

    /// <summary>
    /// Fires the <c>Delete task view</c> item and returns once the confirm dialog's buttons are rendered. The handler
    /// awaits the dialog's result, so the click is started rather than awaited, and the returned task completes only
    /// when the handler has finished (after the dialog is answered and, on Delete, the View is removed and the URL replaced).
    /// Waiting asynchronously, rather than blocking in <c>WaitForAssertion</c>, leaves the pool thread the handler and the
    /// dialog render need free; a blocked test thread starved them on a busy runner and the wait saw no render at all.
    /// </summary>
    /// <param name="cut">The rendered page and its dialog provider.</param>
    /// <returns>The click's task, for the test to await once it has answered the dialog.</returns>
    private static async Task<Task> OpenDeleteDialogAsync(IRenderedComponent<ContainerFragment> cut)
    {
        Task deleting = cut.InvokeAsync(() => ItemFor(cut, DeleteLabel).ClickAsync());
        Task shown = cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        // A click that fails (no such item, a stale handler) completes first; awaiting it surfaces that error instead of a bare wait timeout.
        await await Task.WhenAny(deleting, shown);
        await shown;
        return deleting;
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
        [.. cut.FindAll("div.mud-menu-item").Select(item => item.TextContent.Trim())];

    /// <summary>Clicks the title row's View actions button, opening the menu.</summary>
    private static async Task OpenMenuAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await cut.InvokeAsync(() => cut.Find(".tasks-header button[aria-label^='Actions for ']").ClickAsync());
    }

    /// <summary>The menu item whose label is <paramref name="label"/>.</summary>
    private static IElement ItemFor(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindAll("div.mud-menu-item").Single(item => string.Equals(item.TextContent.Trim(), label, StringComparison.Ordinal));

    private static IElement DialogButton(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));
}

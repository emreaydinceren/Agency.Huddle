namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using TestContext = Xunit.TestContext;

/// <summary>
/// Pins Spec §13.1: the nav lists the built-in Views then the file's own Views as
/// <c>/tasks/{id}</c> links, offers <c>New View</c>, flags an invalid View with a warning icon,
/// re-renders on <see cref="ViewStore.ViewsChanged"/>, and renders nothing when
/// <see cref="TasksOptions.Enabled"/> is <see langword="false"/>. Also pins the row's own "..." menu
/// (TaskViewNav.razor's own file header, RoomList.razor's precedent): Rename for any row, Delete
/// View only for a non-built-in one, reachable by the "..." button or a right-click alike, and
/// Delete's own confirm wording and its navigate-away-only-if-on-screen rule.
/// </summary>
public sealed class TaskViewNavTests
{
    /// <summary>The built-ins come first, then the file's own View, each linking to its own <c>/tasks/{id}</c> route.</summary>
    [Fact]
    public async Task TaskViewNav_ListsBuiltInsThenFileViews_WithTasksHrefs()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        List<string?> hrefs = [.. cut.FindAll("a").Select(a => a.GetAttribute("href"))];
        Assert.Equal(["/tasks/all-tasks", "/tasks/my-tasks", "/tasks/custom1", "/tasks/new"], hrefs);

        var customLink = cut.FindAll("a").Single(a => string.Equals(a.GetAttribute("href"), "/tasks/custom1", StringComparison.Ordinal));
        Assert.Equal("Sprint Board", customLink.TextContent.Trim());
    }

    /// <summary>A <c>New View</c> link, pointing at <c>/tasks/new</c>, is always offered.</summary>
    [Fact]
    public async Task TaskViewNav_ShowsNewViewLink_ToTasksNew()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        var link = cut.FindAll("a").Single(a => string.Equals(a.GetAttribute("href"), "/tasks/new", StringComparison.Ordinal));
        Assert.Equal("New view", link.TextContent.Trim());
    }

    /// <summary>An invalid View entry that still carries an id is shown as a link, marked with a warning icon.</summary>
    [Fact]
    public async Task TaskViewNav_InvalidViewWithId_ShowsWarningIcon()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await File.WriteAllTextAsync(
            Path.Combine(harness.Options.Value.DataDir, "views.json"),
            """{"version":1,"views":[{"id":"broken1","name":"Broken View","kind":"list","fields":["notAField"]}]}""",
            ct);
        harness.Views.Dispose();
        using ViewStore reloaded = new(harness.Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<ViewStore>.Instance);

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true, views: reloaded);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        var link = cut.FindAll("a").Single(a => string.Equals(a.GetAttribute("href"), "/tasks/broken1", StringComparison.Ordinal));
        Assert.NotNull(link.QuerySelector(".mud-icon-root"));
    }

    /// <summary>An invalid View entry with no id (the file's entry never had one, or failed to parse before one could be read) renders as a non-link item, still with the warning icon.</summary>
    [Fact]
    public async Task TaskViewNav_InvalidViewWithNullId_RendersAsNonLinkItem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        await File.WriteAllTextAsync(
            Path.Combine(harness.Options.Value.DataDir, "views.json"),
            """{"version":1,"views":[{"name":"No Id View"}]}""",
            ct);
        harness.Views.Dispose();
        using ViewStore reloaded = new(harness.Options, Microsoft.Extensions.Logging.Abstractions.NullLogger<ViewStore>.Instance);

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true, views: reloaded);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        Assert.DoesNotContain(cut.FindAll("a"), a => string.Equals(a.TextContent.Trim(), "No Id View", StringComparison.Ordinal));
        Assert.Equal("No Id View", cut.Find(".task-view-nav-invalid-item").TextContent.Trim());
        Assert.NotNull(cut.Find(".mud-icon-root"));
    }

    /// <summary>A live <see cref="ViewStore.ViewsChanged"/> publish re-queries and re-renders the list.</summary>
    [Fact]
    public async Task ViewsChanged_ReRenders()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);
        Assert.DoesNotContain("Sprint Board", cut.Markup, StringComparison.Ordinal);

        _ = harness.Views.Save(new TaskView { Id = "custom2", Name = "Sprint Board", Kind = ViewKind.List });

        cut.WaitForAssertion(() =>
        {
            var link = cut.FindAll("a").Single(a => string.Equals(a.GetAttribute("href"), "/tasks/custom2", StringComparison.Ordinal));
            Assert.Equal("Sprint Board", link.TextContent.Trim());
        });
    }

    /// <summary>With <see cref="TasksOptions.Enabled"/> off, the component renders nothing at all.</summary>
    [Fact]
    public async Task TasksDisabled_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: false);
        var cut = ctx.Render<TaskViewNav>();

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>Every row's "..." menu offers Rename; a non-built-in row also offers Delete View, but a built-in one (here All Tasks) does not.</summary>
    [Fact]
    public async Task RowMenu_OffersEditView_AndDeleteViewOnlyForNonBuiltIn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenRowMenuAsync(cut, "All Tasks");
        Assert.Equal(["Rename…"], MenuTexts(cut));

        await OpenRowMenuAsync(cut, "Sprint Board");
        Assert.Equal(["Rename…", "Delete view…"], MenuTexts(cut));
    }

    /// <summary>Right-clicking a row is the second door onto its own menu, mirroring <c>RoomList</c>'s own precedent.</summary>
    [Fact]
    public async Task RightClickingARow_OpensTheSameMenu()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await cut.InvokeAsync(() => RowFor(cut, "Sprint Board").ContextMenuAsync());

        Assert.Equal(["Rename…", "Delete view…"], MenuTexts(cut));
    }

    /// <summary>
    /// A View saved after the nav rendered (including the store's own watcher echo) must not cost the existing
    /// rows their right-click: the nav once cleared every menu ref on ViewsChanged, and Blazor never re-assigns a
    /// surviving component's ref, so right-click silently opened nothing until the page was reloaded.
    /// </summary>
    [Fact]
    public async Task RightClickingARow_AfterAnotherViewIsSaved_StillOpensTheMenu()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        _ = await ViewStoreEcho.SaveAndSettleAsync(harness.Views, new TaskView { Id = "custom2", Name = "Later View", Kind = ViewKind.List }, ct);
        cut.WaitForAssertion(() => Assert.NotNull(RowFor(cut, "Later View")));

        await cut.InvokeAsync(() => RowFor(cut, "Sprint Board").ContextMenuAsync());

        _ = Assert.Single(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Rename…", StringComparison.Ordinal));
    }

    /// <summary>
    /// Deleting a View listed above another must leave the survivor's right-click on its own menu: rows are keyed
    /// by View id, so the survivor keeps its MudMenu instance (and ref) instead of inheriting the deleted row's slot.
    /// </summary>
    [Fact]
    public async Task RightClickingARow_AfterTheViewAboveIsDeleted_StillOpensTheMenu()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Alpha", Kind = ViewKind.List });
        _ = harness.Views.Save(new TaskView { Id = "custom2", Name = "Beta", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        _ = harness.Views.Delete("custom1");
        cut.WaitForAssertion(() => Assert.DoesNotContain(
            cut.FindAll("div.hover-reveal-row"),
            row => string.Equals(row.QuerySelector("a")?.TextContent.Trim(), "Alpha", StringComparison.Ordinal)));

        await cut.InvokeAsync(() => RowFor(cut, "Beta").ContextMenuAsync());

        _ = Assert.Single(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Delete view…", StringComparison.Ordinal));
    }

    /// <summary>Delete View opens <c>ShowMessageBoxAsync</c> with the exact wording the Tasks page's own Delete uses.</summary>
    [Fact]
    public async Task DeleteView_MessageBox_HasTheExactTitleTextAndButtons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenRowMenuAsync(cut, "Sprint Board");
        Task deleting = await OpenDeleteDialogAsync(cut);

        Assert.Equal("Delete \"Sprint Board\"?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("This removes the View. Tasks are not affected.", cut.Find(".mud-dialog-content").TextContent.Trim());
        var buttons = cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Cancel", "Delete"], buttons);

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());
        await deleting;
    }

    /// <summary>Cancelling Delete View keeps the View in <see cref="ViewStore"/> and in the list.</summary>
    [Fact]
    public async Task DeleteView_Cancelled_KeepsTheView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenRowMenuAsync(cut, "Sprint Board");
        Task deleting = await OpenDeleteDialogAsync(cut);
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).ClickAsync());
        await deleting;

        Assert.NotNull(harness.Views.Get("custom1"));
        Assert.Contains("Sprint Board", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>Confirming Delete View removes it from <see cref="ViewStore"/>; when that View is not the one currently on screen, the browser stays put.</summary>
    [Fact]
    public async Task DeleteView_Confirmed_RemovesTheView_StaysPutWhenNotOnScreen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string before = navigation.Uri;
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenRowMenuAsync(cut, "Sprint Board");
        Task deleting = await OpenDeleteDialogAsync(cut);
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());
        await deleting;

        Assert.Null(harness.Views.Get("custom1"));
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>Confirming Delete View for the View currently on screen replaces the URL with <c>/tasks</c>.</summary>
    [Fact]
    public async Task DeleteView_Confirmed_NavigatesToTasks_WhenItIsTheOneOnScreen()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tasks/custom1");
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        await OpenRowMenuAsync(cut, "Sprint Board");
        Task deleting = await OpenDeleteDialogAsync(cut);
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).ClickAsync());
        await deleting;

        Assert.EndsWith("/tasks", navigation.Uri, StringComparison.Ordinal); // contains-ok: a full URI, whose scheme/host prefix bUnit's FakeNavigationManager owns; only the path is this test's concern.
    }

    /// <summary>Registers the harness's services into a fresh MudBlazor-aware bUnit context, optionally overriding <see cref="TasksOptions.Enabled"/> or the harness's own <see cref="ViewStore"/> (for a reload after a hand edit to <c>views.json</c>).</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness, bool tasksEnabled, ViewStore? views = null)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        if (views is not null)
        {
            ctx.Services.AddSingleton(views);
        }

        if (!tasksEnabled)
        {
            ctx.Services.AddSingleton(Options.Create(new TeamOptions { DataDir = harness.Options.Value.DataDir, Tasks = new TasksOptions { Enabled = false } }));
        }

        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderNav(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskViewNav>(0);
            builder.CloseComponent();
        });

    /// <summary>The row <c>div.hover-reveal-row</c> whose link text is <paramref name="viewName"/>.</summary>
    private static AngleSharp.Dom.IElement RowFor(IRenderedComponent<ContainerFragment> cut, string viewName) =>
        cut.FindAll("div.hover-reveal-row").Single(row => string.Equals(row.QuerySelector("a")?.TextContent.Trim(), viewName, StringComparison.Ordinal));

    /// <summary>
    /// Clicks the row's "..." button for <paramref name="viewName"/>, opening its <c>MudMenu</c> popover - a real click on a real <c>button</c>, so this doubles as proof the trigger is keyboard-reachable rather than right-click-only.
    /// Awaits the click's own dispatch: bUnit's synchronous <c>Click()</c> discards the dispatch task, so under load a render already holding the renderer dispatcher defers the handler past the return.
    /// </summary>
    private static async Task OpenRowMenuAsync(IRenderedComponent<ContainerFragment> cut, string viewName)
    {
        await cut.InvokeAsync(() =>
        {
            AngleSharp.Dom.IElement? button = RowFor(cut, viewName).QuerySelector("button[aria-label^='Actions for ']");
            Assert.NotNull(button);
            return button.ClickAsync();
        });
    }

    /// <summary>The distinct texts of the open menu items, in order (a menu opened earlier may still be open, so a text can repeat).</summary>
    private static IReadOnlyList<string> MenuTexts(IRenderedComponent<ContainerFragment> cut) =>
        [.. MenuItems(cut).Select(item => item.TextContent.Trim()).Distinct(StringComparer.Ordinal)];

    /// <summary>Every open <c>MudMenu</c> item - rendered with <c>role="menuitem"</c> and class <c>mud-menu-item</c>, not <c>mud-list-item</c> (that class belongs to <c>MudSelect</c>'s own popover).</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> MenuItems(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-menu-item");

    /// <summary>
    /// Clicks the open menu's <c>Delete view</c> item and returns once the confirm dialog's buttons are rendered. The handler
    /// awaits the dialog's result, so the click is started rather than awaited, and the returned task completes only when the
    /// handler has finished (after the dialog is answered and, on Delete, the View is removed). Waiting asynchronously, rather than
    /// blocking in <c>WaitForAssertion</c>, leaves the pool thread the handler and the dialog render need free; a blocked test
    /// thread starved them on a busy runner and the wait saw no render at all.
    /// </summary>
    /// <param name="cut">The rendered nav and its dialog provider.</param>
    /// <returns>The click's task, for the test to await once it has answered the dialog.</returns>
    private static async Task<Task> OpenDeleteDialogAsync(IRenderedComponent<ContainerFragment> cut)
    {
        Task deleting = cut.InvokeAsync(() => MenuItems(cut).First(item => string.Equals(item.TextContent.Trim(), "Delete view…", StringComparison.Ordinal)).ClickAsync());
        Task shown = cut.WaitForAssertionAsync(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        // A click that fails (no such item, a stale handler) completes first; awaiting it surfaces that error instead of a bare wait timeout.
        await await Task.WhenAny(deleting, shown);
        await shown;
        return deleting;
    }
}

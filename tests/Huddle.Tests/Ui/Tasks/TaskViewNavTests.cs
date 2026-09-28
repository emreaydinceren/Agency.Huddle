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
/// (TaskViewNav.razor's own file header, RoomList.razor's precedent): Edit View for any row, Delete
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

    /// <summary>Every row's "..." menu offers Edit View; a non-built-in row also offers Delete View, but a built-in one (here All Tasks) does not.</summary>
    [Fact]
    public async Task RowMenu_OffersEditView_AndDeleteViewOnlyForNonBuiltIn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        OpenRowMenu(cut, "All Tasks");
        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Edit view", StringComparison.Ordinal));
        Assert.DoesNotContain(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Delete view", StringComparison.Ordinal));

        OpenRowMenu(cut, "Sprint Board");
        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Edit view", StringComparison.Ordinal));
        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Delete view", StringComparison.Ordinal));
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

        RowFor(cut, "Sprint Board").ContextMenu();

        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Edit view", StringComparison.Ordinal));
        Assert.Contains(MenuItems(cut), item => string.Equals(item.TextContent.Trim(), "Delete view", StringComparison.Ordinal));
    }

    /// <summary>Clicking Edit View opens this component's own <see cref="ViewEditorDrawer"/> for that row's View.</summary>
    [Fact]
    public async Task EditView_OpensTheViewEditorDrawerForThatView()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        OpenRowMenu(cut, "Sprint Board");
        ClickMenuItem(cut, "Edit view");

        var drawer = cut.FindComponent<ViewEditorDrawer>();
        Assert.True(drawer.Instance.Open);
        Assert.Equal("custom1", drawer.Instance.Id);
    }

    /// <summary>Delete View opens <c>ShowMessageBoxAsync</c> with the exact wording <see cref="ViewEditorDrawer"/>'s own Delete uses.</summary>
    [Fact]
    public async Task DeleteView_MessageBox_HasTheExactTitleTextAndButtons()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        _ = harness.Views.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        await using MudBunitContext ctx = NewContext(harness, tasksEnabled: true);
        IRenderedComponent<ContainerFragment> cut = RenderNav(ctx);

        OpenRowMenu(cut, "Sprint Board");
        _ = cut.InvokeAsync(() => ClickMenuItem(cut, "Delete view"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));

        Assert.Equal("Delete View", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Delete 'Sprint Board'? Tasks are not affected.", cut.Find(".mud-dialog-content").TextContent.Trim());
        var buttons = cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Cancel", "Delete"], buttons);

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());
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

        OpenRowMenu(cut, "Sprint Board");
        _ = cut.InvokeAsync(() => ClickMenuItem(cut, "Delete view"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Cancel", StringComparison.Ordinal)).Click());

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

        OpenRowMenu(cut, "Sprint Board");
        _ = cut.InvokeAsync(() => ClickMenuItem(cut, "Delete view"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).Click());

        Assert.Null(harness.Views.Get("custom1"));
        Assert.Equal(before, navigation.Uri);
    }

    /// <summary>Confirming Delete View for the View currently on screen replaces the URL with <c>/tasks</c>, mirroring <see cref="ViewEditorDrawer"/>'s own Delete.</summary>
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

        OpenRowMenu(cut, "Sprint Board");
        _ = cut.InvokeAsync(() => ClickMenuItem(cut, "Delete view"));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-actions button")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).Click());

        Assert.EndsWith("/tasks", navigation.Uri, StringComparison.Ordinal); // contains-ok: a full URI, whose scheme/host prefix bUnit's FakeNavigationManager owns; only the path is this test's concern.
    }

    /// <summary>Registers the harness's services into a fresh MudBlazor-aware bUnit context, optionally overriding <see cref="TasksOptions.Enabled"/> or the harness's own <see cref="ViewStore"/> (for a reload after a hand edit to <c>views.json</c>).</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness, bool tasksEnabled, ViewStore? views = null)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
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

    /// <summary>Clicks the row's "..." button for <paramref name="viewName"/>, opening its <c>MudMenu</c> popover - a real click on a real <c>button</c>, so this doubles as proof the trigger is keyboard-reachable rather than right-click-only.</summary>
    private static void OpenRowMenu(IRenderedComponent<ContainerFragment> cut, string viewName)
    {
        RowFor(cut, viewName).QuerySelector("button[aria-label='View actions']")!.Click();
    }

    /// <summary>Every open <c>MudMenu</c> item - rendered with <c>role="menuitem"</c> and class <c>mud-menu-item</c>, not <c>mud-list-item</c> (that class belongs to <c>MudSelect</c>'s own popover).</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> MenuItems(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("div.mud-menu-item");

    private static void ClickMenuItem(IRenderedComponent<ContainerFragment> cut, string text)
    {
        MenuItems(cut).First(item => string.Equals(item.TextContent.Trim(), text, StringComparison.Ordinal)).Click();
    }
}

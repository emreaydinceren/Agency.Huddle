using Agency.Huddle.Tests.Factories;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.2 (the Tasks page) and §13.12 (alerts and empty states). Split the same way
/// <c>AgentsPageTests</c> is: routing, HTTP status and raw markup facts stay on a plain HTTP GET
/// against <see cref="TeamWebApplicationFactory"/>, while the live-update pattern (§13.11) - a
/// requery on <see cref="TaskEvents.TasksReloaded"/>, and unsubscribing on <see cref="IDisposable.Dispose"/> -
/// renders <see cref="TasksPage"/> through <see cref="MudBunitContext"/> and <see cref="TaskToolHarness"/>
/// instead, since only bUnit exposes the event hub the page subscribes to directly.
/// </summary>
public sealed class TasksPageTests
{
    /// <summary>During prerender, with no ViewId in the route, the page shows the built-in All Tasks View.</summary>
    [Fact]
    public async Task TasksPage_NoViewId_PrerenderShowsAllTasks()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/tasks", ct);
        IDocument document = new HtmlParser().ParseDocument(html);
        Assert.Empty(document.QuerySelectorAll(".tasks-header h1"));
        Assert.NotNull(document.QuerySelector(".tasks-header .task-toolbar-header .task-toolbar-kind"));
        Assert.Equal("All Tasks", NormalizeWhitespace(document.Title ?? string.Empty));
    }

    /// <summary>The header row follows the wireframe: List | Kanban, then the open, need-you and all counts, on the left; New task on the right; and Group by is the toolbar row's last control, with no Group menu.</summary>
    [Fact]
    public async Task TasksPage_HeaderRow_HasKanbanThenCountsAndNewTask_AndGroupByEndsTheToolbarRow()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/tasks", ct);
        IDocument document = new HtmlParser().ParseDocument(html);
        IElement header = document.QuerySelector(".tasks-header .task-toolbar-header") ?? throw new InvalidOperationException("No header row in the response.");

        // T2's left side: List | Kanban first, then the counts.
        Assert.Contains("task-toolbar-kind", header.Children[0].ClassList);
        Assert.Contains("tasks-counts", header.Children[1].ClassList);
        Assert.EndsWith("open", NormalizeWhitespace(header.QuerySelector(".tasks-count-open")?.TextContent ?? string.Empty), StringComparison.Ordinal);
        Assert.EndsWith("need you", NormalizeWhitespace(header.QuerySelector(".tasks-count-need-you")?.TextContent ?? string.Empty), StringComparison.Ordinal);
        Assert.EndsWith("all", NormalizeWhitespace(header.QuerySelector(".tasks-count-all")?.TextContent ?? string.Empty), StringComparison.Ordinal);
        Assert.Equal(["List", "Kanban"], header.QuerySelectorAll(".task-toolbar-kind .mud-toggle-item").Select(item => NormalizeWhitespace(item.TextContent)));

        // Group by left the header row and ends the filter row (T3c).
        Assert.Null(header.QuerySelector(".task-toolbar-group"));
        IElement toolbar = document.QuerySelector(".tasks-header .task-toolbar") ?? throw new InvalidOperationException("No toolbar row in the response.");
        Assert.NotNull(toolbar.Children[^1].QuerySelector(".task-toolbar-group")); // the chip sits in MudBlazor's display: contents container
        Assert.StartsWith("Group by:", NormalizeWhitespace(toolbar.Children[^1].TextContent), StringComparison.Ordinal);
        Assert.Equal("New task", NormalizeWhitespace(header.QuerySelector(".tasks-new-task")?.TextContent ?? string.Empty));
        Assert.Null(document.QuerySelector(".page-header")); // T2: no visible page header
        Assert.Empty(document.QuerySelectorAll(".task-toolbar .task-toolbar-group-sort, .task-toolbar .task-toolbar-grouping"));
    }

    /// <summary>A ViewId naming no known View - built-in, valid or invalid - shows the "no longer exists" warning, with <c>role="status"</c>.</summary>
    [Fact]
    public async Task TasksPage_UnknownViewId_ShowsViewNoLongerExistsWithStatusRole()
    {
        // A fresh factory boots a whole host and JITs the first request; under CI load that alone outlasted ten seconds.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/tasks/does-not-exist", ct);
        IDocument document = new HtmlParser().ParseDocument(html);
        IElement alert = document.QuerySelector(".tasks-view-not-found-alert") ?? throw new InvalidOperationException("No .tasks-view-not-found-alert in the response.");

        Assert.Equal("status", alert.GetAttribute("role"));
        Assert.Equal("That View no longer exists. All Tasks", NormalizeWhitespace(alert.TextContent));
    }

    /// <summary>A Task file the store could not parse is surfaced as "Tasks that didn't load", with <c>role="status"</c> (Spec §13.12, traps.md L22-27's precedent gap).</summary>
    [Fact]
    public async Task TasksPage_RejectedTaskFile_ShowsTasksDidntLoadWithStatusRole()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        string tasksFolder = Path.Combine(factory.TasksDirPath, "Platform", "_tasks");
        Directory.CreateDirectory(tasksFolder);
        // Empty frontmatter delimiters, no fields: guaranteed to fail every required-field check,
        // the same fixture TaskStoreTests.cs:95 uses for "definitely rejected". It still lives
        // where TryMap maps it to a location, so the store attempts to parse it and rejects it.
        await File.WriteAllTextAsync(Path.Combine(tasksFolder, "garbage.md"), "---\n---\n", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/tasks", ct);
        IDocument document = new HtmlParser().ParseDocument(html);
        IElement alert = document.QuerySelector(".tasks-rejected-alert") ?? throw new InvalidOperationException("No .tasks-rejected-alert in the response.");
        IElement heading = document.QuerySelector(".tasks-rejected-heading") ?? throw new InvalidOperationException("No .tasks-rejected-heading in the response.");

        // AngleSharp decodes the apostrophe entity back to "'" - the same text Razor's own
        // HTML-encoding (&#x27;) renders on the wire.
        Assert.Equal("status", alert.GetAttribute("role"));
        Assert.Equal("Tasks that didn't load", heading.TextContent.Trim());
    }

    /// <summary>A malformed <c>views.json</c> is surfaced as <see cref="ViewLoadError"/>, with <c>role="alert"</c> and the exact wording Spec §13.12 gives (message, then the "read-only until fixed" reassurance).</summary>
    [Fact]
    public async Task TasksPage_ViewsJsonLoadError_ShowsErrorAlertWithAlertRole()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        var dataDirPath = Path.GetDirectoryName(factory.TasksDirPath) ?? throw new InvalidOperationException("TasksDirPath has no parent.");
        Directory.CreateDirectory(dataDirPath);
        const string malformed = "{ not json";
        string viewsJsonPath = Path.Combine(dataDirPath, "views.json");
        await File.WriteAllTextAsync(viewsJsonPath, malformed, ct);

        // Line/column and message computed independently here from JsonNode.Parse's own JsonException,
        // the same technique ViewStoreTests.MalformedFile_LoadErrorWithLineAndColumn_SaveRefused_FileByteIdentical
        // uses, rather than re-deriving ViewStore's own arithmetic.
        JsonException? parseError = null;
        try
        {
            JsonNode.Parse(malformed);
        }
        catch (JsonException ex)
        {
            parseError = ex;
        }

        Assert.NotNull(parseError);
        long line = (parseError.LineNumber ?? 0) + 1;
        long column = (parseError.BytePositionInLine ?? 0) + 1;
        string expected = string.Create(
            CultureInfo.InvariantCulture,
            $"views.json could not be read (line {line}, column {column}): '{viewsJsonPath}' could not be parsed: {parseError.Message}. Views are read-only until the file is fixed; nothing has been lost.");

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/tasks", ct);
        IDocument document = new HtmlParser().ParseDocument(html);
        IElement alert = document.QuerySelector(".tasks-views-load-error-alert") ?? throw new InvalidOperationException("No .tasks-views-load-error-alert in the response.");

        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Equal(expected, NormalizeWhitespace(alert.TextContent));
    }

    /// <summary>With Tasks turned off, <c>/tasks</c> shows only the "turned off" notice (corrections-B4 D11 item 13), never the toolbar or list.</summary>
    [Fact]
    public async Task TasksPage_TasksDisabled_ShowsTurnedOffAlert()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var disabled = factory.WithWebHostBuilder(builder => builder.UseSetting("Team:Tasks:Enabled", "false"));
        using var client = disabled.CreateClient();

        var html = await client.GetStringAsync("/tasks", ct);
        IDocument document = new HtmlParser().ParseDocument(html);
        IElement alert = document.QuerySelector(".tasks-disabled-alert") ?? throw new InvalidOperationException("No .tasks-disabled-alert in the response.");

        Assert.Equal("status", alert.GetAttribute("role"));
        Assert.Equal("Tasks are turned off.", alert.TextContent.Trim());
    }

    /// <summary>
    /// Raising <see cref="TaskEvents.TasksReloaded"/> makes the page pick up a Task that was written
    /// straight to disk (bypassing <see cref="TaskService"/>, so no <see cref="TaskEvents.TaskChanged"/>
    /// fires) once <see cref="TaskStore.RebuildFromWatcher"/> rescans it - proving the subscription in
    /// Spec §13.11 actually drives a requery, not just a re-render of stale data.
    /// </summary>
    [Fact]
    public async Task TasksPage_TasksReloaded_RequeriesTheTaskList()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        TestTaskStore.WriteTask(
            harness.TasksDirPath,
            Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "Freshly written on disk"));
        harness.Store.RebuildFromWatcher();

        cut.WaitForAssertion(() => Assert.Equal("Freshly written on disk", cut.Find(".task-list-title").TextContent.Trim()));
    }

    /// <summary>
    /// Creating a Task through <see cref="TaskService"/> raises <see cref="TaskEvents.TaskChanged"/>
    /// directly (<c>TaskService.Create</c>'s own call to <c>events.RaiseTaskChanged</c>), and the page
    /// must requery on that event too - narrowed from Spec §13.2's full subscription list to exactly
    /// <c>TasksReloaded</c>, <c>TaskChanged</c> and <c>ViewsChanged</c> by corrections-B4 D11 item 9.
    /// </summary>
    [Fact]
    public async Task TasksPage_TaskChanged_RequeriesTheTaskList()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        _ = harness.Service.Create(
            new TaskDraft("Freshly created via TaskService", "Platform", null),
            TaskActors.Human(harness.Options.Value));

        cut.WaitForAssertion(() => Assert.Equal("Freshly created via TaskService", cut.Find(".task-list-title").TextContent.Trim()));
    }

    /// <summary>Saving a change through <see cref="ViewStore"/> raises <see cref="ViewStore.ViewsChanged"/>, and the page must re-read the effective View (its name and, through it, its filter) from the store rather than the one it first rendered with.</summary>
    [Fact]
    public async Task TasksPage_ViewsChanged_RefreshesTheViewFromTheStore()
    {
        using TaskToolHarness harness = new();
        Assert.True(harness.Views.Save(new TaskView { Id = "custom-view", Name = "Sprint Board", Kind = ViewKind.List }).Saved);

        await using MudBunitContext ctx = NewContext(harness);
        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), "custom-view");
            builder.CloseComponent();
        });
        cut.WaitForAssertion(() => Assert.Equal("Sprint Board", cut.FindComponent<TaskToolbar>().Instance.EffectiveView.Name));

        Assert.True(harness.Views.Save(new TaskView { Id = "custom-view", Name = "Sprint Board Renamed", Kind = ViewKind.List }).Saved);

        cut.WaitForAssertion(() => Assert.Equal("Sprint Board Renamed", cut.FindComponent<TaskToolbar>().Instance.EffectiveView.Name));
    }

    /// <summary>
    /// Spec §13.9: the page writes the current ViewId to <c>huddle.tasks.lastView</c> through
    /// <c>set</c> in <c>storage.js</c>, only from <c>OnAfterRenderAsync</c> - bUnit runs that lifecycle
    /// method during a normal render, so the JSInterop call is directly observable here.
    /// </summary>
    [Fact]
    public async Task TasksPage_AfterFirstRender_StoresLastView()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        ctx.Module(JsModule.Storage).SetupVoid("set", new object[] { "huddle.tasks.lastView", ViewStore.AllTasksId });

        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), ViewStore.AllTasksId);
            builder.CloseComponent();
        });

        ctx.Module(JsModule.Storage).VerifyInvoke("set");
    }

    /// <summary>U13 (UX-047): the page looks up Room names, so a List row's Origin shows the origin Room's name and not its id.</summary>
    [Fact]
    public async Task TasksPage_Origin_ShowsTheRoomName()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        await harness.Directory.InitializeAsync("You", ct);
        Agency.Huddle.App.Data.User human = await harness.Directory.GetHumanAsync(ct);
        Agency.Huddle.App.Data.Room room = await harness.Directory.CreateRoomAsync("Launch room", [human.Id], ct);
        TestTaskStore.WriteTask(
            harness.TasksDirPath,
            Path.Combine("Platform", "_tasks", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "Has an origin", originRoomId: room.Id));
        harness.Store.RebuildFromWatcher();
        Assert.True(harness.Views.Save(new TaskView { Id = "origin-view", Name = "Origins", Kind = ViewKind.List, Fields = ["origin"] }).Saved);

        await using MudBunitContext ctx = NewContext(harness);
        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), "origin-view");
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("td"), cell => string.Equals(cell.TextContent.Trim(), "Launch room", StringComparison.Ordinal)));
    }

    /// <summary>Corrections-B7 §13.2 item 4: a route naming an invalid View (parses, but fails <see cref="ViewValidator"/>) shows its reason and an Edit View button, not "no longer exists".</summary>
    [Fact]
    public async Task TasksPage_InvalidViewRoute_ShowsItsReasonAndEditViewButton()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        // Kind "list" with an empty name fails only ValidateName ("Name must be 1-60 characters."),
        // so the raw entry parses but ViewValidator refuses it - exactly the InvalidView case, not LoadError.
        await File.WriteAllTextAsync(Path.Combine(dir.Path, "views.json"), "{\"version\":1,\"views\":[{\"id\":\"bad-view\",\"name\":\"\",\"kind\":\"list\"}]}", ct);
        PersonaStore personas = dir.CreatePersonaStore();
        TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        using ViewStore views = new(dir.Options(), NullLogger<ViewStore>.Instance);
        Assert.Single(views.InvalidViews);
        using SprintStore sprints = new(dir.Options(), store, events, NullLogger<SprintStore>.Instance);

        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(personas);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton(views);
        ctx.Services.AddSingleton(sprints);
        ctx.Services.AddSingleton<ISprintCatalog>(sprints);
        ctx.Services.AddSingleton<Agency.Huddle.App.Data.ITeamDirectory>(new Agency.Huddle.App.Data.SqliteTeamDirectory(dir.Options()));
        ctx.Services.AddSingleton(TestOptions.Create());
        ctx.Services.AddSingleton<Agency.Huddle.App.Profile.IHumanIdentity>(new Agency.Huddle.App.Profile.ConfiguredHumanIdentity("You"));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        // 14.5.i: the page always hosts WakeToasts (Spec §13.8), which @injects TaskActivity.
        ctx.Services.AddSingleton<TaskActivity>();

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), "bad-view");
            builder.CloseComponent();
        });

        Assert.Empty(cut.FindAll(".tasks-view-not-found-alert"));
        var alert = cut.Find(".tasks-invalid-view-alert");
        Assert.Equal("This View is invalid: Name must be 1-60 characters.", alert.TextContent.Trim());
        Assert.Empty(cut.FindAll(".tasks-invalid-view-edit"));
    }

    /// <summary><c>/tasks/new</c> creates a View under the default name "New view" and replaces the URL with its own, rather than falling through to All Tasks.</summary>
    [Fact]
    public async Task TasksPage_NavigatedToTasksNew_CreatesAViewAndOpensIt()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tasks/new");

        _ = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        TaskView created = Assert.Single(harness.Views.Views, view => string.Equals(view.Name, "New view", StringComparison.Ordinal));
        Assert.EndsWith($"/tasks/{created.Id}", navigation.Uri, StringComparison.Ordinal); // contains-ok: a full URI, whose scheme/host prefix bUnit's FakeNavigationManager owns; only the path is this test's concern.
    }
    /// <summary>
    /// Disposing the page removes its handlers from all three hubs it subscribes to
    /// (<see cref="TaskEvents.TasksReloaded"/>, <see cref="TaskEvents.TaskChanged"/> and
    /// <see cref="ViewStore.ViewsChanged"/>) - checked by counting subscribers via reflection before
    /// and after, per corrections-B4 D11 item 11 ("assert a re-query counter / subscriber count", not
    /// "nothing throws").
    /// </summary>
    [Fact]
    public async Task TasksPage_Dispose_UnsubscribesFromAllThreeHubs()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);

        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        Assert.Equal(1, SubscriberCount(harness.Events, nameof(TaskEvents.TasksReloaded)));
        // 2, not 1: the page's own, plus the SprintStore the page injects (the harness builds an empty one on demand),
        // which subscribes to the same hub to remove a vacated Sprint (Sprints spec §8.2).
        Assert.Equal(2, SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged)));
        Assert.Equal(1, SubscriberCount(harness.Views, nameof(ViewStore.ViewsChanged)));

        await ctx.DisposeAsync();

        Assert.Equal(0, SubscriberCount(harness.Events, nameof(TaskEvents.TasksReloaded)));
        Assert.Equal(0, SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged)));
        Assert.Equal(0, SubscriberCount(harness.Views, nameof(ViewStore.ViewsChanged)));
    }

    /// <summary>Collapses <paramref name="text"/>'s runs of whitespace (the newlines and indentation Razor's literal HTML/text carries between an alert's own text and a nested link) into single spaces, then trims the ends.</summary>
    /// <param name="text">The raw <c>TextContent</c> to normalise.</param>
    private static string NormalizeWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Registers the harness's services - exactly what the page and its children <c>@inject</c> - into a fresh <see cref="MudBunitContext"/>.</summary>
    /// <param name="harness">The Tasks stack to wire in.</param>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        harness.AddTo(ctx.Services);
        return ctx;
    }

    /// <summary>Counts <paramref name="source"/>'s current subscribers to the event named <paramref name="eventName"/>, via the compiler-generated backing field - the event itself offers no other way to observe its invocation list.</summary>
    /// <param name="source">The hub instance to inspect.</param>
    /// <param name="eventName">The event's own name, e.g. <c>nameof(TaskEvents.TasksReloaded)</c>.</param>
    private static int SubscriberCount(object source, string eventName)
    {
        FieldInfo field = source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"'{source.GetType().Name}' has no backing field for '{eventName}'.");

        return (field.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0;
    }
}

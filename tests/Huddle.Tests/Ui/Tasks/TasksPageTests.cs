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
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using TasksPage = Agency.Huddle.App.Components.Pages.Tasks;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins Spec §13.2 (the Tasks page) and §13.12 (alerts and empty states). Split the same way
/// <c>TeammatesPageTests</c> is: routing, HTTP status and raw markup facts stay on a plain HTTP GET
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
        IElement heading = document.QuerySelector(".tasks-header h1") ?? throw new InvalidOperationException("No .tasks-header h1 in the response.");

        Assert.Equal("All Tasks", NormalizeWhitespace(heading.TextContent));
    }

    /// <summary>A ViewId naming no known View - built-in, valid or invalid - shows the "no longer exists" warning, with <c>role="status"</c>.</summary>
    [Fact]
    public async Task TasksPage_UnknownViewId_ShowsViewNoLongerExistsWithStatusRole()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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

    /// <summary>Saving a rename through <see cref="ViewStore"/> raises <see cref="ViewStore.ViewsChanged"/>, and the page must re-read the effective View's own name from the store rather than the one it first rendered with.</summary>
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
        cut.WaitForAssertion(() => Assert.Equal("Sprint Board", cut.Find(".tasks-header h1").TextContent.Trim()));

        Assert.True(harness.Views.Save(new TaskView { Id = "custom-view", Name = "Sprint Board Renamed", Kind = ViewKind.List }).Saved);

        cut.WaitForAssertion(() => Assert.Equal("Sprint Board Renamed", cut.Find(".tasks-header h1").TextContent.Trim()));
    }

    /// <summary>
    /// Spec §13.9: the page writes the current ViewId to <c>huddle.tasks.lastView</c> through
    /// <c>huddleStorage.set</c>, only from <c>OnAfterRenderAsync</c> - bUnit runs that lifecycle
    /// method during a normal render, so the JSInterop call is directly observable here.
    /// </summary>
    [Fact]
    public async Task TasksPage_AfterFirstRender_StoresLastView()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        ctx.JSInterop.SetupVoid("huddleStorage.set", new object[] { "huddle.tasks.lastView", ViewStore.AllTasksId });

        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.AddAttribute(1, nameof(TasksPage.ViewId), ViewStore.AllTasksId);
            builder.CloseComponent();
        });

        ctx.JSInterop.VerifyInvoke("huddleStorage.set");
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
        PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        TaskEvents events = new();
        using ViewStore views = new(dir.Options(), NullLogger<ViewStore>.Instance);
        Assert.Single(views.InvalidViews);

        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(personas);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton(views);
        ctx.Services.AddSingleton(Options.Create(new TeamOptions()));
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

        await cut.InvokeAsync(() => cut.Find(".tasks-invalid-view-edit").ClickAsync());
        var drawer = cut.FindComponent<ViewEditorDrawer>();
        Assert.True(drawer.Instance.Open);
        Assert.Equal("bad-view", drawer.Instance.Id);
    }

    /// <summary><c>/tasks/new</c> opens <see cref="ViewEditorDrawer"/> for a new View (<c>Id</c> null), rather than falling through to All Tasks.</summary>
    [Fact]
    public async Task TasksPage_NavigatedToTasksNew_OpensTheViewEditorDrawerForANewView()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness);
        BunitNavigationManager navigation = (BunitNavigationManager)ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tasks/new");

        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        var drawer = cut.FindComponent<ViewEditorDrawer>();
        Assert.True(drawer.Instance.Open);
        Assert.Null(drawer.Instance.Id);
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
        MudBunitContext ctx = NewContext(harness);

        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TasksPage>(0);
            builder.CloseComponent();
        });

        Assert.Equal(1, SubscriberCount(harness.Events, nameof(TaskEvents.TasksReloaded)));
        Assert.Equal(1, SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged)));
        // 2, not 1: the page's own subscription plus ViewEditorDrawer's (13.2.i hosts it here now,
        // and it re-seeds its draft on ViewsChanged too - corrections-B5 D13 item 4).
        Assert.Equal(2, SubscriberCount(harness.Views, nameof(ViewStore.ViewsChanged)));

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

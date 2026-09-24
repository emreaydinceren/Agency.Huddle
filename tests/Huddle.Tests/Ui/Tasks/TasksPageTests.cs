using System.Reflection;
using Bunit;
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

        Assert.Contains("All Tasks", html, StringComparison.Ordinal);
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

        Assert.Contains("That View no longer exists.", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
    }

    /// <summary>A Task file the store could not parse is surfaced as "Tasks that didn't load", with <c>role="status"</c> (Spec §13.12, traps.md L22-27's precedent gap).</summary>
    [Fact]
    public async Task TasksPage_RejectedTaskFile_ShowsTasksDidntLoadWithStatusRole()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        Directory.CreateDirectory(factory.TasksDirPath);
        // Empty frontmatter delimiters, no fields: guaranteed to fail every required-field check,
        // the same fixture TaskStoreTests.cs:95 uses for "definitely rejected".
        await File.WriteAllTextAsync(Path.Combine(factory.TasksDirPath, "garbage.md"), "---\n---\n", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/tasks", ct);

        // Razor HTML-encodes the apostrophe as &#x27; - the same encoding TeammatesPageTests'
        // "didn't load" precedent would hit too, if it ever asserted on this exact text.
        Assert.Contains("Tasks that didn&#x27;t load", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
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
        await File.WriteAllTextAsync(Path.Combine(dataDirPath, "views.json"), "{ not json", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/tasks", ct);

        Assert.Contains("could not be read (line", html, StringComparison.Ordinal);
        Assert.Contains("Views are read-only until the file is fixed; nothing has been lost.", html, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
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

        Assert.Contains("Tasks are turned off.", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
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
            Path.Combine("Platform", "PLAT-0001.md"),
            TestTasks.Make(id: "PLAT-0001", title: "Freshly written on disk"));
        harness.Store.RebuildFromWatcher();

        cut.WaitForAssertion(() => Assert.Contains("Freshly written on disk", cut.Markup, StringComparison.Ordinal));
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

        cut.WaitForAssertion(() => Assert.Contains("Freshly created via TaskService", cut.Markup, StringComparison.Ordinal));
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
        cut.WaitForAssertion(() => Assert.Contains("Sprint Board", cut.Markup, StringComparison.Ordinal));

        Assert.True(harness.Views.Save(new TaskView { Id = "custom-view", Name = "Sprint Board Renamed", Kind = ViewKind.List }).Saved);

        cut.WaitForAssertion(() => Assert.Contains("Sprint Board Renamed", cut.Markup, StringComparison.Ordinal));
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
        Assert.Equal(1, SubscriberCount(harness.Views, nameof(ViewStore.ViewsChanged)));

        await ctx.DisposeAsync();

        Assert.Equal(0, SubscriberCount(harness.Events, nameof(TaskEvents.TasksReloaded)));
        Assert.Equal(0, SubscriberCount(harness.Events, nameof(TaskEvents.TaskChanged)));
        Assert.Equal(0, SubscriberCount(harness.Views, nameof(ViewStore.ViewsChanged)));
    }

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

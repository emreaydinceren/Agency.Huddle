namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

/// <summary>
/// Pins Spec §13.1: the nav lists the built-in Views then the file's own Views as
/// <c>/tasks/{id}</c> links, offers <c>+ New View</c>, flags an invalid View with a warning icon,
/// re-renders on <see cref="ViewStore.ViewsChanged"/>, and renders nothing when
/// <see cref="TasksOptions.Enabled"/> is <see langword="false"/>.
/// </summary>
public sealed class TaskViewNavTests
{
    /// <summary>The built-ins come first, then the file's own View, each linking to its own <c>/tasks/{id}</c> route.</summary>
    [Fact]
    public void TaskViewNav_ListsBuiltInsThenFileViews_WithTasksHrefs()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);
        _ = store.Save(new TaskView { Id = "custom1", Name = "Sprint Board", Kind = ViewKind.List });

        using var ctx = NewContext(store, tasksEnabled: true);
        var cut = ctx.Render<TaskViewNav>();

        Assert.Contains(cut.FindAll("a"), a => string.Equals(a.GetAttribute("href"), "/tasks/all-tasks", StringComparison.Ordinal));
        Assert.Contains(cut.FindAll("a"), a => string.Equals(a.GetAttribute("href"), "/tasks/my-tasks", StringComparison.Ordinal));
        Assert.Contains(cut.FindAll("a"), a => string.Equals(a.GetAttribute("href"), "/tasks/custom1", StringComparison.Ordinal));
        Assert.Contains("Sprint Board", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A <c>+ New View</c> link, pointing at <c>/tasks/new</c>, is always offered.</summary>
    [Fact]
    public void TaskViewNav_ShowsNewViewLink_ToTasksNew()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);

        using var ctx = NewContext(store, tasksEnabled: true);
        var cut = ctx.Render<TaskViewNav>();

        var link = cut.FindAll("a").Single(a => string.Equals(a.GetAttribute("href"), "/tasks/new", StringComparison.Ordinal));
        Assert.Contains("New View", link.TextContent, StringComparison.Ordinal);
    }

    /// <summary>An invalid View entry that still carries an id is shown as a link, marked with a warning icon.</summary>
    [Fact]
    public void TaskViewNav_InvalidViewWithId_ShowsWarningIcon()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(
            Path.Combine(dir.Path, "views.json"),
            """{"version":1,"views":[{"id":"broken1","name":"Broken View","kind":"list","fields":["notAField"]}]}""");
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);

        using var ctx = NewContext(store, tasksEnabled: true);
        var cut = ctx.Render<TaskViewNav>();

        var link = cut.FindAll("a").Single(a => string.Equals(a.GetAttribute("href"), "/tasks/broken1", StringComparison.Ordinal));
        Assert.NotNull(link.QuerySelector(".mud-icon-root"));
    }

    /// <summary>An invalid View entry with no id (the file's entry never had one, or failed to parse before one could be read) renders as a non-link item, still with the warning icon.</summary>
    [Fact]
    public void TaskViewNav_InvalidViewWithNullId_RendersAsNonLinkItem()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(
            Path.Combine(dir.Path, "views.json"),
            """{"version":1,"views":[{"name":"No Id View"}]}""");
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);

        using var ctx = NewContext(store, tasksEnabled: true);
        var cut = ctx.Render<TaskViewNav>();

        Assert.DoesNotContain(cut.FindAll("a"), a => string.Equals(a.TextContent.Trim(), "No Id View", StringComparison.Ordinal));
        Assert.Contains("No Id View", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find(".mud-icon-root"));
    }

    /// <summary>A live <see cref="ViewStore.ViewsChanged"/> publish re-queries and re-renders the list.</summary>
    [Fact]
    public void ViewsChanged_ReRenders()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);

        using var ctx = NewContext(store, tasksEnabled: true);
        var cut = ctx.Render<TaskViewNav>();
        Assert.DoesNotContain("Sprint Board", cut.Markup, StringComparison.Ordinal);

        _ = store.Save(new TaskView { Id = "custom2", Name = "Sprint Board", Kind = ViewKind.List });

        cut.WaitForAssertion(() => Assert.Contains("Sprint Board", cut.Markup, StringComparison.Ordinal));
    }

    /// <summary>With <see cref="TasksOptions.Enabled"/> off, the component renders nothing at all.</summary>
    [Fact]
    public void TasksDisabled_RendersNothing()
    {
        using var dir = new TempDataDir();
        using var store = new ViewStore(dir.Options(), NullLogger<ViewStore>.Instance);

        using var ctx = NewContext(store, tasksEnabled: false);
        var cut = ctx.Render<TaskViewNav>();

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    private static MudBunitContext NewContext(ViewStore store, bool tasksEnabled)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(Options.Create(new TeamOptions { Tasks = new TasksOptions { Enabled = tasksEnabled } }));
        return ctx;
    }
}

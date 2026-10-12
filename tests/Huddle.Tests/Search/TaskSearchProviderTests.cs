using Agency.Huddle.App;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Search;

/// <summary>Pins <see cref="TaskSearchProvider"/>: Active and Closed Tasks, title, id and assignee matches (spec §8.4, §7.4; GS-T5).</summary>
public sealed class TaskSearchProviderTests
{
    /// <summary>A title match is a NameContains hit that carries the Task's id, title, status, assignee, Team and Closed flag.</summary>
    [Fact]
    public async Task Title_MatchesWithTier()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Fix login page"));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query("login"), ct);

        List<SearchHit> expected = [new TaskHit(new TaskId("PLAT", 1), "Fix login page", TaskState.ToDo, null, "Platform", false, MatchTier.NameContains)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>A phrase that starts the Task's id matches at the NamePrefix tier, after the title misses.</summary>
    [Fact]
    public async Task Id_MatchesWithTier()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0042", "Fix login page"));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query("PLAT-004"), ct);

        List<SearchHit> expected = [new TaskHit(new TaskId("PLAT", 42), "Fix login page", TaskState.ToDo, null, "Platform", false, MatchTier.NamePrefix)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>An exact id ranks ExactName and is the only hit: it gets no special jump behaviour (D-14).</summary>
    [Fact]
    public async Task ExactId_RanksExactName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0042", "Fix login page"));
        harness.SeedOnDisk(MakeTask("PLAT-0043", "Write the spec"));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query("plat-0042"), ct);

        List<SearchHit> expected = [new TaskHit(new TaskId("PLAT", 42), "Fix login page", TaskState.ToDo, null, "Platform", false, MatchTier.ExactName)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>An assignee's Name that matches, when the title and id do not, is a ContentMatch.</summary>
    [Fact]
    public async Task Assignee_MatchesAsContentMatch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Write the spec", assignee: "Nova"));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query("nova"), ct);

        List<SearchHit> expected = [new TaskHit(new TaskId("PLAT", 1), "Write the spec", TaskState.ToDo, "Nova", "Platform", false, MatchTier.ContentMatch)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>The Active pass never returns a Closed Task, even when its title matches.</summary>
    [Fact]
    public async Task Active_NeverReturnsAClosedTask()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Fix login page"));
        harness.SeedOnDisk(MakeTask("PLAT-0002", "Fix login old", closed: true));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query("login"), ct);

        List<SearchHit> expected = [new TaskHit(new TaskId("PLAT", 1), "Fix login page", TaskState.ToDo, null, "Platform", false, MatchTier.NameContains)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>The Archived pass returns only Closed Tasks, and marks each one IsClosed.</summary>
    [Fact]
    public async Task Archived_ReturnsOnlyClosedTasks()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Fix login page"));
        harness.SeedOnDisk(MakeTask("PLAT-0002", "Fix login old", closed: true));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query("login", SearchScope.Archived), ct);

        List<SearchHit> expected = [new TaskHit(new TaskId("PLAT", 2), "Fix login old", TaskState.ToDo, null, "Platform", true, MatchTier.NameContains)];
        Assert.Equal(expected, result.Hits);
    }

    /// <summary>An empty phrase finds nothing, so Tasks are never browsed.</summary>
    [Fact]
    public async Task EmptyPhrase_FindsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Fix login page"));
        TaskSearchProvider provider = new(harness.Store, harness.Options);

        SearchResult result = await provider.SearchAsync(Query(""), ct);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>With Tasks switched off in configuration, a matching Task is not searched at all (spec §8 intro).</summary>
    [Fact]
    public async Task TasksDisabled_ReturnsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.SeedOnDisk(MakeTask("PLAT-0001", "Fix login page"));
        IOptions<TeamOptions> disabled = Options.Create(new TeamOptions { Tasks = new TasksOptions { Enabled = false } });
        TaskSearchProvider provider = new(harness.Store, disabled);

        SearchResult result = await provider.SearchAsync(Query("login"), ct);

        Assert.Empty(result.Hits);
        Assert.False(result.Truncated);
    }

    /// <summary>Builds a Task on the Platform Team, active or Closed.</summary>
    /// <param name="id">The Task's id, such as "PLAT-0001".</param>
    /// <param name="title">The Task's title.</param>
    /// <param name="assignee">The assignee's Name, or null.</param>
    /// <param name="closed">Whether the Task is in the Closed folder.</param>
    /// <returns>The Task.</returns>
    private static TaskItem MakeTask(string id, string title, string? assignee = null, bool closed = false) =>
        TestTasks.Make(id: id, title: title, assignee: assignee, location: new TaskLocation("Platform", null, closed));

    /// <summary>Builds a query over the Tasks filter with no open Team.</summary>
    /// <param name="text">The phrase.</param>
    /// <param name="scope">The Active or Closed pass.</param>
    /// <returns>The query.</returns>
    private static SearchQuery Query(string text, SearchScope scope = SearchScope.Active) =>
        new(text, SearchFilter.Tasks, null, scope, 50);
}

using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

namespace Agency.Huddle.Tests.Tasks.Views;

/// <summary>Tests for <see cref="TaskQuery.Suggest"/>, the <c>#</c> Task picker's search (Spec §13.13.4).</summary>
public sealed class TaskQuerySuggestTests
{
    /// <summary>An empty query returns the most recently updated Active Tasks, most recent first.</summary>
    [Fact]
    public void Suggest_EmptyQuery_ReturnsMostRecentlyUpdatedActiveTasks()
    {
        List<TaskItem> tasks =
        [
            Updated("PLAT-0001", "Oldest", "2026-01-01T09:00:00Z"),
            Updated("PLAT-0002", "Middle", "2026-01-02T09:00:00Z"),
            Updated("PLAT-0003", "Newest", "2026-01-03T09:00:00Z"),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "", 8);

        Assert.Equal(["PLAT-0003", "PLAT-0002", "PLAT-0001"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>An empty query never returns a Closed Task, however recently updated.</summary>
    [Fact]
    public void Suggest_EmptyQuery_ExcludesClosedTasks()
    {
        List<TaskItem> tasks =
        [
            Updated("PLAT-0001", "Active one", "2026-01-01T09:00:00Z"),
            Updated("PLAT-0002", "Closed one", "2026-01-05T09:00:00Z", closed: true),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "", 8);

        TaskItem only = Assert.Single(result);
        Assert.Equal("PLAT-0001", only.Id.ToString());
    }

    /// <summary>A query matches ids that start with it, ignoring case.</summary>
    [Fact]
    public void Suggest_QueryMatchesIdPrefix_IgnoringCase()
    {
        List<TaskItem> tasks =
        [
            Updated("PLAT-0001", "Auth login bug", "2026-01-01T09:00:00Z"),
            Updated("GROW-0001", "Signup funnel", "2026-01-01T09:00:00Z"),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "PL", 8);

        TaskItem only = Assert.Single(result);
        Assert.Equal("PLAT-0001", only.Id.ToString());
    }

    /// <summary>A query matches a title containing it, ignoring case, when no id starts with it.</summary>
    [Fact]
    public void Suggest_QueryMatchesTitle_IgnoringCase()
    {
        List<TaskItem> tasks =
        [
            Updated("PLAT-0005", "Samlon setup", "2026-01-01T09:00:00Z"),
            Updated("PLAT-0006", "Unrelated", "2026-01-01T09:00:00Z"),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "SAML", 8);

        TaskItem only = Assert.Single(result);
        Assert.Equal("PLAT-0005", only.Id.ToString());
    }

    /// <summary>An id match ranks before a title match, even when the title match was updated more recently.</summary>
    [Fact]
    public void Suggest_IdMatch_RanksBeforeTitleMatch()
    {
        List<TaskItem> tasks =
        [
            Updated("GROW-0007", "Contains pl in the middle", "2026-01-05T09:00:00Z"),
            Updated("PLAT-0001", "Older id match", "2026-01-01T09:00:00Z"),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "pl", 8);

        Assert.Equal(["PLAT-0001", "GROW-0007"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>Active matches rank before Closed matches, regardless of match kind or recency.</summary>
    [Fact]
    public void Suggest_ActiveMatches_RankBeforeClosedMatches()
    {
        List<TaskItem> tasks =
        [
            Updated("PLAT-0001", "Closed pl match", "2026-01-05T09:00:00Z", closed: true),
            Updated("PLAT-0002", "Active pl match", "2026-01-01T09:00:00Z"),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "pl", 8);

        Assert.Equal(["PLAT-0002", "PLAT-0001"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>The limit caps the number of results returned.</summary>
    [Fact]
    public void Suggest_Limit_IsHonoured()
    {
        List<TaskItem> tasks =
        [
            Updated("PLAT-0001", "Alpha", "2026-01-01T09:00:00Z"),
            Updated("PLAT-0002", "Beta", "2026-01-02T09:00:00Z"),
            Updated("PLAT-0003", "Gamma", "2026-01-03T09:00:00Z"),
        ];

        IReadOnlyList<TaskItem> result = TaskQuery.Suggest(tasks, "", 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(["PLAT-0003", "PLAT-0002"], result.Select(t => t.Id.ToString()));
    }

    /// <summary>Builds a Task with a single ChangeLog entry, so <see cref="TaskItem.Updated"/> is set.</summary>
    private static TaskItem Updated(string id, string title, string updatedAt, bool closed = false) =>
        TestTasks.Make(
            id: id,
            title: title,
            changeLog: [TestTasks.Entry(updatedAt, "Human", "Created")],
            location: new("Platform", null, closed));
}

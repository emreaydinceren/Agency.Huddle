using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Tests for <see cref="NextTaskTool"/> under "one Task In Progress per Project" (TaskOrder spec §10.3, ADR-0103): with the rule on,
/// one Task per group is named Next and the rest wait for it; with it off the answer is today's.
/// </summary>
public sealed class NextTaskToolGroupRuleTests
{
    private static NextTaskTool CreateTool(TaskToolHarness harness) =>
        new(harness.Store, harness.Directory, new FakePromptSource(), harness.NovaIdOrThrow(), harness.GroupRule);

    private static TaskItem Make(string id, string title, string? project, int rank, TaskState status = TaskState.ToDo) =>
        TestTasks.Make(id: id, title: title, status: status, assignee: "Nova", location: new TaskLocation("Platform", project, false)) with { Rank = rank };

    private static void Seed(TaskToolHarness harness, params TaskItem[] tasks)
    {
        foreach (TaskItem task in tasks)
        {
            string fullPath = TaskLayout.PathFor(harness.TasksDirPath, task.Location, task.Id);
            TestTaskStore.WriteTask(harness.TasksDirPath, Path.GetRelativePath(harness.TasksDirPath, fullPath), task);
        }

        harness.Store.RebuildFromWatcher();
    }

    /// <summary>Switches the Team's rule on in the harness's hand-driven Team file.</summary>
    private static void TeamRuleOn(TaskToolHarness harness)
    {
        Assert.True(TeamFile.TryParse("---\nteamType: 'web@1.2.0'\noneInProgressPerProject: 'on'\n---\n", out TeamFile? file));
        harness.Folders.ReadResult = file;
    }

    /// <summary>With the rule on, the answer names one Task Next and says the other Task of that Project waits for it.</summary>
    [Fact]
    public async Task NextTask_RuleOn_NamesOneTaskPerProject_AndTheOtherWaits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TeamRuleOn(harness);
        Seed(harness, Make("PLAT-0001", "First", "API", 1), Make("PLAT-0002", "Second", "API", 2));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal(
            "Next: PLAT-0001 \"First\" (To Do, rank 1).\nWaiting: PLAT-0002 \"Second\" waits for PLAT-0001 in API; only one Task there can be In Progress at a time.",
            result);
    }

    /// <summary>With the rule off, the same Tasks give today's answer: the second is listed after the first.</summary>
    [Fact]
    public async Task NextTask_RuleOff_IsTodaysAnswer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Seed(harness, Make("PLAT-0001", "First", "API", 1), Make("PLAT-0002", "Second", "API", 2));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal("Next: PLAT-0001 \"First\" (To Do, rank 1).\nAfter it: PLAT-0002 \"Second\" (To Do, rank 2).", result);
    }

    /// <summary>A Project's Task already In Progress, held by someone else, leaves the caller's Task in that Project waiting; another Project still has a Next.</summary>
    [Fact]
    public async Task NextTask_RuleOn_AnotherAgentsTaskInProgress_BlocksOnlyThatProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TeamRuleOn(harness);
        Seed(
            harness,
            TestTasks.Make(id: "PLAT-0009", title: "Kai's", status: TaskState.InProgress, assignee: "Kai", location: new TaskLocation("Platform", "API", false)),
            Make("PLAT-0001", "Mine in API", "API", 1),
            Make("PLAT-0002", "Mine in Web", "Web", 2));

        string result = await CreateTool(harness).InvokeAsync(new JsonObject(), ct);

        Assert.Equal(
            "Next: PLAT-0002 \"Mine in Web\" (To Do, rank 2).\nWaiting: PLAT-0001 \"Mine in API\" waits for PLAT-0009 in API; only one Task there can be In Progress at a time.",
            result);
    }
}

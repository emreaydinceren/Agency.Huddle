using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// <c>create_task</c> and <c>update_task</c> tell an Agent, after the fact, that an Action in a scope that runs inside the Team sends the Task to
/// the Human (ADR-0087). The note appears only when the Task has an Action, its level is Run inside the Team and its Team has
/// <c>accept-before-merge</c>; every other result is unchanged.
/// </summary>
public sealed class TaskActionNoteTests
{
    /// <summary>The whole note for the first Task of Team Platform, the same for both tools; it ends the tool's result.</summary>
    private const string Note = "Note: PLAT-0001's Team Platform runs inside the Team, so a local merge into main needs no Action; this Action sends the Task to the Human. Use an Action only for a step that leaves the Team, such as a push.";

    /// <summary>Creating a Task with an Action under Run inside the Team, with the gate on, appends the note.</summary>
    [Fact]
    public async Task CreateTask_ActionUnderRunInside_AppendsNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        CreateTaskTool tool = CreateTool(harness, rig);

        string result = await tool.InvokeAsync(CreateArguments(action: "Merge feat/greeting into main"), ct);

        Assert.Equal("Created PLAT-0001 \"Ship it\" in Platform, unassigned. No one is notified. " + Note, result);
    }

    /// <summary>Creating a Task with an Action under Ask me adds nothing: the result is the one a tool without the note gives.</summary>
    [Fact]
    public async Task CreateTask_ActionUnderAskMe_ResultUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouLevelRig rig = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await CreateTool(harness, rig).InvokeAsync(CreateArguments(action: "Merge feat/greeting into main"), ct);

        Assert.Equal("Created PLAT-0001 \"Ship it\" in Platform, unassigned. No one is notified.", result);
    }

    /// <summary>Under Run inside the Team a Task with no Action gets no note.</summary>
    [Fact]
    public async Task CreateTask_NoActionUnderRunInside_ResultUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await CreateTool(harness, rig).InvokeAsync(CreateArguments(action: null), ct);

        Assert.DoesNotContain("Note:", result, StringComparison.Ordinal);
    }

    /// <summary>A Team without <c>accept-before-merge</c> has no merge to release, so an Action under Run inside the Team gets no note.</summary>
    [Fact]
    public async Task CreateTask_RunInsideButNoAcceptGate_ResultUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam, gates: "'checks-before-merge'");
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string result = await CreateTool(harness, rig).InvokeAsync(CreateArguments(action: "Push main to origin"), ct);

        Assert.DoesNotContain("Note:", result, StringComparison.Ordinal);
    }

    /// <summary>Setting an Action by <c>update_task</c> under Run inside the Team appends the note after the change summary.</summary>
    [Fact]
    public async Task UpdateTask_ActionUnderRunInside_AppendsNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouLevelRig rig = new(teamLevel: NeedsYouLevel.RunInsideTheTeam);
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = harness.CreateTask("Ship it");
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow(), rig.Levels, rig.Gates);

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["action"] = "Merge feat/greeting into main" }, ct);

        Assert.Equal(Note, result[^Note.Length..]);
    }

    /// <summary>Setting an Action by <c>update_task</c> under Ask me leaves the result without a note.</summary>
    [Fact]
    public async Task UpdateTask_ActionUnderAskMe_NoNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using NeedsYouLevelRig rig = new();
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        TaskItem task = harness.CreateTask("Ship it");
        UpdateTaskTool tool = new(harness.Service, harness.Store, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow(), rig.Levels, rig.Gates);

        string result = await tool.InvokeAsync(new JsonObject { ["taskId"] = task.Id.ToString(), ["action"] = "Merge feat/greeting into main" }, ct);

        Assert.DoesNotContain("Note:", result, StringComparison.Ordinal);
    }

    /// <summary>Builds the tool over the harness's collaborators and the rig's levels and gates.</summary>
    private static CreateTaskTool CreateTool(TaskToolHarness harness, NeedsYouLevelRig rig) =>
        new(harness.Service, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaIdOrThrow(), levels: rig.Levels, gates: rig.Gates);

    /// <summary>The arguments of a create_task call in Platform with the given Action.</summary>
    private static JsonObject CreateArguments(string? action)
    {
        JsonObject arguments = new() { ["title"] = "Ship it", ["team"] = "Platform" };
        if (action is not null)
        {
            arguments["action"] = action;
        }

        return arguments;
    }
}

using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Review fix 2: <see cref="TaskService"/> keeps a Request Task inside its lifecycle. An agent cannot move a Request out of Backlog
/// without a triage accept or decline, nor close it without the requesting Team's QA confirmation; the Human override still works.
/// </summary>
public sealed class RequestLifecycleEnforcementTests
{
    private const string LeaveBacklogRefusal = "A Request leaves Backlog only when its receiving manager accepts or declines it with triage_request.";
    private const string CloseRefusal = "A Request closes only after the requesting Team's verifier confirms it with confirm_request, and that needs it in Done. Done is not closed: if it isn't Done yet, move it to Done with update_task, then ask them to confirm.";

    /// <summary>Request frontmatter filed by Design's Dana.</summary>
    private static List<KeyValuePair<string, string>> Pairs(TriageState triage = TriageState.None, string confirmedBy = "") =>
        [.. new RequestFields(RequestKind.Bug, "Design", "Dana", triage, string.Empty, confirmedBy).ToFrontmatter()];

    /// <summary>Writes a Platform Request into the store.</summary>
    private static TaskItem Seed(TaskToolHarness harness, TaskState status, List<KeyValuePair<string, string>> pairs)
    {
        TaskItem task = TestTasks.Make(id: "PLAT-0001", status: status, description: "Body", location: new TaskLocation("Platform", null, false), unknownFields: pairs);
        TaskItem toWrite = task with { Path = TaskLayout.PathFor(harness.Store.RootDirectory, task.Location, task.Id) };
        return harness.Store.Create(toWrite, TaskFileFormat.Compose(toWrite)) ?? throw new InvalidOperationException("Fixture collided with an existing file.");
    }

    private static TaskActor Agent(TaskToolHarness harness) => new(TaskActorKind.Agent, "Kai", harness.KaiId);

    private static TaskActor Human(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    /// <summary>An agent moving an untriaged Backlog Request to To Do is refused.</summary>
    [Fact]
    public async Task Update_AgentLeavesBacklogWithoutTriage_IsRefused()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Backlog, Pairs());

        TaskResult result = harness.Service.Update(request.Id, new TaskPatch { Status = TaskState.ToDo }, request.Version, Agent(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([LeaveBacklogRefusal], refused.Problems);
        Assert.Equal(TaskState.Backlog, harness.Store.Get(request.Id)?.Status);
    }

    /// <summary>The Human keeps the override when the caller asks for it.</summary>
    [Fact]
    public async Task Update_HumanOverride_MayLeaveBacklog()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Backlog, Pairs());

        TaskResult result = harness.Service.Update(request.Id, new TaskPatch { Status = TaskState.ToDo }, request.Version, Human(harness), humanOverridesGates: true);

        Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(request.Id)?.Status);
    }

    /// <summary>A triage accept patch, which writes the accepted triage together with the status, is the sanctioned way out of Backlog.</summary>
    [Fact]
    public async Task Update_AcceptedTriagePatch_LeavesBacklog()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Backlog, Pairs());
        TaskPatch patch = new() { Status = TaskState.ToDo, ExtraFields = [.. new RequestFields(RequestKind.Bug, "Design", "Dana", TriageState.Accepted, string.Empty, string.Empty).ToFrontmatter()] };

        TaskResult result = harness.Service.Update(request.Id, patch, request.Version, Agent(harness));

        Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal(TaskState.ToDo, harness.Store.Get(request.Id)?.Status);
    }

    /// <summary>An ordinary Task still leaves Backlog freely.</summary>
    [Fact]
    public async Task Update_OrdinaryTask_LeavesBacklog()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Backlog, []);

        TaskResult result = harness.Service.Update(request.Id, new TaskPatch { Status = TaskState.ToDo }, request.Version, Agent(harness));

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>An agent closing a Done Request that nobody confirmed is refused.</summary>
    [Fact]
    public async Task Close_AgentWithoutConfirmation_IsRefused()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Done, Pairs(TriageState.Accepted));

        TaskResult result = harness.Service.Close(request.Id, Agent(harness));

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([CloseRefusal], refused.Problems);
        Assert.False(harness.Store.Get(request.Id)?.Location.Closed);
    }

    /// <summary>A confirmed Request closes.</summary>
    [Fact]
    public async Task Close_ConfirmedRequest_Closes()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Done, Pairs(TriageState.Accepted, "Quinn"));

        TaskResult result = harness.Service.Close(request.Id, Agent(harness));

        Assert.IsType<TaskResult.Saved>(result);
        Assert.True(harness.Store.Get(request.Id)?.Location.Closed);
    }

    /// <summary>The Human may close an unconfirmed Request.</summary>
    [Fact]
    public async Task Close_HumanWithoutConfirmation_Closes()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Done, Pairs(TriageState.Accepted));

        TaskResult result = harness.Service.Close(request.Id, Human(harness));

        Assert.IsType<TaskResult.Saved>(result);
    }

    /// <summary>A declined Request has nothing to confirm and may be closed.</summary>
    [Fact]
    public async Task Close_DeclinedRequest_Closes()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(TestContext.Current.CancellationToken);
        TaskItem request = Seed(harness, TaskState.Rejected, Pairs(TriageState.Declined));

        TaskResult result = harness.Service.Close(request.Id, Agent(harness));

        Assert.IsType<TaskResult.Saved>(result);
    }
}

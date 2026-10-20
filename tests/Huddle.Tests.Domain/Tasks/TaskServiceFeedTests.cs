using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Feed;
using static Agency.Huddle.Tests.Tasks.TaskGateText;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>What <see cref="TaskService"/> tells the Feed after a write (Feed spec §6.5.1), and that the Feed can never change the answer.</summary>
public sealed class TaskServiceFeedTests
{
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    private static TaskActor HumanOf(TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);

    private static TaskItem CreateBacklogTask(TaskToolHarness harness) =>
        Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Fix login", "Platform", null), HumanOf(harness))).Task;

    /// <summary>A Create posts a task-created item through the injected publisher, naming the actor.</summary>
    [Fact]
    public void Create_PostsTaskCreatedWithTheActor()
    {
        RecordingFeedPublisher feed = new();
        using TaskToolHarness harness = new(feed: feed);

        TaskResult result = harness.Service.Create(new TaskDraft("Fix login", "Platform", null), Nova);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.TaskCreated, post.SourceType);
        Assert.Equal(saved.Task.Id.ToString(), post.TargetKey);
        Assert.Equal("Nova", post.Actor.Name);
    }

    /// <summary>A publisher that throws on every call changes neither the result nor the written Task.</summary>
    [Fact]
    public void Create_ThrowingPublisher_StillSaves()
    {
        RecordingFeedPublisher feed = new() { Throws = true };
        using TaskToolHarness harness = new(feed: feed);

        TaskResult result = harness.Service.Create(new TaskDraft("Fix login", "Platform", null), Nova);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.NotNull(harness.Store.Get(saved.Task.Id));
    }

    /// <summary>An Agent's move that a process gate refuses posts a gate-decision Notification carrying the gate and the refusal.</summary>
    [Fact]
    public void Update_AgentRefusedByProcessGate_PostsGateDecisionDue()
    {
        using TempDataDir gateDir = new();
        RecordingFeedPublisher feed = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)), feed: feed);
        TaskItem task = CreateBacklogTask(harness);
        int before = feed.Posts.Count;

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        Assert.IsType<TaskResult.GateRefused>(result);
        FeedMessage post = Assert.Single(feed.Posts.Skip(before));
        Assert.Equal(FeedSourceType.GateDecisionDue, post.SourceType);
        Assert.True(post.Notify);
        GateDecisionPayload payload = Assert.IsType<GateDecisionPayload>(post.Payload);
        Assert.Equal("qa-evidence-before-done", payload.GateId);
        Assert.Equal(QaDone, payload.Message);
        Assert.Equal((TaskState.Backlog.ToWire(), TaskState.Done.ToWire()),(payload.FromStatus, payload.ToStatus));
    }

    /// <summary>The Human's own refused move posts nothing: only an Agent waits on the Human's override.</summary>
    [Fact]
    public void Update_HumanRefusedByProcessGate_PostsNothing()
    {
        using TempDataDir gateDir = new();
        RecordingFeedPublisher feed = new();
        using TaskToolHarness harness = new(gates: BuildGates(gateDir, Process(QaGate)), feed: feed);
        TaskItem task = CreateBacklogTask(harness);
        int before = feed.Posts.Count;

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, HumanOf(harness));

        Assert.IsType<TaskResult.GateRefused>(result);
        Assert.Equal(before, feed.Posts.Count);
    }

    /// <summary>A refusal by the Human gate cannot be overridden, so it posts nothing.</summary>
    [Fact]
    public void Update_AgentRefusedByHumanGate_PostsNothing()
    {
        RecordingFeedPublisher feed = new();
        using TaskToolHarness harness = new(feed: feed);
        TaskItem task = Assert.IsType<TaskResult.Saved>(harness.Service.Create(new TaskDraft("Publish", "Platform", null) { HumanGate = true }, HumanOf(harness))).Task;
        int before = feed.Posts.Count;

        TaskResult result = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.Done }, null, Nova);

        TaskResult.GateRefused refused = Assert.IsType<TaskResult.GateRefused>(result);
        Assert.False(refused.CanOverride);
        Assert.Equal(before, feed.Posts.Count);
    }

    /// <summary>A status change asks the Feed to resolve any gate-decision Notification about the Task.</summary>
    [Fact]
    public void Update_StatusChange_ResolvesGateDecisionDue()
    {
        RecordingFeedPublisher feed = new();
        using TaskToolHarness harness = new(feed: feed);
        TaskItem task = CreateBacklogTask(harness);

        _ = harness.Service.Update(task.Id, new TaskPatch { Status = TaskState.ToDo }, null, HumanOf(harness), humanOverridesGates: true);

        Assert.Contains(new FeedResolution(task.Id.ToString(), FeedSourceType.GateDecisionDue), feed.Resolutions);
    }
}

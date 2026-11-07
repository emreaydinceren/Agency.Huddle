using Microsoft.AspNetCore.TestHost;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Elicitation;
using Agency.Huddle.Tests.Feed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// What <see cref="TaskFeedPublisher"/> does when the Task hub raises an event (Feed spec §6.5.1): it records the change in the Feed,
/// and a failing Feed never reaches the raiser (policy 4: the Feed records and never decides).
/// </summary>
public sealed class TaskFeedPublisherTests
{
    private static readonly TaskActor Nova = new(TaskActorKind.Agent, "Nova", "nova-id");

    private static TaskChange CreatedBy(TaskActor actor)
    {
        TaskItem created = TestTasks.Make(id: "PLAT-0001", title: "Fix login", status: TaskState.Backlog);
        return new TaskChange(null, created, [], actor, new ChangeLogEntry(DateTimeOffset.UtcNow, actor.Name, "created"));
    }

    private static TaskFeedPublisher Publisher(TaskEvents events, IFeedPublisher feed, ILogger<TaskFeedPublisher>? logger = null) =>
        new(events, feed, new ConfiguredHumanIdentity("You"), logger ?? new RecordingLogger<TaskFeedPublisher>());

    /// <summary>A change raised on the hub posts exactly the items <see cref="TaskFeedRules"/> gives for it.</summary>
    [Fact]
    public void TaskChanged_PostsWhatTheRulesGive()
    {
        TaskEvents events = new();
        RecordingFeedPublisher feed = new();
        using TaskFeedPublisher publisher = Publisher(events, feed);
        TaskChange change = CreatedBy(Nova);

        events.RaiseTaskChanged(change);

        FeedEffects expected = TaskFeedRules.For(change, "You", static _ => false, static _ => Agency.Huddle.App.Tasks.HumanGate.Proof.Empty);
        Assert.NotEmpty(expected.Posts);
        Assert.Equal(expected.Posts.Select(static post => (post.SourceType, post.TargetKey)), feed.Posts.Select(static post => (post.SourceType, post.TargetKey)));
    }

    /// <summary>A Feed that throws is logged as a warning and the raiser of the change never sees it.</summary>
    [Fact]
    public void TaskChanged_ThrowingFeed_IsLoggedAndDoesNotReachTheRaiser()
    {
        TaskEvents events = new();
        RecordingFeedPublisher feed = new() { Throws = true };
        RecordingLogger<TaskFeedPublisher> logger = new();
        using TaskFeedPublisher publisher = Publisher(events, feed, logger);

        events.RaiseTaskChanged(CreatedBy(Nova));

        LogEntry entry = Assert.Single(logger.AtExactly(LogLevel.Warning));
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    /// <summary>A move a gate refused posts the gate-decision Notification, naming the gate, the message and the Agent that asked.</summary>
    [Fact]
    public void TaskMoveRefused_PostsGateDecisionDue()
    {
        TaskEvents events = new();
        RecordingFeedPublisher feed = new();
        using TaskFeedPublisher publisher = Publisher(events, feed);
        TaskItem current = TestTasks.Make(id: "PLAT-0001", title: "Fix login", status: TaskState.Backlog);

        events.RaiseTaskMoveRefused(new TaskMoveRefusal(current, TaskState.Done, Nova, GateId.QaEvidenceBeforeDone, "Add Evidence first."));

        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.GateDecisionDue, post.SourceType);
        Assert.True(post.Notify);
        Assert.Equal("Nova", post.Actor.Name);
        GateDecisionPayload payload = Assert.IsType<GateDecisionPayload>(post.Payload);
        Assert.Equal("Add Evidence first.", payload.Message);
        Assert.Equal((TaskState.Backlog.ToWire(), TaskState.Done.ToWire()), (payload.FromStatus, payload.ToStatus));
    }

    /// <summary>A Feed that throws while recording a refused move is logged and does not reach the raiser.</summary>
    [Fact]
    public void TaskMoveRefused_ThrowingFeed_IsLoggedAndDoesNotReachTheRaiser()
    {
        TaskEvents events = new();
        RecordingFeedPublisher feed = new() { Throws = true };
        RecordingLogger<TaskFeedPublisher> logger = new();
        using TaskFeedPublisher publisher = Publisher(events, feed, logger);
        TaskItem current = TestTasks.Make(id: "PLAT-0001", status: TaskState.Backlog);

        events.RaiseTaskMoveRefused(new TaskMoveRefusal(current, TaskState.Done, Nova, GateId.QaEvidenceBeforeDone, "Add Evidence first."));

        _ = Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>After <c>Dispose</c> the publisher no longer listens, for a change or a refused move.</summary>
    [Fact]
    public void Dispose_StopsPosting()
    {
        TaskEvents events = new();
        RecordingFeedPublisher feed = new();
        TaskFeedPublisher publisher = Publisher(events, feed);
        publisher.Dispose();
        TaskItem current = TestTasks.Make(id: "PLAT-0001", status: TaskState.Backlog);

        events.RaiseTaskChanged(CreatedBy(Nova));
        events.RaiseTaskMoveRefused(new TaskMoveRefusal(current, TaskState.Done, Nova, GateId.QaEvidenceBeforeDone, "Add Evidence first."));

        Assert.Empty(feed.Posts);
        Assert.Empty(feed.Resolutions);
    }

    /// <summary>In the real container nobody resolves the publisher by hand, yet a Task written through <see cref="TaskService"/> reaches the Feed.</summary>
    [Fact]
    public async Task RealContainer_TaskWrite_PostsAFeedItem()
    {
        RecordingFeedPublisher feed = new();
        await using TeamWebApplicationFactory root = new();
        await using WebApplicationFactory<Program> host = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFeedPublisher>();
            services.AddSingleton<IFeedPublisher>(feed);
        }));
        TaskService service = host.Services.GetRequiredService<TaskService>();
        TaskActor human = TaskActors.Human(host.Services.GetRequiredService<IOptions<TeamOptions>>().Value);
        _ = host.Services.GetRequiredService<PersonaStore>().Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");

        TaskResult result = service.Create(new TaskDraft("Fix login", "Platform", null), human);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Contains(feed.Posts, post => post.TargetKey == saved.Task.Id.ToString());
    }
}

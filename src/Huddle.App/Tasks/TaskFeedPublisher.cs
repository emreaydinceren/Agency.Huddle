using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Tells the Feed what a Task event means (Feed spec §6.5.1). It subscribes to <see cref="TaskEvents"/> in its constructor, so the host
/// builds it at start-up (it is a hosted service only so that it is built before the first Task write); <see cref="TaskService"/> knows nothing of the Feed.
/// The Feed records and never decides (policy 4): a failing rule or publisher is logged and dropped, and never reaches the raiser.
/// </summary>
internal sealed class TaskFeedPublisher : IDisposable, IHostedService
{
    private readonly TaskEvents events;
    private readonly IFeedPublisher feed;
    private readonly IHumanIdentity human;
    private readonly ILogger<TaskFeedPublisher> logger;
    private readonly IRequestConfirmers? confirmers;
    private readonly IProofSource? proofs;

    /// <summary>Creates the publisher and subscribes it to the Task events.</summary>
    /// <param name="events">The hub whose Task events it records.</param>
    /// <param name="feed">Receives the items each event produces.</param>
    /// <param name="human">The Human's live identity, whose Name the rules compare against.</param>
    /// <param name="logger">Reports a Feed failure.</param>
    /// <param name="confirmers">Tells whether a Team has a member who can confirm a Request; <see langword="null"/> means none can.</param>
    /// <param name="proofs">Evaluates a Task's Proof for the count on a Needs You item; <see langword="null"/> means an empty, complete Proof.</param>
    public TaskFeedPublisher(
        TaskEvents events,
        IFeedPublisher feed,
        IHumanIdentity human,
        ILogger<TaskFeedPublisher> logger,
        IRequestConfirmers? confirmers = null,
        IProofSource? proofs = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(human);
        ArgumentNullException.ThrowIfNull(logger);

        this.events = events;
        this.feed = feed;
        this.human = human;
        this.logger = logger;
        this.confirmers = confirmers;
        this.proofs = proofs;
        events.TaskChanged += this.OnTaskChanged;
        events.TaskMoveRefused += this.OnTaskMoveRefused;
    }

    /// <summary>Does nothing: the work was done when the host built this service.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Does nothing; <see cref="Dispose"/> unsubscribes.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Unsubscribes from the Task events.</summary>
    public void Dispose()
    {
        this.events.TaskChanged -= this.OnTaskChanged;
        this.events.TaskMoveRefused -= this.OnTaskMoveRefused;
    }

    /// <summary>Posts and resolves the Feed items for a change. The Feed can never hurt the work, so a failure is logged and dropped.</summary>
    /// <param name="change">The change that was just written.</param>
    private void OnTaskChanged(TaskChange change)
    {
        try
        {
            FeedEffects effects = TaskFeedRules.For(change, this.human.Name, team => this.confirmers?.TeamHasConfirmer(team) ?? false, this.ProofOf);
            foreach (FeedMessage message in effects.Posts)
            {
                this.feed.Post(message);
            }

            foreach (FeedResolution resolution in effects.Resolutions)
            {
                this.feed.Resolve(resolution.TargetKey, resolution.Type);
            }
        }
        catch (Exception ex)
        {
            // The Feed records and never decides (Feed spec policy 4): a failing rule or publisher must not fail the Task write that caused it.
            this.logger.LogWarning(ex, "The Feed could not record the change to Task {TaskId}.", change.After.Id.ToString());
        }
    }

    /// <summary>Posts the Notification for a refused move that only the Human can override (Feed spec §6.5.1).</summary>
    /// <param name="move">The refused move.</param>
    private void OnTaskMoveRefused(TaskMoveRefusal move)
    {
        try
        {
            string id = move.Current.Id.ToString();
            GateDecisionPayload payload = new(id, move.Current.Title, move.Current.Location.Team, ProcessDocument.ToId(move.Gate), move.Message, move.Current.Status.ToWire(), move.Requested.ToWire());
            this.feed.Post(new FeedMessage(FeedSourceType.GateDecisionDue, id, new FeedActor(FeedActorKind.Agent, move.Actor.Name), true, payload));
        }
        catch (Exception ex)
        {
            // Same rule as OnTaskChanged: the refusal the Agent sees must not change because the Feed failed.
            this.logger.LogWarning(ex, "The Feed could not record the refused move of Task {TaskId}.", move.Current.Id.ToString());
        }
    }

    /// <summary>Evaluates a Task's Proof, or returns the empty (complete) Proof when no source is wired.</summary>
    /// <param name="task">The Task.</param>
    private Proof ProofOf(TaskItem task) => this.proofs?.For(task) ?? Proof.Empty;
}

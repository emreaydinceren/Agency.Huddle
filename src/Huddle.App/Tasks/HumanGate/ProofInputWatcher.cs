using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>
/// Closes #593: a Proof input that lives outside the Task file (an attached document's REVIEWED history entry, a tagged child
/// reaching Done) re-evaluates the gated Task and moves it from In Progress to Needs You the same way a Task write does. It reuses
/// <see cref="DocHistoryStore.Changed"/> rather than <see cref="LibraryEvents"/>, because <see cref="LibraryEvents"/> only carries
/// the Human's Library saves, while a REVIEWED entry comes from an Agent's <c>log_change</c>, which only <see cref="DocHistoryStore"/>
/// sees. There is no polling. It subscribes in its constructor, so the host builds it at start-up (it is a hosted service only for that).
/// </summary>
internal sealed class ProofInputWatcher : IHostedService, IDisposable
{
    private readonly DocHistoryStore history;
    private readonly TaskEvents events;
    private readonly TaskStore tasks;
    private readonly TaskService service;
    private readonly LibraryPathResolver resolver;

    /// <summary>Creates the watcher and subscribes it to the document History and the Task events.</summary>
    /// <param name="history">The document History, whose appends it watches.</param>
    /// <param name="events">The Task events, whose changes it watches.</param>
    /// <param name="tasks">The Task store, to find the Tasks a change concerns.</param>
    /// <param name="service">The Task service that re-evaluates and moves a Task.</param>
    /// <param name="resolver">Resolves an attached path line to a Library path.</param>
    public ProofInputWatcher(DocHistoryStore history, TaskEvents events, TaskStore tasks, TaskService service, LibraryPathResolver resolver)
    {
        this.history = history;
        this.events = events;
        this.tasks = tasks;
        this.service = service;
        this.resolver = resolver;
        history.Changed += this.OnHistoryChanged;
        events.TaskChanged += this.OnTaskChanged;
    }

    /// <summary>Does nothing: the work was done when the host built this service.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Does nothing; <see cref="Dispose"/> unsubscribes.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Unsubscribes from the History and the Task events.</summary>
    public void Dispose()
    {
        this.history.Changed -= this.OnHistoryChanged;
        this.events.TaskChanged -= this.OnTaskChanged;
    }

    /// <summary>Re-evaluates the Tasks In Progress that attach <paramref name="file"/> when its newest entry is a review.</summary>
    /// <param name="file">The document whose History changed.</param>
    private void OnHistoryChanged(LibraryPath file)
    {
        // Only a review can complete Proof; a Created, Edited or Objected entry can only make it less complete.
        IReadOnlyList<DocHistoryEntry> entries = this.history.Read(file);
        if (entries.Count == 0 || entries[^1].Kind != DocChangeKind.Reviewed)
        {
            return;
        }

        List<TaskItem> waiting = [.. this.tasks.All.Where(static task => task.Status == TaskState.InProgress && !task.Location.Closed)];
        foreach (TaskItem task in waiting.Where(task => this.Attaches(task, file)))
        {
            this.service.MoveIfReady(task.Id);
        }
    }

    /// <summary>Re-evaluates the parent of a child that has just reached Done or been tagged while Done.</summary>
    /// <param name="change">The Task change.</param>
    private void OnTaskChanged(TaskChange change)
    {
        if (change.After.Parent is { } parent
            && change.After.Status == TaskState.Done
            && change.Changes.Any(static field => field.Field is TaskField.Status or TaskField.Tags))
        {
            this.service.MoveIfReady(parent);
        }
    }

    /// <summary>Whether the description of <paramref name="task"/> attaches <paramref name="file"/>, as <see cref="ProofContextBuilder"/> reads it.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="file">The document.</param>
    private bool Attaches(TaskItem task, LibraryPath file)
    {
        // The relative path compares ignoring case on every OS: a false match only costs a re-evaluation, which moves nothing unless Proof is complete.
        return TaskAttachmentText.PathLines(task.Description).Any(line =>
            this.resolver.TryResolveAbsolute(line, out LibraryPath? attached)
            && string.Equals(attached.Root.Id, file.Root.Id, StringComparison.Ordinal)
            && string.Equals(attached.RelativePath, file.RelativePath, StringComparison.OrdinalIgnoreCase));
    }
}

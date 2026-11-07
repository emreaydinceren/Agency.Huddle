using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.DocHistory;

/// <summary>
/// Records the Human's Library changes in the History (DocHistory spec §6.4, §6.5). It subscribes to <see cref="LibraryEvents"/> in its
/// constructor, so the host builds it at start-up (it is a hosted service only so that it is built before the first Library write);
/// <see cref="LibraryFileService"/> knows nothing of the History. The recorder swallows and logs its own IO failures.
/// </summary>
internal sealed class DocHistoryLibraryListener : IDisposable, IHostedService
{
    private readonly LibraryEvents events;
    private readonly DocHistoryRecorder recorder;

    /// <summary>Creates the listener and subscribes it to the Library events.</summary>
    /// <param name="events">The hub whose events it records.</param>
    /// <param name="recorder">The one writer of History entries.</param>
    public DocHistoryLibraryListener(LibraryEvents events, DocHistoryRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(recorder);

        this.events = events;
        this.recorder = recorder;
        events.HumanSaved += this.recorder.HumanSaved;
        events.HumanCreated += this.recorder.HumanCreated;
        events.UploadDiscarded += this.recorder.Discarded;
        events.Moved += this.OnMoved;
    }

    /// <summary>Does nothing: the work was done when the host built this service.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Does nothing; <see cref="Dispose"/> unsubscribes.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Unsubscribes from the Library events.</summary>
    public void Dispose()
    {
        this.events.HumanSaved -= this.recorder.HumanSaved;
        this.events.HumanCreated -= this.recorder.HumanCreated;
        this.events.UploadDiscarded -= this.recorder.Discarded;
        this.events.Moved -= this.OnMoved;
    }

    /// <summary>Carries the History of the moved item to its new place.</summary>
    /// <param name="moved">The move.</param>
    private void OnMoved(LibraryItemMoved moved) => this.recorder.Moved(moved.From, moved.To);
}

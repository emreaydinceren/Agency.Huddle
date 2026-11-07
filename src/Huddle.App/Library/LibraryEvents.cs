using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Library;

/// <summary>
/// A plain hub for what the Human does to Library files: <see cref="LibraryFileService"/> raises each event
/// synchronously, where the change succeeded, and knows nothing of who listens. A handler that throws is
/// logged and skipped, so it neither stops the handlers after it nor fails the save, create or move.
/// </summary>
/// <param name="logger">Reports a subscriber that threw; defaults to a logger that discards.</param>
internal sealed class LibraryEvents(ILogger<LibraryEvents>? logger = null)
{
    private readonly ILogger<LibraryEvents> logger = logger ?? NullLogger<LibraryEvents>.Instance;

    /// <summary>Raised after the Human saved a Markdown document.</summary>
    public event Action<LibraryPath>? HumanSaved;

    /// <summary>Raised after the Human created a document.</summary>
    public event Action<LibraryPath>? HumanCreated;

    /// <summary>Raised after a file or folder was renamed or moved.</summary>
    public event Action<LibraryItemMoved>? Moved;

    /// <summary>Invokes every <see cref="HumanSaved"/> subscriber in turn; one that throws is logged and skipped.</summary>
    /// <param name="file">The saved document.</param>
    internal void RaiseHumanSaved(LibraryPath file)
    {
        ArgumentNullException.ThrowIfNull(file);
        EventRaise.Isolated(this.HumanSaved, file, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.HumanSaved)));
    }

    /// <summary>Invokes every <see cref="HumanCreated"/> subscriber in turn; one that throws is logged and skipped.</summary>
    /// <param name="file">The created document.</param>
    internal void RaiseHumanCreated(LibraryPath file)
    {
        ArgumentNullException.ThrowIfNull(file);
        EventRaise.Isolated(this.HumanCreated, file, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.HumanCreated)));
    }

    /// <summary>Invokes every <see cref="Moved"/> subscriber in turn; one that throws is logged and skipped.</summary>
    /// <param name="moved">The move.</param>
    internal void RaiseMoved(LibraryItemMoved moved)
    {
        ArgumentNullException.ThrowIfNull(moved);
        EventRaise.Isolated(this.Moved, moved, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Moved)));
    }
}

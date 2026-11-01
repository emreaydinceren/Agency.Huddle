using System.Diagnostics.CodeAnalysis;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Finds the open Tasks that link to a Library document. <see cref="WikiLinkIndex"/> cannot: it skips the
/// underscore folders of the Teams root, which is where <c>_tasks/</c> lives. A Task links to a document by an
/// attachment line (the file's absolute path alone in a code span, <see cref="TaskAttachmentText"/>) or by a
/// <c>[[wikilink]]</c> in its description. A closed Task, one under <c>_closed/</c>, does not count.
/// </summary>
internal sealed class TaskBacklinkSource : ITaskBacklinkSource, IDisposable
{
    private const string TeamsRootId = "teams";

    private readonly TaskStore store;
    private readonly LibraryPathResolver resolver;
    private readonly WikiLinkIndex wikiLinks;
    private readonly ILogger<TaskBacklinkSource> logger;
    private bool disposed;

    /// <summary>Creates the source over <paramref name="store"/>, forwarding its <see cref="TaskStore.IndexChanged"/>.</summary>
    /// <param name="store">Every Task.</param>
    /// <param name="resolver">Maps an attached absolute path, and a Task's own file, into the Library.</param>
    /// <param name="wikiLinks">Resolves a wikilink written in a description.</param>
    /// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
    public TaskBacklinkSource(TaskStore store, LibraryPathResolver resolver, WikiLinkIndex wikiLinks, ILogger<TaskBacklinkSource>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(wikiLinks);

        this.store = store;
        this.resolver = resolver;
        this.wikiLinks = wikiLinks;
        this.logger = logger ?? NullLogger<TaskBacklinkSource>.Instance;
        this.store.IndexChanged += this.OnIndexChanged;
    }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<TaskBacklink> For(LibraryPath document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(this.disposed, this);

        return TaskBacklinkSource.Scan(this.store.All, document, this.resolver, this.wikiLinks);
    }

    /// <summary>Unsubscribes from <see cref="TaskStore.IndexChanged"/>.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.store.IndexChanged -= this.OnIndexChanged;
        this.disposed = true;
    }

    /// <summary>The Tasks in <paramref name="tasks"/> that link to <paramref name="document"/>; the work of <see cref="For"/>, apart from the store.</summary>
    /// <param name="tasks">The Tasks to scan.</param>
    /// <param name="document">The open document.</param>
    /// <param name="resolver">Maps a path into the Library.</param>
    /// <param name="wikiLinks">Resolves a wikilink.</param>
    internal static List<TaskBacklink> Scan(IReadOnlyList<TaskItem> tasks, LibraryPath document, LibraryPathResolver resolver, WikiLinkIndex wikiLinks)
    {
        List<TaskBacklink> results = [];
        foreach (TaskItem task in tasks)
        {
            if (task.Location.Closed)
            {
                continue;
            }

            string? line = TaskBacklinkSource.FirstLinkingLine(task, document, resolver, wikiLinks);
            if (line is not null)
            {
                results.Add(new TaskBacklink(task.Id, task.Title, task.Status, line));
            }
        }

        return results;
    }

    private void OnIndexChanged() =>
        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));

    /// <summary>The first description line of <paramref name="task"/> that links to <paramref name="document"/>, or <see langword="null"/>.</summary>
    private static string? FirstLinkingLine(TaskItem task, LibraryPath document, LibraryPathResolver resolver, WikiLinkIndex wikiLinks)
    {
        HashSet<int> wikilinkLines = TaskBacklinkSource.WikilinkLines(task, document, resolver, wikiLinks);
        string[] lines = task.Description.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (TaskBacklinkSource.IsAttachmentTo(line, document, resolver))
            {
                return line[1..^1];
            }

            if (wikilinkLines.Contains(index + 1))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="line"/> is an attachment line whose path is <paramref name="document"/>.</summary>
    private static bool IsAttachmentTo(string line, LibraryPath document, LibraryPathResolver resolver)
    {
        if (!TaskAttachmentText.IsPathLine(line) || !resolver.TryResolveAbsolute(line[1..^1], out LibraryPath? target))
        {
            return false;
        }

        return TaskBacklinkSource.IsSame(target, document.Root.Id, document.RelativePath);
    }

    /// <summary>The 1-based description lines whose wikilinks resolve to <paramref name="document"/>, resolved from the Task's own file.</summary>
    private static HashSet<int> WikilinkLines(TaskItem task, LibraryPath document, LibraryPathResolver resolver, WikiLinkIndex wikiLinks)
    {
        HashSet<int> lines = [];
        if (!task.Description.Contains("[[", StringComparison.Ordinal)
            || !TaskBacklinkSource.TryLocateTaskFile(task, resolver, out LibraryPath? from)
            || !string.Equals(from.Root.Id, document.Root.Id, StringComparison.Ordinal))
        {
            return lines;
        }

        foreach (WikiLink link in WikiLinkParser.Parse(task.Description))
        {
            string? resolved = wikiLinks.Resolve(from, link).Path;
            if (resolved is not null && FolderSnapshot.PathComparer.Equals(resolved, document.RelativePath))
            {
                _ = lines.Add(link.Line);
            }
        }

        return lines;
    }

    /// <summary>
    /// Places <paramref name="task"/>'s file in the Teams root, as the note a wikilink is measured from. The
    /// resolver refuses any underscore folder under Teams, so <c>_tasks/</c> cannot go through it; the path built
    /// here is only a position for <see cref="WikiLinkIndex.Resolve"/> and is never read or written.
    /// </summary>
    private static bool TryLocateTaskFile(TaskItem task, LibraryPathResolver resolver, [NotNullWhen(true)] out LibraryPath? from)
    {
        from = null;
        if (!resolver.TryResolve(TeamsRootId, string.Empty, out LibraryPath? teamsRoot, out _))
        {
            return false;
        }

        string relative = Path.GetRelativePath(teamsRoot.FullPath, task.Path);
        if (Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return false;
        }

        from = new LibraryPath(teamsRoot.Root, relative.Replace('\\', '/'), task.Path, LibraryNodeRole.File, ReadOnly: true);
        return true;
    }

    private static bool IsSame(LibraryPath path, string rootId, string relativePath) =>
        string.Equals(path.Root.Id, rootId, StringComparison.Ordinal) && FolderSnapshot.PathComparer.Equals(path.RelativePath, relativePath);
}

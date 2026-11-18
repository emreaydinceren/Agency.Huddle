using Microsoft.Extensions.Options;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.DocHistory;

/// <summary>
/// The one writer of History entries (DocHistory spec §6.4): stamps each with the injected clock, records
/// the Human's saves and creates under the Human's live Name, records an Agent's <c>log_change</c> under the
/// Name the tool was built for, and carries a history across a Library rename or move. Only Markdown
/// documents have a history, and nothing is written while <see cref="DocHistoryOptions.Enabled"/> is false.
/// A failure to write is logged and never fails the save, create or move that caused it.
/// </summary>
internal sealed class DocHistoryRecorder
{
    private readonly DocHistoryStore store;
    private readonly IOptions<TeamOptions> options;
    private readonly ILogger<DocHistoryRecorder> logger;
    private readonly IHumanIdentity human;
    private readonly TimeProvider clock;

    /// <summary>Creates the recorder.</summary>
    /// <param name="store">Where entries are kept.</param>
    /// <param name="options">The bound options; <see cref="TeamOptions.DocHistory"/> is read on every call.</param>
    /// <param name="logger">Receives write failures.</param>
    /// <param name="human">The Human's live Name (rules.md). Built by hand without one, the configured Name is used.</param>
    /// <param name="timeProvider">The clock; <see cref="TimeProvider.System"/> when omitted.</param>
    public DocHistoryRecorder(
        DocHistoryStore store,
        IOptions<TeamOptions> options,
        ILogger<DocHistoryRecorder> logger,
        IHumanIdentity? human = null,
        TimeProvider? timeProvider = null)
    {
        this.store = store;
        this.options = options;
        this.logger = logger;
        this.human = human ?? new ConfiguredHumanIdentity(options.Value.HumanName);
        this.clock = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Whether History is on for this installation.</summary>
    internal bool Enabled => this.options.Value.DocHistory.Enabled;

    /// <summary>Whether <paramref name="file"/> can have a history: a Markdown document.</summary>
    /// <param name="file">A Library item.</param>
    /// <returns><see langword="true"/> for a file with a Markdown extension (<see cref="LibraryFileKinds.IsMarkdown"/>).</returns>
    internal static bool HasHistory(LibraryPath file) =>
        file.IsFile && LibraryFileKinds.IsMarkdown(file.RelativePath);

    /// <summary>Records the Human's successful save of <paramref name="file"/> as <see cref="DocChangeKind.Edited"/>.</summary>
    /// <param name="file">The saved document.</param>
    internal void HumanSaved(LibraryPath file) => this.TryRecordHuman(file, DocChangeKind.Edited);

    /// <summary>Records the Human's successful create of <paramref name="file"/> as <see cref="DocChangeKind.Created"/>.</summary>
    /// <param name="file">The created document.</param>
    internal void HumanCreated(LibraryPath file) => this.TryRecordHuman(file, DocChangeKind.Created);

    /// <summary>Records an Agent's <c>log_change</c> call. Unlike the Human's entries, a failure is reported to the caller.</summary>
    /// <param name="file">The document, already resolved and checked by the tool.</param>
    /// <param name="agentName">The Name the tool was built for.</param>
    /// <param name="kind">What the Agent says it did.</param>
    /// <param name="summary">The Agent's one line, already checked by the tool.</param>
    /// <param name="goldfish">Whether the Agent logged it as a goldfish.</param>
    /// <returns>Whether the entry was written.</returns>
    internal bool AgentLogged(LibraryPath file, string agentName, DocChangeKind kind, string summary, bool goldfish = false)
    {
        try
        {
            this.store.Append(file, new DocHistoryEntry(this.clock.GetUtcNow(), agentName, DocActorKind.Agent, kind, summary, goldfish));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(exception, "Could not record {Agent}'s change to {Path} in its history.", agentName, file.FullPath);
            return false;
        }
    }

    /// <summary>
    /// Records a write Huddle made on a Helper's Verdict (Team Memory Helper spec §6.6), when History is on and the file is Markdown.
    /// A failure is logged and never undoes the write.
    /// </summary>
    /// <param name="file">The document.</param>
    /// <param name="helperActor">The Helper's actor name, such as <c>Team Memory (Helper)</c>.</param>
    /// <param name="kind">Whether the file was created or edited.</param>
    /// <param name="summary">One line, cut here to <c>Team:DocHistory:MaxSummaryLength</c>.</param>
    /// <param name="postToFeed"><see langword="false"/> when the Helper reports the write in a Feed item of its own, as Memory Tidy does for a whole pass.</param>
    internal void HelperWrote(LibraryPath file, string helperActor, DocChangeKind kind, string summary, bool postToFeed = true)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(summary);
        if (!this.Enabled || !HasHistory(file))
        {
            return;
        }

        int max = this.options.Value.DocHistory.MaxSummaryLength;
        string line = max > 3 && summary.Length > max ? string.Concat(summary.AsSpan(0, max - 3), "...") : summary;
        try
        {
            this.store.Append(file, new DocHistoryEntry(this.clock.GetUtcNow(), helperActor, DocActorKind.Helper, kind, line), postToFeed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(exception, "Could not record {Helper}'s change to {Path} in its history.", helperActor, file.FullPath);
        }
    }

    /// <summary>Carries the history of <paramref name="from"/> to <paramref name="to"/> after a Library rename or move.</summary>
    /// <param name="from">The item before the move.</param>
    /// <param name="to">The item after the move.</param>
    internal void Moved(LibraryPath from, LibraryPath to)
    {
        // Not guarded by Enabled: moving a history that exists is not recording, and a document moved while History
        // is off must still have its history where the document is when History is turned back on (spec §8).
        try
        {
            this.store.Move(from, to);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(exception, "Could not move the history of {From} to {To}.", from.FullPath, to.FullPath);
        }
    }

    /// <summary>Deletes the History of an unsent upload that was removed, so a later upload under the same name starts clean.</summary>
    /// <param name="file">The deleted upload.</param>
    internal void Discarded(LibraryPath file)
    {
        // Not guarded by Enabled: removing a history that exists is not recording (see Moved).
        try
        {
            this.store.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(exception, "Could not delete the history of {Path}.", file.FullPath);
        }
    }

    /// <summary>Appends one Human entry with an empty summary, when History is on and the file is Markdown.</summary>
    /// <param name="file">The document.</param>
    /// <param name="kind">What the Human did.</param>
    private void TryRecordHuman(LibraryPath file, DocChangeKind kind)
    {
        if (!this.Enabled || !HasHistory(file))
        {
            return;
        }

        try
        {
            this.store.Append(file, new DocHistoryEntry(this.clock.GetUtcNow(), this.human.Name, DocActorKind.Human, kind, string.Empty));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.logger.LogWarning(exception, "Could not record the save of {Path} in its history.", file.FullPath);
        }
    }
}

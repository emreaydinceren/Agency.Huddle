using Microsoft.Extensions.Options;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// A Persona is a markdown file whose body becomes a Claude agent's system prompt. Files live under
/// <c>{DataDir}/{Acp.TeamsDir}/</c>, optionally nested under Team sub-folders that are purely
/// organisational - <c>Teams/Business/coo.md</c> is exactly as much a Persona as <c>Teams/coo.md</c>,
/// and which sub-folder (if any) a file sits under plays no part in its identity or its Team
/// membership.
/// </summary>
/// <remarks>
/// <para>
/// A Persona's identity - <see cref="PersonaIdentity.Name"/>, <see cref="PersonaIdentity.Title"/>
/// and <see cref="PersonaIdentity.Alias"/>, all required, plus an optional
/// <see cref="PersonaIdentity.Teams"/> - comes entirely from its leading YAML frontmatter block,
/// read by <see cref="PersonaFrontmatter.TryReadIdentity(string, out PersonaIdentity?, out string)"/>
/// and assembled across every file by <see cref="PersonaIndex"/>. The filename a file happens to
/// have on disk is storage only: it plays no further part once the file exists, which is what lets
/// <see cref="Update"/> change a Persona's Name without renaming its file, and what makes moving a
/// file between Team sub-folders a complete no-op. A file with no frontmatter, a missing identity
/// field, or an identity that collides with another file's (see <see cref="PersonaIndex"/> for the
/// collision rules) does not become a Persona at all: it is invisible to <see cref="ListNames"/> and
/// <see cref="Get"/>, and shows up only in <see cref="RejectedFiles"/>, naming why - the read path
/// never throws for a bad file.
/// </para>
/// <para>
/// <see cref="PersonaFrontmatter.ComposeJobDescription"/> is a separate concern from identity and
/// stays unenforced: it reads whatever OTHER top-level frontmatter fields a file has to compose the
/// job description <c>mcp__team__list_agents</c> shows, valid identity or not.
/// </para>
/// <para>
/// A Persona's chosen <see cref="Persona.Model"/> and <see cref="Persona.Effort"/> live separately,
/// in <see cref="PersonaModelStore"/>'s and <see cref="PersonaEffortStore"/>'s SQLite tables, keyed
/// by the frontmatter Name - never the filename, and never anything else - because they are mutable
/// app state layered on top of the Persona, not Persona content itself. <see cref="Get"/> is where
/// the file and the two stored values are joined back into one <see cref="Persona"/>, and it is the
/// only place that join happens. A Persona name (front-matter Name, or the filename <see cref="Add"/>
/// writes to) is validated with the same rules as an Agent name, because bringing a Persona online
/// registers an Agent over the pipe.
/// </para>
/// </remarks>
public sealed class PersonaStore : IDisposable, IMentionAliasSource
{
    // Editors commonly fire several filesystem events per save (a temp-file write plus a rename,
    // or several partial writes), so raising PersonasChanged straight off FileSystemWatcher would
    // thrash every observer (the /teammates page, PersonaSupervisor). Coalesce a burst of events
    // into one PersonasChanged per pause in activity.
    private const int WatcherDebounceMilliseconds = 500;

    // FileSystemWatcher buffers events in a fixed-size kernel window (8 KB by default) and the OS
    // drops events outright — no exception, no log, nothing — when it overflows; it raises the
    // Error event instead. A `git checkout` or a script touching a dozen Persona files at once is
    // enough to trigger it at the default size. 64 KB is cheap at this file count and makes an
    // overflow far rarer, though OnWatcherError below is the real backstop.
    private const int WatcherInternalBufferSize = 64 * 1024;

    private readonly string teamsDir;
    private readonly PersonaModelStore models;
    private readonly PersonaEffortStore efforts;
    private readonly ILogger<PersonaStore> logger;
    private readonly Lock watchGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    // The whole discovered state of the Teams directory, swapped in as one immutable reference under
    // watchGate. Readers never take the lock: a `volatile` field is all a single-writer-many-readers
    // publish of an immutable snapshot needs, and PersonaIndex never mutates once built.
    private volatile PersonaIndex index;

    /// <summary>Scans the Teams directory and starts watching it for changes.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.DataDir"/> and <see cref="AcpOptions.TeamsDir"/>, which together locate the Teams directory.</param>
    /// <param name="models">The SQLite-backed store for each Persona's chosen Model.</param>
    /// <param name="efforts">The SQLite-backed store for each Persona's chosen Effort.</param>
    /// <param name="logger">Used to warn if the filesystem watcher reports a dropped-event buffer overflow.</param>
    public PersonaStore(
        IOptions<TeamOptions> options,
        PersonaModelStore models,
        PersonaEffortStore efforts,
        ILogger<PersonaStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(efforts);
        ArgumentNullException.ThrowIfNull(logger);

        this.models = models;
        this.efforts = efforts;
        this.logger = logger;
        this.teamsDir = Path.Combine(options.Value.DataDir, options.Value.Acp.TeamsDir);

        // Created eagerly (rather than lazily on first Add) so the watcher below has a directory
        // to watch from the moment the app starts, even before any Persona has been added.
        Directory.CreateDirectory(this.teamsDir);

        this.index = this.RebuildIndexFromDisk();

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        // Filter widened from "*.md" to "*" (see IsMarkdownFile in OnWatcherEvent, which restores
        // the narrowing): a directory rename - Teams/Business becoming Teams/BusinessOps - raises
        // a Renamed event whose Name is the directory itself, never matching "*.md", which would
        // otherwise drop the event and leave every Persona path nested under it stale.
        // IncludeSubdirectories = true is the headline fix of this phase: without it, a Persona
        // under a Team sub-folder is found once by the startup scan and then never reloads on
        // edit again - no error, no log, nothing, exactly the shape docs/agencyteam/traps.md warns
        // about. NotifyFilters.DirectoryName is what makes a directory rename raise an event at
        // all.
        this.watcher = new FileSystemWatcher(this.teamsDir, "*")
        {
            IncludeSubdirectories = true,
            InternalBufferSize = WatcherInternalBufferSize,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };
        this.watcher.Changed += this.OnWatcherEvent;
        this.watcher.Created += this.OnWatcherEvent;
        this.watcher.Deleted += this.OnWatcherEvent;
        this.watcher.Renamed += this.OnWatcherEvent;
        this.watcher.Error += this.OnWatcherError;
        this.watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Raised exactly once per write operation - <see cref="Add"/>, <see cref="Update"/> and
    /// <see cref="Remove"/> each raise it once themselves after refreshing the index, and the
    /// filesystem watcher's debounce raises it once per settled burst of external changes. Never
    /// raised while the index is stale: every raise is preceded by a fresh <see cref="RebuildIndexFromDisk"/>.
    /// </summary>
    public event Action? PersonasChanged;

    /// <summary>Every Persona that loaded cleanly, ordered by Name (ordinal). A projection over <see cref="Entries"/>.</summary>
    public IReadOnlyList<string> ListNames() => this.index.Entries.Select(entry => entry.Name).ToList();

    /// <summary>Every Persona that loaded cleanly, with its identity, discovered path and cached text.</summary>
    public IReadOnlyList<PersonaEntry> Entries => this.index.Entries;

    /// <summary>
    /// Every file under the Teams directory that did NOT become a Persona - a missing identity field,
    /// or a Name/Alias collision with another file - for the UI to show the user why a file they
    /// expected to see is missing. Never throws; a bad file is data, not an error.
    /// </summary>
    public IReadOnlyList<RejectedPersonaFile> RejectedFiles => this.index.Rejected;

    /// <summary>The distinct Team names named by any valid Persona's frontmatter, sorted (ordinal).</summary>
    public IReadOnlyList<string> Teams => this.index.Teams;

    /// <summary>
    /// Every valid Persona's Alias, paired with its Name. Implements <see cref="IMentionAliasSource"/>
    /// so <see cref="MentionParser"/> and <see cref="ChatService"/> can resolve an
    /// Alias as a Mention or an <c>/invite</c> target through the interface alone - registered against
    /// this same singleton instance (see
    /// <see cref="Agency.Huddle.App.ServiceCollectionExtensions.AddTeamServices"/>) - without either
    /// one taking a direct dependency on <see cref="PersonaStore"/> itself.
    /// </summary>
    public IReadOnlyList<MentionAlias> Aliases => this.index.Entries.Select(entry => new MentionAlias(entry.Alias, entry.Name)).ToList();

    /// <summary>Resolves a Persona by Name first, then by Alias, both case-insensitively. Identity only - no Model/Effort join; see <see cref="Get"/> for that.</summary>
    /// <param name="nameOrAlias">The Name or Alias to resolve.</param>
    public PersonaEntry? ResolveByNameOrAlias(string nameOrAlias) => this.index.ByNameOrAlias(nameOrAlias);

    /// <summary>Looks up a Persona by its front-matter Name, joining in its stored Model and Effort. Returns <see langword="null"/>, never throws, when no Persona has that Name.</summary>
    /// <param name="name">The Persona's Name.</param>
    public Persona? Get(string name)
    {
        var entry = this.index.ByName(name);
        if (entry is null)
        {
            return null;
        }

        // This one line is the entire file-to-database join, and it happens in exactly one place:
        // both PersonaSupervisor and the razor page already go through Get.
        return new Persona(entry.Name, entry.Text, this.models.Get(entry.Name), this.efforts.Get(entry.Name));
    }

    /// <summary>
    /// Creates a new Persona file at the Teams root - never inside a Team sub-folder, because
    /// sub-folders are the user's own filing system and the app never guesses which one a new file
    /// belongs in. The identity is supplied structurally, as <paramref name="identity"/>, rather
    /// than parsed out of raw text: this method composes the file's frontmatter itself (see
    /// <see cref="PersonaFrontmatter.Compose"/>), so the filename it writes to
    /// (<c>{identity.Name}.md</c>) and the identity the file actually contains can never disagree -
    /// the exact bug this signature replaces, where a caller could write <c>Bob.md</c> containing a
    /// Persona named "Jarvis". <see cref="NameRules.IsValidAgentName(string?)"/> guards
    /// <paramref name="identity"/>'s Name, which makes this the one remaining path-traversal guard
    /// now that <see cref="Get"/>, <see cref="PathFor"/>, <see cref="Update"/> and
    /// <see cref="Remove"/> all resolve a path through the index - an enumeration of files actually
    /// on disk, never a concatenation - instead. Its Alias is guarded the same way: an Alias never
    /// reaches a path, but it is still an identifier a caller could otherwise write something
    /// invalid into. An <paramref name="identity"/>/<paramref name="body"/> combination that would
    /// not load cleanly once composed (a blank Title, or a Name/Alias that collides with an
    /// existing Persona) throws <see cref="ChatException"/> before anything is written to disk, and
    /// the identity used to key <paramref name="model"/> and <paramref name="effort"/> in SQLite is
    /// whatever the composed text's frontmatter Name resolves to - normally
    /// <paramref name="identity"/>'s own Name, unless composing and re-parsing it somehow changed
    /// it, which <see cref="ValidateCandidate"/> would already have rejected.
    /// </summary>
    /// <param name="identity">The new Persona's Name, Title, Alias and Teams.</param>
    /// <param name="body">The Persona's system-prompt body - everything after the frontmatter this method composes.</param>
    /// <param name="model">The Model to store for the new Persona, or <see langword="null"/> for the agent's default.</param>
    /// <param name="effort">The Effort to store for the new Persona, or <see langword="null"/> for the model's default.</param>
    public Persona Add(PersonaIdentity identity, string body, string? model = null, string? effort = null)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!NameRules.IsValidAgentName(identity.Name))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"'{identity.Name}' is not a valid Persona name.");
        }

        if (!NameRules.IsValidAgentName(identity.Alias))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"'{identity.Alias}' is not a valid Persona alias.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ChatException(ErrorCodes.BadMessage, "Persona text must not be blank.");
        }

        var path = Path.Combine(this.teamsDir, $"{identity.Name}.md");
        if (File.Exists(path))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Persona '{identity.Name}' already exists.");
        }

        var text = PersonaFrontmatter.Compose(identity, body);

        var entry = this.ValidateCandidate(path, text, excludingPath: null);

        Directory.CreateDirectory(this.teamsDir);
        File.WriteAllText(path, text);
        this.models.Set(entry.Name, model);
        this.efforts.Set(entry.Name, effort);

        this.RefreshIndexAndNotify();

        return new Persona(entry.Name, text, model, effort);
    }

    /// <summary>
    /// Overwrites an existing Persona's file, at whichever path it was actually discovered under -
    /// nested or not - and its stored model and effort together. Because a system prompt, a model AND
    /// an effort are all fixed at <c>session/new</c> (docs/acp/agent-guide.md §3.5), the only way for
    /// any of these edits to take effect is for the observer of <see cref="PersonasChanged"/>
    /// (<see cref="PersonaSupervisor"/>) to restart that Persona's session, losing its conversation
    /// memory. There is deliberately no default for <paramref name="model"/> or
    /// <paramref name="effort"/>, and no separate "SetModel"/"SetEffort" method: a defaulted overload
    /// of either would let an existing call site silently wipe a stored value, and a separate
    /// single-field setter would raise its own PersonasChanged, causing the exact multi-restart this
    /// method's single event avoids.
    /// </summary>
    /// <remarks>
    /// A <paramref name="text"/> that would not load cleanly (no valid identity, or a collision) is
    /// rejected with <see cref="ChatException"/> and the file is left untouched. When
    /// <paramref name="text"/>'s frontmatter Name differs from the Persona's current Name, the OLD
    /// Name's Model and Effort rows are removed before the new ones are stored under the NEW Name -
    /// without this, editing <c>name:</c> would silently drop the teammate's Model and Effort, the
    /// exact "silently resurrect an old setting" failure docs/agencyteam/rules.md exists to prevent,
    /// just running the other way (silently DROPPING a setting rather than resurrecting one).
    /// </remarks>
    public Persona Update(string name, string text, string? model, string? effort)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ChatException(ErrorCodes.BadMessage, "Persona text must not be blank.");
        }

        var current = this.index.ByName(name) ?? throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' does not exist.");

        var entry = this.ValidateCandidate(current.Path, text, excludingPath: current.Path);

        File.WriteAllText(current.Path, text);

        if (!string.Equals(current.Name, entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            this.models.Remove(current.Name);
            this.efforts.Remove(current.Name);
        }

        this.models.Set(entry.Name, model);
        this.efforts.Set(entry.Name, effort);

        this.RefreshIndexAndNotify();

        return new Persona(entry.Name, text, model, effort);
    }

    /// <summary>
    /// Deletes a Persona's file, which stops its runner the next time
    /// <see cref="PersonasChanged"/> is observed. This intentionally does NOT cascade: the Agent,
    /// its Rooms, its memberships and its Transcripts all remain in the Team Directory untouched (a
    /// decision of the repo owner). The Agent simply goes offline permanently, exactly as any pipe
    /// client that disconnects does — "removing a Persona" is not "deleting an Agent". The stored
    /// Model and Effort DO go with the file, though: unlike Agents/Rooms/Transcripts, which are chat
    /// facts that outlive the Persona that created them, a Model and an Effort are part of the
    /// Persona itself. Leaving either row behind would silently resurrect an old setting if a
    /// Persona of the same name were re-created later.
    /// </summary>
    public void Remove(string name)
    {
        var current = this.index.ByName(name) ?? throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' does not exist.");

        File.Delete(current.Path);
        this.models.Remove(current.Name);
        this.efforts.Remove(current.Name);

        this.RefreshIndexAndNotify();
    }

    /// <summary>The absolute path of a Persona's file, at whichever path it was actually discovered under, for the "open in default editor" feature.</summary>
    public string PathFor(string name)
    {
        var current = this.index.ByName(name) ?? throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' does not exist.");

        return current.Path;
    }

    /// <summary>Stops watching the Teams directory and releases the debounce timer.</summary>
    public void Dispose()
    {
        lock (this.watchGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.watcher.EnableRaisingEvents = false;
        this.watcher.Changed -= this.OnWatcherEvent;
        this.watcher.Created -= this.OnWatcherEvent;
        this.watcher.Deleted -= this.OnWatcherEvent;
        this.watcher.Renamed -= this.OnWatcherEvent;
        this.watcher.Error -= this.OnWatcherError;
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }

    /// <summary>
    /// Builds a candidate index - the current valid entries, with the file at <paramref name="path"/>
    /// substituted for <paramref name="text"/> (and, for an update, its OLD entry at
    /// <paramref name="excludingPath"/> removed first) - and returns the resulting entry for
    /// <paramref name="path"/>, or throws <see cref="ChatException"/> naming the rejection reason if
    /// the candidate would not load. Pure and disk-free: <see cref="Add"/> and <see cref="Update"/>
    /// both call this BEFORE writing anything, so a rejected save never touches the file.
    /// </summary>
    /// <param name="path">The path the candidate file would live (or already lives) at.</param>
    /// <param name="text">The candidate file's full raw text.</param>
    /// <param name="excludingPath">
    /// For an update, the path of the entry being replaced (normally equal to <paramref name="path"/>
    /// itself), removed from the base set before the candidate is added back in. <see langword="null"/>
    /// for a brand new file, which has no existing entry to remove.
    /// </param>
    private PersonaEntry ValidateCandidate(string path, string text, string? excludingPath)
    {
        var files = this.index.Entries
            .Where(existing => excludingPath is null || !string.Equals(existing.Path, excludingPath, StringComparison.Ordinal))
            .Select(existing => (existing.Path, existing.Text))
            .Append((path, text))
            .ToList();

        var candidate = PersonaIndex.Build(files);

        var rejection = candidate.Rejected.FirstOrDefault(file => string.Equals(file.Path, path, StringComparison.Ordinal));
        if (rejection is not null)
        {
            throw new ChatException(ErrorCodes.BadMessage, rejection.Reason);
        }

        return candidate.Entries.First(entry => string.Equals(entry.Path, path, StringComparison.Ordinal));
    }

    /// <summary>
    /// Scans the Teams directory recursively and parses every ".md" file into a fresh
    /// <see cref="PersonaIndex"/>. The only place this class touches the filesystem to build the
    /// index - <see cref="ValidateCandidate"/>, and <see cref="PersonaIndex"/> itself, are pure.
    /// </summary>
    private PersonaIndex RebuildIndexFromDisk()
    {
        if (!Directory.Exists(this.teamsDir))
        {
            return PersonaIndex.Build([]);
        }

        // SearchOption.AllDirectories: Team sub-folders are purely organisational, so a Persona
        // nested under one is exactly as much a Persona as one at the top level.
        var files = Directory.GetFiles(this.teamsDir, "*.md", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();

        return PersonaIndex.Build(files);
    }

    /// <summary>
    /// Rebuilds the index from disk and swaps it in, then raises <see cref="PersonasChanged"/> exactly
    /// once. Used by <see cref="Add"/>, <see cref="Update"/> and <see cref="Remove"/> right after each
    /// writes to disk, so the index is always rebuilt BEFORE the event fires - otherwise
    /// <see cref="PersonaSupervisor.OnPersonasChanged"/> would read the stale snapshot the write was
    /// meant to replace.
    /// </summary>
    private void RefreshIndexAndNotify()
    {
        lock (this.watchGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.index = this.RebuildIndexFromDisk();
        }

        // Exactly one PersonasChanged per operation: three writes raising three events would cause
        // three restarts (PersonaSupervisor spawning three "node" adapter processes) for what the
        // caller sees as a single save.
        this.PersonasChanged?.Invoke();
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        if (!AffectsATeamsFile(e))
        {
            return;
        }

        lock (this.watchGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    // The watcher's filter is "*" (widened from "*.md" in the constructor), so this is now the
    // only thing keeping a stray non-Persona file (a ".txt", a lock file) from churning the
    // debounce and restarting every online Persona for no reason. Two deliberate exceptions:
    //   - A directory rename: Teams/Business becoming Teams/BusinessOps raises a Renamed event
    //     whose Name is the directory itself, which never matches ".md", yet every Persona path
    //     nested under it just went stale. Directory.Exists(e.FullPath) is safe to call here
    //     because a rename never deletes the item - the new path exists for as long as this
    //     handler runs, which FileSystemWatcher guarantees by invoking it synchronously off the
    //     event.
    //   - A directory delete or move-away: the same sub-folder disappearing outright raises a
    //     Deleted event, also named after the directory, but by the time this handler runs
    //     Directory.Exists(e.FullPath) is false - the Rename check above cannot catch it. A
    //     directory name has no extension, so HasNoExtension is the cheap, sufficient signal (the
    //     worst case is one extra, harmless refresh for a rare extension-less non-Persona file).
    //     Missing this used to just mean a stale ListNames() entry; now that the index CACHES each
    //     file's text, missing it means PersonaSupervisor and the razor card keep serving stale
    //     prompt text for every Persona that was nested under the removed folder, forever.
    private static bool AffectsATeamsFile(FileSystemEventArgs e) =>
        IsMarkdownFile(e.Name)
        || (e.ChangeType == WatcherChangeTypes.Renamed && Directory.Exists(e.FullPath))
        || (e.ChangeType == WatcherChangeTypes.Deleted && HasNoExtension(e.Name));

    private static bool IsMarkdownFile(string? name) =>
        name is not null && string.Equals(Path.GetExtension(name), ".md", StringComparison.OrdinalIgnoreCase);

    private static bool HasNoExtension(string? name) => name is not null && Path.GetExtension(name).Length == 0;

    // FileSystemWatcher raises this instead of a normal change event when its internal buffer
    // overflows and the OS drops events - see WatcherInternalBufferSize's comment. There is no way
    // to know which paths were dropped, so the only correct response is the same one a normal
    // change takes: schedule a refresh on the existing debounce path rather than trust whatever
    // the watcher's view of the world now is.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        this.logger.LogWarning(
            e.GetException(),
            "PersonaStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");

        lock (this.watchGate)
        {
            if (this.disposed)
            {
                return;
            }

            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        Action? changed;
        lock (this.watchGate)
        {
            // Checked and captured under the same lock Dispose() takes, so a Dispose() racing
            // this callback either finishes first (this returns without capturing anything) or
            // this captures the delegate before Dispose() can flip the flag — never both. The
            // delegate is invoked OUTSIDE the lock, below: no current subscriber takes watchGate,
            // but invoking arbitrary subscriber code while holding a lock this class also takes
            // from Dispose() and every watcher-event handler is exactly the shape that deadlocks
            // the moment a future subscriber does take a lock of its own.
            if (this.disposed)
            {
                return;
            }

            // Rebuilt under the same lock, and BEFORE the delegate capture below: an observer
            // reading this store from inside its own PersonasChanged handler (PersonaSupervisor
            // does exactly this) must see the state the filesystem change just produced, not
            // whatever was true before it.
            this.index = this.RebuildIndexFromDisk();
            changed = this.PersonasChanged;
        }

        changed?.Invoke();
    }
}

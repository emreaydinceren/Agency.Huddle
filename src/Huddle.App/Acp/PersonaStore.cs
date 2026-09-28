using Microsoft.Extensions.Options;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The old and the new front-matter Name of a Persona file whose <c>name:</c> changed in place -
/// see <see cref="PersonaStore"/>'s <c>PersonaRenamed</c> event, which carries one of these per
/// detected rename.
/// </summary>
/// <param name="OldName">The Name the Persona resolved to before this change.</param>
/// <param name="NewName">The Name the same physical file resolves to now.</param>
public sealed record PersonaRenamed(string OldName, string NewName);

/// <summary>
/// The front-matter Name of a Persona file that no longer exists - see <see cref="PersonaStore"/>'s
/// <c>PersonaRemoved</c> event, which carries one of these per detected removal.
/// </summary>
/// <param name="Name">The Name the removed Persona resolved to.</param>
public sealed record PersonaRemoved(string Name);

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

    // Serialises Add and Update end to end - exists-check through the synchronous index publish -
    // so a second writer can never validate against a snapshot the first writer's own write is
    // about to invalidate (Spec §14 D-14). Held ONLY across that span; the event raise that follows
    // (NotifyChanged) always runs after this lock is released - see that method's remarks for why.
    // A distinct lock from watchGate on purpose: watchGate guards the index SWAP itself (taken by
    // the watcher's debounce too, which never writes a file and so never needs writeGate), while
    // writeGate guards the whole check-then-write-then-publish sequence around a caller's own edit.
    // Nested only in one direction - writeGate, then watchGate, inside PublishFreshIndex - never the
    // reverse; see PublishFreshIndex's remarks for the ordering proof.
    private readonly Lock writeGate = new();

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

    /// <summary>
    /// Raised when an existing Persona file's front-matter Name changed - detected in
    /// <see cref="RaiseRenames"/> by comparing the previous index against the freshly rebuilt one,
    /// keyed by <see cref="PersonaEntry.Path"/> rather than Name, because the path is the one thing a
    /// rename cannot touch: <see cref="Update"/> deliberately rewrites the SAME physical file, and a
    /// file hand-edited outside the app reaches only the debounced watcher path
    /// (<see cref="OnDebounceElapsed"/>), where no other record of the old Name survives anywhere.
    /// Compared with <see cref="StringComparison.Ordinal"/> rather than
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>, so a case-only rename ("coo" to "Coo") is
    /// still announced - the display Name genuinely changed even though SQLite's Model/Effort rows
    /// and <see cref="PersonaIndex"/> itself treat the two as equal.
    /// </summary>
    /// <remarks>
    /// Raised SYNCHRONOUSLY and to completion, strictly BEFORE <see cref="PersonasChanged"/>, from
    /// <see cref="NotifyChanged"/> - called by <see cref="Add"/>, <see cref="Update"/> and
    /// <see cref="Remove"/> (via <see cref="RefreshIndexAndNotify"/>) after each writes to disk, and
    /// by <see cref="OnDebounceElapsed"/> when the watcher settles on an external change. This
    /// ordering is load-bearing, not incidental: <see cref="Agency.Huddle.App.Acp.PersonaSupervisor"/> reacts to
    /// <see cref="PersonasChanged"/> by stopping the OLD Name's runner and starting the NEW Name's,
    /// and the newly started runner registers itself over the pipe under the new Name the instant it
    /// connects. If the Team Directory row has not already been renamed in place by the time that
    /// registration happens, it mints a brand-new Team Directory user id instead of reusing the
    /// renamed row's - the exact ghost (a stale Agent, its Rooms and its Transcripts left behind
    /// under the old Name) this event exists to prevent. Firing this event first, and waiting for
    /// every subscriber to finish handling it, is what guarantees the rename-in-place wins that race.
    /// This class deliberately takes no dependency on
    /// <see cref="Agency.Huddle.App.Data.ITeamDirectory"/> to do the renaming itself - see
    /// docs/agencyteam/rules.md - it only announces that a rename happened.
    /// </remarks>
    internal event Action<PersonaRenamed>? PersonaRenamed;

    /// <summary>
    /// Raised when a Persona file that used to exist is gone - detected in <see cref="RaiseRemovals"/>
    /// by the same path-keyed diff <see cref="RaiseRenames"/> uses: a path present only in
    /// <c>previous</c> is a deletion, never a rename, because a rename keeps its path. Raised from
    /// both doors <see cref="PersonaRenamed"/> is raised from, and for the same reason: from
    /// <see cref="Remove"/>'s own call to <see cref="RefreshIndexAndNotify"/>, and from
    /// <see cref="OnDebounceElapsed"/>, which is the only place a <c>.md</c> file deleted in an editor
    /// is ever noticed. Covering just the first would leave a hand-deleted Persona's per-Persona state
    /// behind with nothing left to collect it.
    /// </summary>
    /// <remarks>
    /// Raised SYNCHRONOUSLY and to completion, strictly BEFORE <see cref="PersonasChanged"/>, for the
    /// same reason as <see cref="PersonaRenamed"/>: a subscriber cascading a removal (see
    /// <see cref="Agency.Huddle.App.Acp.PersonaRenameCascade"/>) needs the fact settled before
    /// <see cref="PersonaSupervisor"/> reacts to <see cref="PersonasChanged"/>. This class deliberately
    /// takes no dependency on anything downstream to do the cascading itself - see
    /// docs/agencyteam/rules.md - it only announces that a removal happened.
    /// </remarks>
    internal event Action<PersonaRemoved>? PersonaRemoved;

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
    /// The absolute path of the Teams directory this store reads and writes Persona files under.
    /// Exposed so <c>CandidateChecker</c> can rewrite an absolute path inside a
    /// <see cref="Check"/> rejection message into one relative to it (Spec §6.8), without this
    /// store's own layout leaking any further than that one call site.
    /// </summary>
    internal string TeamsDirectory => this.teamsDir;

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
        // both PersonaSupervisor and the razor page already go through Get. Adapter is not part of
        // that join - unlike Model and Effort, it travels with the file itself (Spec §7.1), so it
        // is carried straight off the entry rather than looked up in a store.
        return new Persona(entry.Name, entry.Text, this.models.Get(entry.Name), this.efforts.Get(entry.Name), entry.Adapter);
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

        string text;
        PersonaEntry entry;
        (PersonaIndex Previous, PersonaIndex Updated)? change;

        // Spec §14 D-14: the exists-check, the collision check AND the write must all run under
        // ONE lock, held through the synchronous index publish below - otherwise a second Add
        // racing this one could pass its own check against the same pre-write snapshot and write
        // a colliding file before this Add's write is ever visible to it.
        lock (this.writeGate)
        {
            var path = Path.Combine(this.teamsDir, $"{identity.Name}.md");
            if (File.Exists(path))
            {
                throw new ChatException(ErrorCodes.BadMessage, $"Persona '{identity.Name}' already exists.");
            }

            text = PersonaFrontmatter.Compose(identity, body);

            entry = this.ValidateCandidate(path, text, excludingPath: null);

            Directory.CreateDirectory(this.teamsDir);
            File.WriteAllText(path, text);
            this.models.Set(entry.Name, model);
            this.efforts.Set(entry.Name, effort);

            // Published BEFORE writeGate is released, not after: the next Add or Update to take
            // this lock must see an index that already reflects THIS write, or it would pass
            // ValidateCandidate against the same stale snapshot this Add itself just resolved.
            change = this.PublishFreshIndex();
        }

        // Raised only after writeGate is released - see NotifyChanged's remarks for why.
        this.NotifyChanged(change);

        return new Persona(entry.Name, text, model, effort, entry.Adapter);
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

        PersonaEntry entry;
        (PersonaIndex Previous, PersonaIndex Updated)? change;

        // Same hole as Add, and the same fix: the "current Persona" lookup, the collision check
        // AND the write must all run under ONE lock, held through the synchronous index publish -
        // otherwise a concurrent Add or Update could validate against the same pre-write snapshot
        // this Update resolves its own collision check against.
        lock (this.writeGate)
        {
            var current = this.index.ByName(name) ?? throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' does not exist.");

            entry = this.ValidateCandidate(current.Path, text, excludingPath: current.Path);

            File.WriteAllText(current.Path, text);

            if (!string.Equals(current.Name, entry.Name, StringComparison.OrdinalIgnoreCase))
            {
                this.models.Remove(current.Name);
                this.efforts.Remove(current.Name);
            }

            this.models.Set(entry.Name, model);
            this.efforts.Set(entry.Name, effort);

            // Published BEFORE writeGate is released - see Add's matching comment.
            change = this.PublishFreshIndex();
        }

        // Raised only after writeGate is released - see NotifyChanged's remarks for why.
        this.NotifyChanged(change);

        return new Persona(entry.Name, text, model, effort, entry.Adapter);
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

    /// <summary>
    /// Throws the same <see cref="ChatException"/> <see cref="Add"/> itself throws when a file
    /// already sits at the path <see cref="Add"/> would write <paramref name="name"/> to - checked
    /// directly against the filesystem, since a file that never became a valid Persona is invisible
    /// to <see cref="Entries"/> and <see cref="Check"/>'s collision rules alike, both of which only
    /// see parsed identities. Does nothing when no such file exists. <c>CandidateChecker</c> is the
    /// one caller (Spec §6.8's "files" step); it catches this the way it catches any
    /// <see cref="ChatException"/> the store raises, turning it into a problem string.
    /// </summary>
    /// <param name="name">The Name to check a file does not already exist for.</param>
    internal void EnsureNoFileExistsFor(string name)
    {
        var path = Path.Combine(this.teamsDir, $"{name}.md");
        if (File.Exists(path))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' already exists.");
        }
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
    /// Reports every problem each of <paramref name="texts"/> would hit if it were written as a brand
    /// new Persona right now - collisions with the current entries AND with each other - without
    /// writing anything to disk or raising any event (Spec §6.8). "Extract, do not copy": this runs
    /// through <see cref="BuildCandidateIndex"/>, the exact same engine <see cref="ValidateCandidate"/>
    /// uses for <see cref="Add"/> and <see cref="Update"/>, rather than re-implementing the collision
    /// rules a second time. Each text is assigned a synthetic path under the Teams directory
    /// (<see cref="SyntheticPathsFor"/>) purely so its own rejection - or the lack of one - can be
    /// matched back to it once the shared candidate index is built; that path is never written to.
    /// Guaranteed one result per input text, in input order, even when two texts share a Name and
    /// therefore share the same rejection reason.
    /// </summary>
    /// <param name="texts">Candidate Persona file texts, none of which exist on disk yet.</param>
    /// <returns>
    /// One pair per input text, in the same order: the text itself, and either <see langword="null"/>
    /// when it has no problem, or the exact rejection reason <see cref="PersonaIndex.Build"/> produced
    /// for it.
    /// </returns>
    internal IReadOnlyList<(string Text, string? Problem)> Check(IReadOnlyList<string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var syntheticPaths = this.SyntheticPathsFor(texts);
        var additions = texts.Zip(syntheticPaths, (text, path) => (Path: path, Text: text)).ToList();

        var candidate = this.BuildCandidateIndex(additions, excludingPath: null);

        var reasonsByPath = candidate.Rejected.ToDictionary(file => file.Path, file => file.Reason, StringComparer.Ordinal);

        return texts.Select((text, index) => (text, reasonsByPath.GetValueOrDefault(syntheticPaths[index]))).ToList();
    }

    /// <summary>
    /// One stable, synthetic, never-written-to path per <paramref name="texts"/> entry, in the same
    /// order - <c>{Name}.md</c> for the FIRST text that parses to a given Name (matching the real path
    /// <see cref="Add"/> would write it to), and <c>{Name}~{n}.md</c> for every LATER text that parses
    /// to that SAME Name, OR whose plain <c>{Name}.md</c> is already taken by a real file
    /// (<see cref="PlainPathIsTaken"/>). Without the suffix, two Candidates proposing the same Name -
    /// or one Candidate proposing a Name a real Persona already has - would collapse onto one
    /// identical Path in the candidate index, and <see cref="PersonaIndex.Build"/>'s "others"
    /// exclusion (which compares Path to tell a file apart from itself) would then see no "other" file
    /// for either one - an empty, useless "used by" list instead of the real collision. A text whose
    /// frontmatter does not parse falls back to an index-based placeholder, since it has no Name to
    /// key on and cannot collide with another text on Name either way.
    /// </summary>
    /// <param name="texts">The texts <see cref="Check"/> was given, in order.</param>
    private List<string> SyntheticPathsFor(IReadOnlyList<string> texts)
    {
        var occurrencesByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var paths = new List<string>(texts.Count);

        for (var index = 0; index < texts.Count; index++)
        {
            if (!PersonaFrontmatter.TryReadIdentity(texts[index], out var identity, out _))
            {
                paths.Add(Path.Combine(this.teamsDir, $"__check-{index}.md"));
                continue;
            }

            if (!occurrencesByName.ContainsKey(identity.Name) && this.PlainPathIsTaken(identity.Name))
            {
                // The plain "{Name}.md" path is already a real file's own path - a loaded Persona
                // entry, or a file on disk that never became one - so the FIRST text proposing this
                // Name must not reuse that exact Path too. Seeding the occurrence count at 1 gives it
                // "{Name}~1.md" instead, exactly like a second sibling proposing the same Name
                // already got before this fix.
                occurrencesByName[identity.Name] = 1;
            }

            var occurrence = occurrencesByName.GetValueOrDefault(identity.Name);
            occurrencesByName[identity.Name] = occurrence + 1;

            paths.Add(occurrence == 0
                ? Path.Combine(this.teamsDir, $"{identity.Name}.md")
                : Path.Combine(this.teamsDir, $"{identity.Name}~{occurrence}.md"));
        }

        return paths;
    }

    /// <summary>
    /// Whether <c>{teamsDir}/{name}.md</c> - the plain synthetic path <see cref="SyntheticPathsFor"/>
    /// would otherwise give the FIRST text proposing <paramref name="name"/> - is already a real
    /// file's own path: a loaded Persona entry's <see cref="PersonaEntry.Path"/>, or a file on disk
    /// that never became one (a rejected file, or one written outside this process).
    /// </summary>
    /// <param name="name">The Name to check.</param>
    private bool PlainPathIsTaken(string name)
    {
        var plainPath = Path.Combine(this.teamsDir, $"{name}.md");
        return this.index.Entries.Any(entry => string.Equals(entry.Path, plainPath, StringComparison.Ordinal)) || File.Exists(plainPath);
    }

    /// <summary>
    /// Builds a candidate index - the current valid entries, minus the entry at
    /// <paramref name="excludingPath"/> if any, plus every <paramref name="additions"/> file - with no
    /// filesystem access of its own. The one place <see cref="ValidateCandidate"/> and <see cref="Check"/>
    /// both build the hypothetical index they inspect, so a candidate edit is always validated by
    /// exactly the same engine that decides what the read path shows.
    /// </summary>
    /// <param name="additions">The candidate file(s) to add - one for <see cref="ValidateCandidate"/>, one per text for <see cref="Check"/>.</param>
    /// <param name="excludingPath">
    /// For an update, the path of the entry being replaced, removed from the base set before the
    /// candidate is added back in. <see langword="null"/> when nothing should be removed.
    /// </param>
    private PersonaIndex BuildCandidateIndex(IReadOnlyList<(string Path, string Text)> additions, string? excludingPath)
    {
        var files = this.index.Entries
            .Where(existing => excludingPath is null || !string.Equals(existing.Path, excludingPath, StringComparison.Ordinal))
            .Select(existing => (existing.Path, existing.Text))
            .Concat(additions)
            .ToList();

        return PersonaIndex.Build(files);
    }

    /// <summary>
    /// Builds a candidate index - the current valid entries, with the file at <paramref name="path"/>
    /// substituted for <paramref name="text"/> (and, for an update, its OLD entry at
    /// <paramref name="excludingPath"/> removed first) - and returns the resulting entry for
    /// <paramref name="path"/>, or throws <see cref="ChatException"/> naming the rejection reason if
    /// the candidate would not load. Pure and disk-free: <see cref="Add"/> and <see cref="Update"/>
    /// both call this BEFORE writing anything, so a rejected save never touches the file. Delegates to
    /// <see cref="BuildCandidateIndex"/> for the actual build - the same code <see cref="Check"/> uses.
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
        var candidate = this.BuildCandidateIndex([(path, text)], excludingPath);

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
    /// Rebuilds the index from disk and swaps it in, with no event raised - the publish half of
    /// what used to be one <c>RefreshIndexAndNotify</c> method, split so <see cref="Add"/> and
    /// <see cref="Update"/> can call this synchronously, BEFORE releasing <see cref="writeGate"/>,
    /// while still raising events only afterwards through <see cref="NotifyChanged"/> (Spec §14
    /// D-14). Takes <see cref="watchGate"/> for the swap itself, exactly as the pre-split code did -
    /// still the single lock that guards <see cref="index"/> against <see cref="Dispose"/> and the
    /// watcher's own debounced swap (<see cref="OnDebounceElapsed"/>). Called from inside
    /// <see cref="writeGate"/> by <see cref="Add"/> and <see cref="Update"/>, so the lock order at
    /// that call site is always <see cref="writeGate"/> THEN <see cref="watchGate"/>, never the
    /// reverse - <see cref="OnWatcherEvent"/>, <see cref="OnWatcherError"/>,
    /// <see cref="OnDebounceElapsed"/> and <see cref="Dispose"/> all take ONLY <see cref="watchGate"/>
    /// and never attempt <see cref="writeGate"/>, so the two locks can never deadlock against each
    /// other.
    /// </summary>
    /// <returns>
    /// The (previous, updated) index pair for <see cref="NotifyChanged"/> to diff, or
    /// <see langword="null"/> if the store was disposed mid-write, in which case there is nothing
    /// left to publish or notify.
    /// </returns>
    private (PersonaIndex Previous, PersonaIndex Updated)? PublishFreshIndex()
    {
        lock (this.watchGate)
        {
            if (this.disposed)
            {
                return null;
            }

            var previous = this.index;
            this.index = this.RebuildIndexFromDisk();
            return (previous, this.index);
        }
    }

    /// <summary>
    /// Raises <see cref="PersonaRenamed"/>, then <see cref="PersonaRemoved"/>, then
    /// <see cref="PersonasChanged"/> - in that order, for the reasons each event's own remarks give -
    /// from the (previous, updated) pair <see cref="PublishFreshIndex"/> produced, or does nothing if
    /// that pair is <see langword="null"/> (the store was disposed before publishing). <see cref="Add"/>
    /// and <see cref="Update"/> call this only AFTER releasing <see cref="writeGate"/>, and
    /// <see cref="OnDebounceElapsed"/> only after releasing <see cref="watchGate"/>: invoking
    /// arbitrary subscriber code while holding either lock is exactly the shape that deadlocks the
    /// moment a future subscriber takes a lock of its own - the same reasoning
    /// <see cref="OnDebounceElapsed"/>'s own remarks already gave for <see cref="watchGate"/>, now
    /// extended to <see cref="writeGate"/> too.
    /// </summary>
    /// <param name="change">The pair <see cref="PublishFreshIndex"/> returned.</param>
    private void NotifyChanged((PersonaIndex Previous, PersonaIndex Updated)? change)
    {
        if (change is not { } value)
        {
            return;
        }

        // PersonaRenamed BEFORE PersonasChanged - see that event's own remarks for why the ordering
        // is load-bearing rather than incidental.
        this.RaiseRenames(value.Previous, value.Updated);

        // PersonaRemoved after PersonaRenamed and still before PersonasChanged, for the same reason:
        // a subscriber cascading a removal needs it settled before PersonaSupervisor reacts.
        this.RaiseRemovals(value.Previous, value.Updated);

        // Exactly one PersonasChanged per operation: three writes raising three events would cause
        // three restarts (PersonaSupervisor spawning three "node" adapter processes) for what the
        // caller sees as a single save.
        this.PersonasChanged?.Invoke();
    }

    /// <summary>
    /// Publishes a fresh index and notifies, as one call - used by <see cref="Remove"/>, which has
    /// no <see cref="writeGate"/> hole to close: Spec §14 D-14 is about two WRITES racing each
    /// other, and a delete never collides with anything the way a colliding Name or Alias can.
    /// <see cref="Add"/> and <see cref="Update"/> instead call <see cref="PublishFreshIndex"/>
    /// themselves, synchronously, before releasing <see cref="writeGate"/>, and call
    /// <see cref="NotifyChanged"/> only after releasing it.
    /// </summary>
    private void RefreshIndexAndNotify() => this.NotifyChanged(this.PublishFreshIndex());

    /// <summary>
    /// Diffs <paramref name="previous"/> against <paramref name="updated"/>, keyed by
    /// <see cref="PersonaEntry.Path"/>, and publishes <see cref="PersonaRenamed"/> once for every
    /// path present in both whose Name differs. A path present only in <paramref name="updated"/> is
    /// a new Persona, and a path present only in <paramref name="previous"/> is a deletion - neither
    /// is a rename. Two files swapping Names is two renames, and falls out of this the same way as
    /// any other pair. Naturally idempotent: a debounced watcher settling on a change already
    /// reflected in <paramref name="previous"/> (the second, ~500&#160;ms-later PersonasChanged a
    /// single save raises - see <see cref="Update"/>) compares two indexes that already agree on
    /// every shared path, so nothing fires the second time.
    /// </summary>
    /// <param name="previous">The index in effect before this refresh.</param>
    /// <param name="updated">The freshly rebuilt index about to become current.</param>
    private void RaiseRenames(PersonaIndex previous, PersonaIndex updated)
    {
        if (this.PersonaRenamed is null || previous.Entries.Count == 0)
        {
            return;
        }

        var updatedByPath = updated.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);

        foreach (var before in previous.Entries)
        {
            if (!updatedByPath.TryGetValue(before.Path, out var after))
            {
                continue;
            }

            if (string.Equals(before.Name, after.Name, StringComparison.Ordinal))
            {
                continue;
            }

            this.PublishRenamed(new PersonaRenamed(before.Name, after.Name));
        }
    }

    /// <summary>
    /// Invokes every <see cref="PersonaRenamed"/> subscriber in turn, logging and skipping one that
    /// throws rather than letting it stop the remaining subscribers or propagate - the same rule
    /// <see cref="Agency.Huddle.App.Services.RoomEvents"/> documents for its own events.
    /// </summary>
    /// <param name="renamed">The old and the new Name to publish.</param>
    private void PublishRenamed(PersonaRenamed renamed)
    {
        if (this.PersonaRenamed is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<PersonaRenamed>)handler).Invoke(renamed);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A PersonaRenamed handler threw for '{OldName}' -> '{NewName}' and was skipped.", renamed.OldName, renamed.NewName);
            }
        }
    }

    /// <summary>
    /// Diffs <paramref name="previous"/> against <paramref name="updated"/>, keyed by
    /// <see cref="PersonaEntry.Path"/> exactly as <see cref="RaiseRenames"/> does, and publishes
    /// <see cref="PersonaRemoved"/> once for every path present in <paramref name="previous"/> with no
    /// matching path in <paramref name="updated"/>. Because a rename keeps its path, a renamed Persona
    /// is never mistaken for a removed one, and there is no ordering hazard between this diff and
    /// <see cref="RaiseRenames"/>'s.
    /// </summary>
    /// <param name="previous">The index in effect before this refresh.</param>
    /// <param name="updated">The freshly rebuilt index about to become current.</param>
    private void RaiseRemovals(PersonaIndex previous, PersonaIndex updated)
    {
        if (this.PersonaRemoved is null || previous.Entries.Count == 0)
        {
            return;
        }

        var updatedPaths = updated.Entries.Select(entry => entry.Path).ToHashSet(StringComparer.Ordinal);

        foreach (var before in previous.Entries)
        {
            if (updatedPaths.Contains(before.Path))
            {
                continue;
            }

            this.PublishRemoved(new PersonaRemoved(before.Name));
        }
    }

    /// <summary>
    /// Invokes every <see cref="PersonaRemoved"/> subscriber in turn, logging and skipping one that
    /// throws rather than letting it stop the remaining subscribers or propagate - the same rule
    /// <see cref="PublishRenamed"/> follows for its own event.
    /// </summary>
    /// <param name="removed">The Name to publish.</param>
    private void PublishRemoved(PersonaRemoved removed)
    {
        if (this.PersonaRemoved is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<PersonaRemoved>)handler).Invoke(removed);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "A PersonaRemoved handler threw for '{Name}' and was skipped.", removed.Name);
            }
        }
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
    // debounce and restarting every online Persona for no reason. Three deliberate exceptions:
    //   - A directory appearing: on Linux, IncludeSubdirectories is emulated - the watcher adds an
    //     inotify watch for a new sub-folder only after it reads that folder's own Created event,
    //     so a Persona file written into the folder in that gap raises nothing at all (mkdir then
    //     write, a copied or unzipped Team folder). The folder's Created event is the one signal
    //     that is guaranteed, and the debounced refresh rescans from disk, so it finds whatever
    //     landed inside. Missing this left the Persona unloaded until some unrelated change
    //     happened to trigger a rescan - under CI load, roughly one new folder in six.
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
    internal static bool AffectsATeamsFile(FileSystemEventArgs e) =>
        IsMarkdownFile(e.Name)
        || (e.ChangeType == WatcherChangeTypes.Created && Directory.Exists(e.FullPath))
        || (e.ChangeType == WatcherChangeTypes.Renamed && Directory.Exists(e.FullPath))
        || (e.ChangeType == WatcherChangeTypes.Deleted && HasNoExtension(e.Name));

    private static bool IsMarkdownFile(string? name) =>
        name is not null && string.Equals(Path.GetExtension(name), ".md", StringComparison.OrdinalIgnoreCase);

    private static bool HasNoExtension(string? name) => name is not null && Path.GetExtension(name).Length == 0;

    /// <summary>
    /// FileSystemWatcher raises this instead of a normal change event when its internal buffer
    /// overflows and the OS drops events - see <see cref="WatcherInternalBufferSize"/>'s comment.
    /// There is no way to know which paths were dropped, so the only correct response is the same
    /// one a normal change takes: schedule a refresh on the existing debounce path rather than trust
    /// whatever the watcher's view of the world now is. <c>internal</c> rather than <c>private</c>
    /// only so <c>Huddle.Tests</c> can invoke it directly after <see cref="Dispose"/> - see
    /// docs/agencyteam/known-limits.md's "Third known flake" entry and
    /// <c>PersonaStoreTests.OnWatcherError_AfterDispose_DoesNotLog</c>.
    /// </summary>
    /// <param name="sender">Unused; required by the <see cref="FileSystemWatcher.Error"/> event shape.</param>
    /// <param name="e">Carries the exception the watcher caught.</param>
    internal void OnWatcherError(object sender, ErrorEventArgs e)
    {
        lock (this.watchGate)
        {
            // Checked BEFORE logging, not after: logging first and checking disposed second meant
            // a watcher Error event that fires during host teardown, after this store (and possibly
            // the logging provider itself) has been disposed, threw an unhandled
            // ObjectDisposedException on the watcher callback thread and crashed the process -
            // docs/agencyteam/known-limits.md's "Third known flake" entry.
            // SkillStore.OnWatcherError already gets this ordering right; this now matches it.
            if (this.disposed)
            {
                return;
            }

            this.logger.LogWarning(
                e.GetException(),
                "PersonaStore's FileSystemWatcher reported an error (likely a dropped-event buffer overflow); scheduling a refresh.");
            this.debounceTimer.Change(WatcherDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        Action? changed;
        PersonaIndex previous;
        PersonaIndex updated;
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
            previous = this.index;
            updated = this.RebuildIndexFromDisk();
            this.index = updated;
            changed = this.PersonasChanged;
        }

        // This is the door a hand-edited file (bypassing Update entirely) reaches - the ONLY place
        // a rename made outside the app is ever detected, which is why RaiseRenames has to run here
        // too, not only from RefreshIndexAndNotify. Still strictly before PersonasChanged.
        this.RaiseRenames(previous, updated);

        // And for exactly the same reason, in the same order as RefreshIndexAndNotify: a .md file
        // DELETED in an editor is detected here and nowhere else. Raising renames here but not
        // removals would leave a hand-deleted Persona's per-Persona state behind with nothing to
        // ever collect it - the silent resurrection rules.md's removal row exists to prevent, since
        // a later Persona reusing that Name would inherit it. Idempotent for the same reason
        // RaiseRenames is: the ~500 ms debounce that follows an in-app Remove compares two indexes
        // that already agree the path is gone, so nothing fires a second time.
        this.RaiseRemovals(previous, updated);
        changed?.Invoke();
    }
}

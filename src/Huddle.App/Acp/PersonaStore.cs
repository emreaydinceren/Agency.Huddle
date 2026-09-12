using Microsoft.Extensions.Options;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// A Persona is a markdown file whose body becomes a Claude agent's system prompt. Files live at
/// <c>{DataDir}/{Acp.PersonaDir}/&lt;name&gt;.md</c>. A leading YAML frontmatter block is
/// optional and unenforced — no required fields, no allowlist — so a user can still add a
/// Persona by dropping in a plain-prose file with none at all; when frontmatter is present, <see
/// cref="PersonaFrontmatter"/> is the only thing that reads it, to compose the job description
/// <c>mcp__team__list_agents</c> shows. A Persona's chosen <see cref="Persona.Model"/> and
/// <see cref="Persona.Effort"/> live separately, in <see cref="PersonaModelStore"/>'s and
/// <see cref="PersonaEffortStore"/>'s SQLite tables — frontmatter is informational, not
/// schema, so it was never a candidate to hold either; <see cref="Get"/> is where the file and
/// the two stored values are joined back into one <see cref="Persona"/>. A Persona name is
/// validated with the same rules as an Agent name, because bringing a Persona online registers an
/// Agent over the pipe.
/// </summary>
public sealed class PersonaStore : IDisposable
{
    // Editors commonly fire several filesystem events per save (a temp-file write plus a rename,
    // or several partial writes), so raising PersonasChanged straight off FileSystemWatcher would
    // thrash every observer (the /teammates page, PersonaSupervisor). Coalesce a burst of events
    // into one PersonasChanged per pause in activity.
    private const int WatcherDebounceMilliseconds = 500;

    private readonly string personaDir;
    private readonly PersonaModelStore models;
    private readonly PersonaEffortStore efforts;
    private readonly Lock watchGate = new();
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private bool disposed;

    public PersonaStore(IOptions<TeamOptions> options, PersonaModelStore models, PersonaEffortStore efforts)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(efforts);

        this.models = models;
        this.efforts = efforts;
        this.personaDir = Path.Combine(options.Value.DataDir, options.Value.Acp.PersonaDir);

        // Created eagerly (rather than lazily on first Add) so the watcher below has a directory
        // to watch from the moment the app starts, even before any Persona has been added.
        Directory.CreateDirectory(this.personaDir);

        this.debounceTimer = new Timer(this.OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
        this.watcher = new FileSystemWatcher(this.personaDir, "*.md")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        this.watcher.Changed += this.OnWatcherEvent;
        this.watcher.Created += this.OnWatcherEvent;
        this.watcher.Deleted += this.OnWatcherEvent;
        this.watcher.Renamed += this.OnWatcherEvent;
        this.watcher.EnableRaisingEvents = true;
    }

    public event Action? PersonasChanged;

    public IReadOnlyList<string> ListNames()
    {
        if (!Directory.Exists(this.personaDir))
        {
            return [];
        }

        var names = Directory.GetFiles(this.personaDir, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return names;
    }

    public Persona? Get(string name)
    {
        if (!NameRules.IsValidAgentName(name))
        {
            return null;
        }

        var path = this.GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        // This one line is the entire file-to-database join, and it happens in exactly one place:
        // both PersonaSupervisor and the razor page already go through Get.
        return new Persona(name, File.ReadAllText(path), this.models.Get(name), this.efforts.Get(name));
    }

    public Persona Add(string name, string text, string? model = null, string? effort = null)
    {
        if (!NameRules.IsValidAgentName(name))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"'{name}' is not a valid Persona name.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ChatException(ErrorCodes.BadMessage, "Persona text must not be blank.");
        }

        var path = this.GetPath(name);
        if (File.Exists(path))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' already exists.");
        }

        Directory.CreateDirectory(this.personaDir);
        File.WriteAllText(path, text);
        this.models.Set(name, model);
        this.efforts.Set(name, effort);

        // Exactly one PersonasChanged per operation: three writes raising three events would cause
        // three restarts (PersonaSupervisor spawning three "node" adapter processes) for what the
        // caller sees as a single save.
        this.PersonasChanged?.Invoke();

        return new Persona(name, text, model, effort);
    }

    /// <summary>
    /// Overwrites an existing Persona's file and its stored model and effort together. Because a
    /// system prompt, a model AND an effort are all fixed at <c>session/new</c>
    /// (docs/acp/agent-guide.md §3.5), the only way for any of these edits to take effect is for the
    /// observer of <see cref="PersonasChanged"/> (<see cref="PersonaSupervisor"/>) to restart that
    /// Persona's session, losing its conversation memory. There is deliberately no default for
    /// <paramref name="model"/> or <paramref name="effort"/>, and no separate "SetModel"/"SetEffort"
    /// method: a defaulted overload of either would let an existing call site silently wipe a stored
    /// value, and a separate single-field setter would raise its own PersonasChanged, causing the
    /// exact multi-restart this method's single event avoids.
    /// </summary>
    public Persona Update(string name, string text, string? model, string? effort)
    {
        if (!NameRules.IsValidAgentName(name))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"'{name}' is not a valid Persona name.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ChatException(ErrorCodes.BadMessage, "Persona text must not be blank.");
        }

        var path = this.GetPath(name);
        if (!File.Exists(path))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' does not exist.");
        }

        File.WriteAllText(path, text);
        this.models.Set(name, model);
        this.efforts.Set(name, effort);

        // Exactly one PersonasChanged per operation: three writes raising three events would cause
        // three restarts (PersonaSupervisor spawning three "node" adapter processes) for what the
        // caller sees as a single save.
        this.PersonasChanged?.Invoke();

        return new Persona(name, text, model, effort);
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
        if (!NameRules.IsValidAgentName(name))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"'{name}' is not a valid Persona name.");
        }

        var path = this.GetPath(name);
        if (!File.Exists(path))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"Persona '{name}' does not exist.");
        }

        File.Delete(path);
        this.models.Remove(name);
        this.efforts.Remove(name);

        // Exactly one PersonasChanged per operation: three writes raising three events would cause
        // three restarts (PersonaSupervisor spawning three "node" adapter processes) for what the
        // caller sees as a single save.
        this.PersonasChanged?.Invoke();
    }

    /// <summary>
    /// The absolute path of a Persona's file, for the "open in default editor" feature. Validates
    /// the name first: this is the path-traversal guard, exactly as for <see cref="Add"/>.
    /// </summary>
    public string PathFor(string name)
    {
        if (!NameRules.IsValidAgentName(name))
        {
            throw new ChatException(ErrorCodes.BadMessage, $"'{name}' is not a valid Persona name.");
        }

        return this.GetPath(name);
    }

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
        this.watcher.Dispose();
        this.debounceTimer.Dispose();
    }

    private string GetPath(string name)
    {
        return Path.Combine(this.personaDir, $"{name}.md");
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
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
        lock (this.watchGate)
        {
            // Checked and invoked under the same lock Dispose() takes, so a Dispose() racing this
            // callback either finishes first (this returns without raising) or this raises before
            // Dispose() can flip the flag — never both.
            if (this.disposed)
            {
                return;
            }

            this.PersonasChanged?.Invoke();
        }
    }
}
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>What is kept about one Room Session so that it can be resumed (RS §6.6).</summary>
/// <param name="SessionId">The Adapter's own id for this Room's session.</param>
/// <param name="AdapterId">The Adapter Profile id the session was opened under; a mismatch against the Persona's current one means the entry is stale.</param>
/// <param name="Model">The model the session was pinned to, or <see langword="null"/> for the Adapter's default.</param>
/// <param name="Effort">The effort level the session was pinned to, or <see langword="null"/> for the model's default.</param>
/// <param name="LastMessageId">The id of the last Message this Room Session's Transcript range should resume after, or <see langword="null"/> before any Turn has completed.</param>
/// <param name="LastTurnUtc">When this Room Session last completed a Turn.</param>
/// <param name="WorkMode">
/// The Work Mode the session was opened in (ADR-0033), or <see langword="null"/> for the Adapter's own.
/// Trailing and optional, so every existing construction is unchanged, and a file written before Work
/// Modes existed has no such key and loads as <see langword="null"/>: it still resumes for a Persona that
/// has none, with no migration.
/// </param>
internal sealed record RoomSessionEntry(
    string SessionId, string AdapterId, string? Model, string? Effort, string? LastMessageId, DateTimeOffset LastTurnUtc, string? WorkMode = null);

/// <summary>
/// Owns <c>{DataDir}/room-sessions/</c>, one JSON file per Agent, keyed by the Agent's Name, per
/// RS §6.6. Mirrors <see cref="Agency.Huddle.App.FileChanges.FileStateStore"/>'s pattern - atomic
/// write, a whole read-modify-write under this store's <see cref="gate"/>, corrupt or missing
/// reads as <see langword="null"/> plus a Warning, never a crash - but is simpler than it in two
/// ways settled for this store specifically: <see cref="RoomSessionEntry"/> already matches the
/// wire shape, so no separate "Document" record is needed for it, and <see cref="Rename"/> only
/// moves the file - it has no other file's keys to rewrite (correction item 20).
/// </summary>
internal sealed class RoomSessionStore(IOptions<TeamOptions> options, ILogger<RoomSessionStore> logger)
{
    /// <summary>
    /// Derived from <see cref="ProtocolJson.Options"/> (correction item 18: that base already sets
    /// <c>DefaultIgnoreCondition = WhenWritingNull</c>, so a <see langword="null"/> <see cref="RoomSessionEntry.Model"/>
    /// or <see cref="RoomSessionEntry.Effort"/> is omitted rather than written <c>null</c>), indented
    /// and with an unsafe-relaxed encoder because this file is hand-editable, matching
    /// <see cref="Agency.Huddle.App.FileChanges.FileStateStore"/>'s own reasoning.
    /// </summary>
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Lock gate = new();

    /// <summary>The folder holding every Agent's file, <c>{DataDir}/room-sessions/</c>. Created on first write.</summary>
    private string Folder => Path.Combine(options.Value.DataDir, "room-sessions");

    /// <summary>Reads <paramref name="roomId"/>'s stored entry for <paramref name="name"/>, or <see langword="null"/> when there is none or the file is corrupt.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="roomId">The Room id.</param>
    internal RoomSessionEntry? Get(string name, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            RoomSessionDocument? document = this.LoadCore(name);
            return document is not null && document.Rooms.TryGetValue(roomId, out RoomSessionEntry? entry) ? entry : null;
        }
    }

    /// <summary>Stores <paramref name="entry"/> for <paramref name="roomId"/>, keeping every other Room's entry for <paramref name="name"/> unchanged.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="roomId">The Room id.</param>
    /// <param name="entry">The entry to store.</param>
    internal void Put(string name, string roomId, RoomSessionEntry entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(entry);

        lock (this.gate)
        {
            RoomSessionDocument document = this.LoadCore(name) ?? RoomSessionStore.EmptyDocument();
            document.Rooms[roomId] = entry;
            this.SaveCore(name, document);
        }
    }

    /// <summary>Removes <paramref name="roomId"/>'s entry for <paramref name="name"/>, if any, keeping the rest.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="roomId">The Room id to forget.</param>
    internal void Forget(string name, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        lock (this.gate)
        {
            RoomSessionDocument? document = this.LoadCore(name);
            if (document is null || !document.Rooms.Remove(roomId))
            {
                return;
            }

            this.SaveCore(name, document);
        }
    }

    /// <summary>Deletes every stored entry for <paramref name="name"/> (Restart, a Persona edit; RS §6.13).</summary>
    /// <param name="name">The Agent's Name.</param>
    internal void ForgetAll(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (this.gate)
        {
            string path = this.PathFor(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Keeps only the entries whose Room id is in <paramref name="liveRoomIds"/>, dropping the rest (RS §6.6: pruned at start against <c>Welcome.Rooms</c>).</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="liveRoomIds">The Room ids the Agent is currently a Member of.</param>
    internal void Prune(string name, IReadOnlyCollection<string> liveRoomIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(liveRoomIds);

        lock (this.gate)
        {
            RoomSessionDocument? document = this.LoadCore(name);
            if (document is null || document.Rooms.Count == 0)
            {
                return;
            }

            HashSet<string> live = new(liveRoomIds, StringComparer.Ordinal);
            List<string> stale = [.. document.Rooms.Keys.Where(roomId => !live.Contains(roomId))];
            if (stale.Count == 0)
            {
                return;
            }

            foreach (string roomId in stale)
            {
                document.Rooms.Remove(roomId);
            }

            this.SaveCore(name, document);
        }
    }

    /// <summary>Moves <paramref name="oldName"/>'s file to <paramref name="newName"/>. Only the file moves - no other file references a Name here to rewrite (correction item 20).</summary>
    /// <param name="oldName">The Agent's current Name.</param>
    /// <param name="newName">The Agent's new Name.</param>
    internal void Rename(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        lock (this.gate)
        {
            string oldPath = this.PathFor(oldName);
            if (!File.Exists(oldPath))
            {
                return;
            }

            Directory.CreateDirectory(this.Folder);
            File.Move(oldPath, this.PathFor(newName), overwrite: true);
        }
    }

    /// <summary>Deletes <paramref name="name"/>'s file entirely (Persona removal; RS §6.13).</summary>
    /// <param name="name">The Agent's Name.</param>
    internal void Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (this.gate)
        {
            string path = this.PathFor(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>The unlocked read side shared by every public member.</summary>
    private RoomSessionDocument? LoadCore(string name)
    {
        string path = this.PathFor(name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RoomSessionDocument>(json, RoomSessionStore.IndentedJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "'{Path}' could not be parsed as Room Session state and was ignored.", path);
            return null;
        }
    }

    /// <summary>The unlocked write side shared by every public member: a temporary file, then an atomic rename over the target.</summary>
    private void SaveCore(string name, RoomSessionDocument document)
    {
        Directory.CreateDirectory(this.Folder);

        string path = this.PathFor(name);
        string tmpPath = path + ".tmp";
        string json = JsonSerializer.Serialize(document, RoomSessionStore.IndentedJsonOptions);
        File.WriteAllText(tmpPath, json);
        File.Move(tmpPath, path, overwrite: true);
    }

    /// <summary>The full path of <paramref name="name"/>'s file, <c>{Folder}/&lt;Name&gt;.json</c>.</summary>
    private string PathFor(string name) => Path.Combine(this.Folder, $"{name}.json");

    /// <summary>A fresh, empty document: Room-id keys keep their case (correction item 19), unlike <see cref="Agency.Huddle.App.FileChanges.FileStateStore"/>'s case-insensitive Name-keyed dictionaries.</summary>
    private static RoomSessionDocument EmptyDocument() => new(new Dictionary<string, RoomSessionEntry>(StringComparer.Ordinal));
}

/// <summary>The JSON shape of one Agent's stored Room Sessions, per RS §6.6.</summary>
/// <param name="Rooms">Keyed by Room id, case preserved (correction item 19).</param>
internal sealed record RoomSessionDocument(Dictionary<string, RoomSessionEntry> Rooms);

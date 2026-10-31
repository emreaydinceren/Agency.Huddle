using Agency.Huddle.App.Library;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Owns <c>{DataDir}/room-sessions/last-catch-up/{Name}/</c>: per Room, <c>{roomId}.md</c> holds the whole
/// prompt text a fresh session's first ordinary Turn sent (correction C3) and <c>{roomId}.json</c> beside it
/// holds the <see cref="FreshStartNote"/> (correction C12), so both survive a restart. A write that hits an IO
/// failure is logged as a Warning and swallowed; a Name or Room id that is not valid is refused.
/// </summary>
internal sealed class LastCatchUpStore(IOptions<TeamOptions> options, ILogger<LastCatchUpStore> logger)
{
    private readonly Lock gate = new();

    /// <summary>The folder holding one sub-folder per Agent Name.</summary>
    private string Root => Path.Combine(options.Value.DataDir, "room-sessions", "last-catch-up");

    /// <summary>Writes <paramref name="text"/> as the reread text for <paramref name="roomId"/> under <paramref name="name"/>, replacing any earlier text.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="roomId">The Room id.</param>
    /// <param name="text">The text, kept byte for byte.</param>
    internal void Write(string name, string roomId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        this.WriteFile(name, roomId, ".md", text);
    }

    /// <summary>Reads the reread text, or <see langword="null"/> when there is none or it cannot be read.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="roomId">The Room id.</param>
    internal string? Read(string name, string roomId)
    {
        string path = this.PathFor(name, roomId, ".md");
        lock (this.gate)
        {
            return this.TryReadText(path);
        }
    }

    /// <summary>Writes <paramref name="note"/> to <c>{note.RoomId}.json</c> under <paramref name="name"/>.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="note">The note to keep.</param>
    internal void WriteNote(string name, FreshStartNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        this.WriteFile(name, note.RoomId, ".json", ProtocolJson.Serialize(note));
    }

    /// <summary>Reads every Name's note for <paramref name="roomId"/>; a corrupt file is ignored with a Warning.</summary>
    /// <param name="roomId">The Room id.</param>
    internal IReadOnlyList<(string Name, FreshStartNote Note)> ReadNotes(string roomId)
    {
        RequireId(roomId);
        List<(string Name, FreshStartNote Note)> notes = [];

        lock (this.gate)
        {
            if (!Directory.Exists(this.Root))
            {
                return notes;
            }

            foreach (string folder in Directory.EnumerateDirectories(this.Root))
            {
                string name = Path.GetFileName(folder);
                string path = Path.Combine(folder, $"{roomId}.json");
                if (NameRules.IsValidAgentName(name) && this.TryReadNote(path) is { } note)
                {
                    notes.Add((name, note));
                }
            }
        }

        return notes;
    }

    /// <summary>Deletes the note only, leaving the reread text.</summary>
    /// <param name="name">The Agent's Name.</param>
    /// <param name="roomId">The Room id.</param>
    internal void RemoveNote(string name, string roomId)
    {
        string path = this.PathFor(name, roomId, ".json");
        lock (this.gate)
        {
            this.TryDelete(path);
        }
    }

    /// <summary>Moves <paramref name="oldName"/>'s folder to <paramref name="newName"/>, replacing any folder already there.</summary>
    /// <param name="oldName">The Agent's current Name.</param>
    /// <param name="newName">The Agent's new Name.</param>
    internal void Rename(string oldName, string newName)
    {
        RequireName(oldName);
        RequireName(newName);
        string from = Path.Combine(this.Root, oldName);
        string to = Path.Combine(this.Root, newName);

        lock (this.gate)
        {
            if (string.Equals(oldName, newName, StringComparison.Ordinal) || !Directory.Exists(from))
            {
                return;
            }

            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                string temp = $"{from}.{Guid.NewGuid():N}.tmp";
                Directory.Move(from, temp);
                Directory.Move(temp, to);
                return;
            }

            if (Directory.Exists(to))
            {
                Directory.Delete(to, recursive: true);
            }

            Directory.Move(from, to);
        }
    }

    /// <summary>Deletes <paramref name="name"/>'s whole folder.</summary>
    /// <param name="name">The Agent's Name.</param>
    internal void Remove(string name)
    {
        RequireName(name);
        string folder = Path.Combine(this.Root, name);

        lock (this.gate)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>Deletes <paramref name="roomId"/>'s <c>.md</c> and <c>.json</c> under every Name, leaving other Rooms' files.</summary>
    /// <param name="roomId">The Room id.</param>
    internal void RemoveRoom(string roomId)
    {
        RequireId(roomId);

        lock (this.gate)
        {
            if (!Directory.Exists(this.Root))
            {
                return;
            }

            foreach (string folder in Directory.EnumerateDirectories(this.Root))
            {
                this.TryDelete(Path.Combine(folder, $"{roomId}.md"));
                this.TryDelete(Path.Combine(folder, $"{roomId}.json"));
            }
        }
    }

    private static void RequireName(string name)
    {
        if (!NameRules.IsValidAgentName(name))
        {
            throw new ArgumentException("Not a valid Agent Name.", nameof(name));
        }
    }

    private static void RequireId(string roomId)
    {
        if (!NameRules.IsValidId(roomId))
        {
            throw new ArgumentException("Not a valid Room id.", nameof(roomId));
        }
    }

    private string PathFor(string name, string roomId, string extension)
    {
        RequireName(name);
        RequireId(roomId);
        return Path.Combine(this.Root, name, roomId + extension);
    }

    private void WriteFile(string name, string roomId, string extension, string text)
    {
        string path = this.PathFor(name, roomId, extension);

        lock (this.gate)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(this.Root, name));
                AtomicFile.WriteText(path, text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "'{Path}' could not be written; the catch-up record was not kept.", path);
            }
        }
    }

    private string? TryReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "'{Path}' could not be read and was ignored.", path);
            return null;
        }
    }

    private FreshStartNote? TryReadNote(string path)
    {
        string? json = this.TryReadText(path);
        if (json is null)
        {
            return null;
        }

        try
        {
            if (ProtocolJson.Deserialize(json) is FreshStartNote note)
            {
                return note;
            }

            logger.LogWarning("'{Path}' does not hold a Room note and was ignored.", path);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "'{Path}' could not be parsed as a Room note and was ignored.", path);
        }

        return null;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "'{Path}' could not be deleted.", path);
        }
    }
}

using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Pipes;

/// <summary>
/// Holds the latest <see cref="FreshStartNote"/> per Room and Agent (corrections C4, C12). Every change is
/// written through <see cref="LastCatchUpStore"/>, so a note survives an app restart; a Room is loaded from
/// the store on its first read and cached after that.
/// </summary>
internal sealed class FreshStartNotes(LastCatchUpStore store)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, Dictionary<string, FreshStartNote>> byRoom = new(StringComparer.Ordinal);

    /// <summary>Keeps <paramref name="note"/> as the latest note of <paramref name="agentName"/> in <paramref name="roomId"/>, replacing any earlier one.</summary>
    /// <param name="roomId">The Room id.</param>
    /// <param name="agentName">The Agent's Name.</param>
    /// <param name="note">The note.</param>
    internal void Set(string roomId, string agentName, FreshStartNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        lock (this.gate)
        {
            Dictionary<string, FreshStartNote> room = this.LoadRoom(roomId);
            store.WriteNote(agentName, note);
            room[agentName] = note;
        }
    }

    /// <summary>The latest note of every Agent in <paramref name="roomId"/>.</summary>
    /// <param name="roomId">The Room id.</param>
    internal IReadOnlyList<(string AgentName, FreshStartNote Note)> For(string roomId)
    {
        lock (this.gate)
        {
            return [.. this.LoadRoom(roomId).Select(static pair => (pair.Key, pair.Value))];
        }
    }

    /// <summary>Forgets the note of <paramref name="agentName"/> in <paramref name="roomId"/>; the reread text stays.</summary>
    /// <param name="roomId">The Room id.</param>
    /// <param name="agentName">The Agent's Name.</param>
    internal void Dismiss(string roomId, string agentName)
    {
        lock (this.gate)
        {
            Dictionary<string, FreshStartNote> room = this.LoadRoom(roomId);
            store.RemoveNote(agentName, roomId);
            room.Remove(agentName);
        }
    }

    private Dictionary<string, FreshStartNote> LoadRoom(string roomId)
    {
        if (!this.byRoom.TryGetValue(roomId, out Dictionary<string, FreshStartNote>? room))
        {
            room = new(StringComparer.Ordinal);
            foreach ((string name, FreshStartNote note) in store.ReadNotes(roomId))
            {
                room[name] = note;
            }

            this.byRoom[roomId] = room;
        }

        return room;
    }
}

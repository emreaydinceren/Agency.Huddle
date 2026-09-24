using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public interface ITeamDirectory
{
    Task InitializeAsync(string humanName, CancellationToken ct = default);

    Task<User> GetHumanAsync(CancellationToken ct = default);

    Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default);

    /// <summary>
    /// Renames the <see cref="User"/> row with id <paramref name="userId"/> to
    /// <paramref name="newName"/>, keeping its id unchanged so every Room, <c>room_members</c> row
    /// and Transcript that already reference that id continue to reference the same Teammate under
    /// its new Name.
    /// </summary>
    /// <param name="userId">The id of the row to rename.</param>
    /// <param name="newName">
    /// The new Name. Must satisfy <see cref="NameRules.IsValidAgentName(string?)"/>, and must not
    /// already belong to a different Teammate — Names are <c>COLLATE NOCASE UNIQUE</c>, so this is
    /// checked case-insensitively.
    /// </param>
    /// <returns><see langword="true"/> if the row was renamed; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Synchronous, because its only caller is <see cref="Agency.Huddle.App.Acp.PersonaRenameCascade"/>,
    /// reacting to <see cref="Agency.Huddle.App.Acp.PersonaStore.PersonaRenamed"/> — a plain
    /// <c>Action&lt;T&gt;</c> event that cannot be awaited. This is the one Team Directory write that
    /// MUST complete before <see cref="Agency.Huddle.App.Acp.PersonaStore.PersonasChanged"/> fires and
    /// <c>PersonaSupervisor</c> starts a runner under the new Name; an async handler could not
    /// guarantee that ordering, and without it the restarted runner's <c>hello</c> mints a brand-new
    /// user id instead of renaming this one (see ADR-0011). <see cref="Agency.Huddle.App.Data.PersonaModelStore"/>
    /// and <see cref="Agency.Huddle.App.Data.PersonaEffortStore"/> are the existing precedent for
    /// synchronous access to this same database.
    /// <para>
    /// Returns <see langword="false"/> rather than throwing because the caller is a
    /// file-watcher-driven Persona rename: an exception raised from inside that callback would
    /// either be swallowed silently or kill the debounce timer that invoked it, with nothing
    /// surfaced to a person. <c>userId</c> must name a row whose <c>kind</c> is <c>'agent'</c> — the
    /// Human is never a rename target, because a Persona renamed to the Human's Name could otherwise
    /// take over the Human's row and its Rooms.
    /// </para>
    /// </remarks>
    bool RenameUser(string userId, string newName);

    Task<User?> GetUserAsync(string id, CancellationToken ct = default);

    Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Finds the <see cref="User"/> row with the given Name, case-insensitively — the synchronous
    /// twin of <see cref="FindUserByNameAsync"/>.
    /// </summary>
    /// <param name="name">The Name to look up.</param>
    /// <returns>The matching <see cref="User"/>, or <see langword="null"/> if none exists.</returns>
    /// <remarks>
    /// Synchronous for the same reason <see cref="RenameUser"/> is: <see cref="Agency.Huddle.App.Acp.PersonaRenameCascade"/>
    /// must find the Agent to rename from inside a plain <c>Action&lt;T&gt;</c> event handler, which
    /// cannot await, rather than blocking on the async version. <see cref="Agency.Huddle.App.Data.PersonaModelStore"/>
    /// and <see cref="Agency.Huddle.App.Data.PersonaEffortStore"/> are the existing precedent for
    /// synchronous access to this same database.
    /// </remarks>
    User? FindUserByName(string name);

    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Room>> GetRoomsForUserAsync(string userId, CancellationToken ct = default);

    Task<Room?> GetRoomAsync(string roomId, CancellationToken ct = default);

    Task<IReadOnlyList<User>> GetRoomMembersAsync(string roomId, CancellationToken ct = default);

    Task<Room> CreateRoomAsync(string name, IEnumerable<string> memberIds, CancellationToken ct = default);

    Task<bool> AddMemberAsync(string roomId, string userId, CancellationToken ct = default);

    Task RenameRoomAsync(string roomId, string name, CancellationToken ct = default);

    /// <summary>
    /// Finds the Room whose Members are exactly the Human and this Agent, if one exists. A Room's
    /// behaviour follows from its member count, so this is a membership query and never a lookup
    /// of a stored room kind.
    /// </summary>
    Task<Room?> FindRoomWithExactMembersAsync(string humanId, string agentId, CancellationToken ct = default);

    /// <summary>The oldest non-Archived Room whose Members are exactly <paramref name="memberIds"/>.</summary>
    /// <param name="memberIds">
    /// The exact Member set to match. Duplicate ids are treated as one set member each. An empty
    /// collection always yields <see langword="null"/>.
    /// </param>
    /// <param name="ct">Cancels the read.</param>
    Task<Room?> FindRoomWithExactMemberSetAsync(IReadOnlyCollection<string> memberIds, CancellationToken ct = default);

    /// <summary>
    /// Archives or unarchives a Room. Archiving is a display filter only - see
    /// <see cref="Room.Archived"/> - the Room stays live and Agents can still post into it.
    /// </summary>
    /// <param name="roomId">The Room to archive or unarchive.</param>
    /// <param name="archived">
    /// <see langword="true"/> to archive the Room; <see langword="false"/> to unarchive it.
    /// </param>
    /// <param name="ct">Cancels the write.</param>
    Task SetRoomArchivedAsync(string roomId, bool archived, CancellationToken ct = default);

    /// <summary>
    /// Permanently deletes a Room and its Membership rows. This does not delete the Room's Transcript,
    /// which lives outside the Team Directory in <see cref="IChatStore"/> - a caller that wants a Room
    /// fully gone must also call <see cref="IChatStore.DeleteAsync"/>, or an orphan transcript file is
    /// left behind.
    /// </summary>
    /// <param name="roomId">The Room to delete.</param>
    /// <param name="ct">Cancels the deletion.</param>
    /// <remarks>Deleting an unknown id is a silent no-op, matching <see cref="RenameRoomAsync"/>.</remarks>
    Task DeleteRoomAsync(string roomId, CancellationToken ct = default);
}
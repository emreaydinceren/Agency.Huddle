namespace Agency.Huddle.App.Data;

public interface ITeamDirectory
{
    Task InitializeAsync(string humanName, CancellationToken ct = default);

    Task<User> GetHumanAsync(CancellationToken ct = default);

    Task<User?> UpsertAgentUserAsync(string name, string? description, CancellationToken ct = default);

    Task<User?> GetUserAsync(string id, CancellationToken ct = default);

    Task<User?> FindUserByNameAsync(string name, CancellationToken ct = default);

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
}
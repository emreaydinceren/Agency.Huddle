using System.Reflection;
using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// An <see cref="ITeamDirectory"/> for sidebar component tests that only read Rooms: it answers
/// <c>GetRoomsAsync</c>, <c>GetUsersAsync</c> and <c>GetMembersByRoomAsync</c> from lists the test
/// sets, and refuses every other member, so a component that starts reading something else (a
/// per-Team query, or a per-Room <c>GetRoomMembersAsync</c> from each row) fails loudly.
/// It exists because the real directory needs an awaited <c>InitializeAsync</c> that a synchronous
/// bUnit context builder cannot run.
/// </summary>
public class ScriptedRoomsDirectory : DispatchProxy
{
    private int roomReads;
    private int memberReads;

    /// <summary>Every Room the fake knows, scoped or not, in creation order.</summary>
    public List<Room> Rooms { get; } = [];

    /// <summary>Each Room's Members by Room id; a Room with no entry has none.</summary>
    public Dictionary<string, IReadOnlyList<User>> Members { get; } = new(StringComparer.Ordinal);

    /// <summary>How many times <c>GetMembersByRoomAsync</c> has been called.</summary>
    public int MemberReads => Volatile.Read(ref this.memberReads);

    /// <summary>Gates for the next <c>GetRoomsAsync</c> calls, one per call in order: a call that takes a gate answers with the Rooms as they were when it was made, but only once the gate is completed.</summary>
    public Queue<TaskCompletionSource> Gates { get; } = new();

    /// <summary>How many times <c>GetRoomsAsync</c> has been called.</summary>
    public int RoomReads => Volatile.Read(ref this.roomReads);

    /// <summary>Creates the proxy and returns it with the scripting handle.</summary>
    /// <param name="script">The handle the test uses to set the Rooms the proxy answers with.</param>
    /// <returns>The directory to register.</returns>
    public static ITeamDirectory Create(out ScriptedRoomsDirectory script)
    {
        ITeamDirectory proxy = Create<ITeamDirectory, ScriptedRoomsDirectory>();
        script = (ScriptedRoomsDirectory)(object)proxy;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        switch (targetMethod.Name)
        {
            case nameof(ITeamDirectory.GetRoomsAsync):
                _ = Interlocked.Increment(ref this.roomReads);
                IReadOnlyList<Room> snapshot = [.. this.Rooms];
                return this.Gates.TryDequeue(out TaskCompletionSource? gate)
                    ? Answer(gate, snapshot)
                    : Task.FromResult(snapshot);
            case nameof(ITeamDirectory.GetUsersAsync):
                IReadOnlyList<User> none = [];
                return Task.FromResult(none);
            case nameof(ITeamDirectory.GetMembersByRoomAsync):
                _ = Interlocked.Increment(ref this.memberReads);
                IReadOnlyDictionary<string, IReadOnlyList<User>> members = new Dictionary<string, IReadOnlyList<User>>(this.Members, StringComparer.Ordinal);
                return Task.FromResult(members);
            default:
                throw new NotSupportedException($"ScriptedRoomsDirectory does not answer {targetMethod.Name}.");
        }
    }

    /// <summary>Waits for <paramref name="gate"/> and then answers with <paramref name="snapshot"/>.</summary>
    /// <param name="gate">Completed by the test to release the read.</param>
    /// <param name="snapshot">The Rooms as they were when the read began.</param>
    private static async Task<IReadOnlyList<Room>> Answer(TaskCompletionSource gate, IReadOnlyList<Room> snapshot)
    {
        await gate.Task;
        return snapshot;
    }
}

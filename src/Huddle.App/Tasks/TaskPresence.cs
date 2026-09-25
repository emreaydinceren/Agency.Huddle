using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Tasks;

/// <summary>The Tasks UI's presence badge for an assignee, per Spec §10.8.</summary>
public enum PresenceState
{
    /// <summary>The assignee has a Turn running right now.</summary>
    Awake,

    /// <summary>The assignee is online but has no Turn running.</summary>
    Asleep,

    /// <summary>The assignee is not online.</summary>
    Offline,
}

/// <summary>Resolves the Tasks UI's presence badge from an Agent's online and busy state.</summary>
internal static class TaskPresence
{
    /// <summary>
    /// Resolves the presence badge for one Agent.
    /// </summary>
    /// <param name="online">
    /// Whether the Agent counts as online: the caller computes this as
    /// <see cref="Acp.PersonaStatusResolver.Resolve(bool, Acp.PersonaStatus?)"/> returning
    /// <see cref="Acp.PersonaState.Online"/> or <see cref="Acp.PersonaState.Degraded"/> — a
    /// <see cref="Acp.PersonaState.Starting"/> Agent counts as Offline here, per corrections-B3 D8.
    /// </param>
    /// <param name="busy">Whether the Agent has a Turn running right now.</param>
    /// <returns><see cref="PresenceState.Offline"/> unless <paramref name="online"/>, then <see cref="PresenceState.Awake"/> or <see cref="PresenceState.Asleep"/> from <paramref name="busy"/>.</returns>
    internal static PresenceState Resolve(bool online, bool busy)
    {
        if (!online)
        {
            return PresenceState.Offline;
        }

        return busy ? PresenceState.Awake : PresenceState.Asleep;
    }

    /// <summary>
    /// One presence lookup by Name, shared by the Board, <c>TaskDetail</c> and <c>TaskTriggerService.Preview</c>
    /// (corrections-B5): resolves the Teammate, its online state and whether it has a Turn running, then
    /// calls <see cref="Resolve(bool, bool)"/>.
    /// </summary>
    /// <param name="name">The assignee's Name.</param>
    /// <param name="directory">Looks up the Name's <see cref="User"/> row.</param>
    /// <param name="gateway">Reports whether the Agent's pipe connection is live.</param>
    /// <param name="health">Reports the Agent's latest health, if any has ever been reported.</param>
    /// <param name="turns">Reports whether the Agent has a Turn running, in any Room.</param>
    /// <returns>
    /// <see langword="null"/> when <paramref name="name"/> does not belong to a known Agent (an unknown
    /// Name, or the Human's own Name); otherwise the resolved <see cref="PresenceState"/>.
    /// </returns>
    internal static PresenceState? For(string name, ITeamDirectory directory, IAgentGateway gateway, PersonaHealth health, TurnActivity turns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(turns);

        User? user = directory.FindUserByName(name);
        if (user is not { Kind: UserKind.Agent })
        {
            return null;
        }

        PersonaStatus resolved = PersonaStatusResolver.Resolve(gateway.IsOnline(user.Id), health.Get(name));
        bool online = resolved.State is PersonaState.Online or PersonaState.Degraded;
        bool busy = turns.IsBusy(user.Id);
        return TaskPresence.Resolve(online, busy);
    }
}

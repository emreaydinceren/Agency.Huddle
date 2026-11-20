using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>
/// Smart wakes spec 5.3: wakes each Agent a batch's comments Mention, by name and nothing else (the assignee and the manager are not woken by a
/// comment that does not Mention them). It drops the actor (compared ignoring case), the Human and any name that is no known Persona, keeps the
/// round's first-mention order, and wakes nobody when wake-ups are off. The wake goes through the service's delivery, so it shares the Room choice,
/// the per-Task wake budget and the held wakes of the other rules.
/// </summary>
/// <param name="human">The Human's live identity, who is never woken.</param>
/// <param name="personas">Resolves whether a Mentioned name is a known Persona.</param>
/// <param name="options">Read live for <c>Tasks.Enabled</c> and <c>Tasks.WakeEnabled</c>.</param>
internal sealed class MentionWakeRule(IHumanIdentity human, PersonaStore personas, TeamOptions options) : ITaskWakeRule
{
    /// <inheritdoc />
    public string Name => "Mention";

    /// <inheritdoc />
    public TaskWakeChoice Choose(TaskWakeRound round)
    {
        ArgumentNullException.ThrowIfNull(round);

        if (round.Mentioned is not { Count: > 0 } mentioned || !options.Tasks.Enabled || !options.Tasks.WakeEnabled)
        {
            return new TaskWakeChoice.Skip(null);
        }

        string actor = GoldfishStamp.AgentName(round.Actor.Name);
        List<string> names = [.. mentioned.Where(name =>
            !string.Equals(name, actor, StringComparison.OrdinalIgnoreCase)
            && !human.Matches(name)
            && personas.Get(name) is not null)];

        return names.Count == 0 ? new TaskWakeChoice.Skip(null) : new TaskWakeChoice.WakeEach(names);
    }
}

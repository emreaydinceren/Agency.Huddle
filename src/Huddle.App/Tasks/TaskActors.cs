using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;

namespace Agency.Huddle.App.Tasks;

/// <summary>Builds the <see cref="TaskActor"/> for the Human, the one actor <see cref="Acp.PersonaStore"/> knows nothing about (Settled corrections-B5 "Upstream additions" D6).</summary>
internal static class TaskActors
{
    /// <summary>The Human actor, built from configuration rather than resolved through a Persona lookup.</summary>
    /// <param name="options">Supplies <see cref="TeamOptions.HumanName"/>.</param>
    public static TaskActor Human(TeamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TaskActor(TaskActorKind.Human, options.HumanName, KnownIds.Human);
    }

    /// <summary>The Human actor under the Human's current Name.</summary>
    /// <param name="human">The Human's live identity.</param>
    public static TaskActor Human(IHumanIdentity human)
    {
        ArgumentNullException.ThrowIfNull(human);
        return new TaskActor(TaskActorKind.Human, human.Name, KnownIds.Human);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Wake;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Tasks.Wake;

/// <summary>
/// The collaborators a wake rule reads, built by hand: the Human ("You"), a Persona store, a hand-driven Team catalog, a
/// scripted options. The rule tests build each rule from these and call <c>Choose</c> directly.
/// </summary>
internal sealed class WakeRuleKit : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <summary>Builds the kit with Tasks and wake-ups on, no Teams, and no Personas.</summary>
    public WakeRuleKit()
    {
        this.Personas = new PersonaStore(
            new AgentPaths(this.dir.Options()),
            new PersonaModelStore(this.dir.Options()),
            new PersonaEffortStore(this.dir.Options()),
            new PersonaWorkModeStore(this.dir.Options()),
            NullLogger<PersonaStore>.Instance);
    }

    /// <summary>The options the rules read <c>Tasks.Enabled</c> and <c>Tasks.WakeEnabled</c> from.</summary>
    public TeamOptions Options { get; } = new() { HumanName = "You" };

    /// <summary>The Human, whose Name is "You".</summary>
    public IHumanIdentity Human { get; } = new ConfiguredHumanIdentity("You");

    /// <summary>The Persona store the rules ask whether a Name is a known Persona.</summary>
    public PersonaStore Personas { get; }

    /// <summary>The Team catalog the rules read a Team's manager and members from.</summary>
    public FakeTeamCatalog Teams { get; } = new();

    /// <summary>Registers each name as a Persona.</summary>
    /// <param name="names">The Persona names.</param>
    public void AddPersonas(params string[] names)
    {
        foreach (string name in names)
        {
            _ = this.Personas.Add(new PersonaIdentity(name, name, name.ToUpperInvariant(), ["Platform"]), $"You are {name}.");
        }
    }

    /// <summary>Replaces the catalog with Teams <paramref name="teams"/>.</summary>
    /// <param name="teams">The Teams: name, manager (or <see langword="null"/>) and members.</param>
    public void SetTeams(params (string Name, string? Manager, string[] Members)[] teams) =>
        this.Teams.Teams = [.. teams.Select(static t => new TeamSummary(t.Name, [], t.Members, true) { Manager = t.Manager })];

    /// <summary>Builds the manager rule over this kit's collaborators.</summary>
    /// <param name="withCatalog">Whether to hand the rule the catalog; <see langword="false"/> hands it none.</param>
    /// <returns>The rule.</returns>
    public ManagerWakeRule ManagerRule(bool withCatalog = true) =>
        new(withCatalog ? this.Teams : null, this.Human, this.Personas, this.Options);

    /// <summary>Builds the requesting-manager rule over this kit's collaborators.</summary>
    /// <returns>The rule.</returns>
    public RequestingManagerWakeRule RequestingManagerRule() =>
        new(this.Teams, this.Human, this.Personas, this.Options);

    /// <summary>Builds the Mention rule over this kit's collaborators.</summary>
    /// <returns>The rule.</returns>
    public MentionWakeRule MentionRule() =>
        new(this.Human, this.Personas, this.Options);

    /// <summary>A round whose batch's comments Mention <paramref name="mentioned"/>, made by an Agent called <paramref name="actorName"/>.</summary>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="actorName">Who made the change.</param>
    /// <param name="mentioned">The Agent names the comments Mention, in first-mention order.</param>
    /// <returns>The round.</returns>
    public static TaskWakeRound MentionRound(TaskItem latest, string actorName, params string[] mentioned) =>
        new(latest, new TaskActor(TaskActorKind.Agent, actorName, null), false, new WakePreview(null, null, WakeBlock.None), false, false, mentioned);

    /// <summary>A round for the rule under test; the assignee preview is a clear one for nobody, which only the assignee rule reads.</summary>
    /// <param name="latest">The Task's latest state.</param>
    /// <param name="actorName">Who made the change.</param>
    /// <param name="statusChanged">Whether the batch changed the status.</param>
    /// <returns>The round.</returns>
    public static TaskWakeRound Round(TaskItem latest, string actorName = "You", bool statusChanged = false) =>
        new(latest, new TaskActor(TaskActorKind.Agent, actorName, null), statusChanged, new WakePreview(null, null, WakeBlock.None));

    /// <summary>Disposes the Persona store and the temporary data directory.</summary>
    public void Dispose()
    {
        this.Personas.Dispose();
        this.dir.Dispose();
    }
}

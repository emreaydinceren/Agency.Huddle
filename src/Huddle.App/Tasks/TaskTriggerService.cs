using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Services;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// What saving a Task change would do to its assignee (Spec §10.1): the Name it would notify, that
/// Name's presence if the wake would actually happen, and which guard - if any - stops it. Every UI
/// label about who will be woken (a Save button, a drag hint, a banner) reads this record rather than
/// re-deriving the guards in Spec §10.2 itself, so the interface can never disagree with
/// <see cref="TaskTriggerService.Preview"/>.
/// </summary>
/// <param name="AssigneeName">The Task's assignee Name (<c>After.Assignee</c>), or <see langword="null"/> when the Task has none.</param>
/// <param name="Presence">The assignee's presence badge, resolved only when <paramref name="Block"/> is <see cref="WakeBlock.None"/>; otherwise <see langword="null"/>.</param>
/// <param name="Block">Which guard (Spec §10.2) stops the wake, or <see cref="WakeBlock.None"/> when none does.</param>
public sealed record WakePreview(string? AssigneeName, PresenceState? Presence, WakeBlock Block);

/// <summary>Which Spec §10.2 guard, if any, stops a Task change from waking its assignee.</summary>
public enum WakeBlock
{
    /// <summary>No guard applies: the change would wake the assignee.</summary>
    None,

    /// <summary>The Task has no assignee, or its assignee isn't a known Persona (guards 2 and 5).</summary>
    NoAssignee,

    /// <summary>The assignee is the Human (guard 3).</summary>
    AssigneeIsHuman,

    /// <summary>The assignee made the change themselves (guard 4, the self-edit guard).</summary>
    AssigneeIsActor,

    /// <summary>The change was made by an Agent, and the Task's wake budget (Spec §10.6) is spent (guard 6).</summary>
    BudgetPaused,

    /// <summary>Tasks, or wake-ups specifically, are turned off (guard 1).</summary>
    Disabled,
}

/// <summary>
/// Wakes a Task's assignee when the Task changes (Spec §10, ADR-0026): subscribes to
/// <see cref="TaskEvents.TaskChanged"/>, coalesces same-actor changes to the same Task, and posts a
/// Message through <see cref="ChatService"/> that Mentions the assignee. This task (9.3) implements
/// only <see cref="Preview"/>, the pure guard evaluation Spec §10.1-§10.2 describes; the coalescing
/// timer, Room choice and posting that <see cref="StartAsync"/> will drive arrive in Task 9.4, which
/// is why <see cref="StartAsync"/>, <see cref="StopAsync"/> and <see cref="Dispose"/> are no-ops for
/// now rather than left unimplemented.
/// </summary>
internal sealed partial class TaskTriggerService : IHostedService, IDisposable
{
    private readonly TaskActivity activity;
    private readonly TurnActivity turns;
    private readonly ITeamDirectory directory;
    private readonly PersonaStore personas;
    private readonly IAgentGateway gateway;
    private readonly PersonaHealth health;
    private readonly TeamOptions teamOptions;

    /// <summary>
    /// Creates the service. <paramref name="events"/>, <paramref name="store"/>,
    /// <paramref name="chat"/>, <paramref name="prompts"/>, <paramref name="clock"/> and
    /// <paramref name="logger"/> are validated but not yet stored - Task 9.4 wires them into the
    /// coalescing and posting this task does not implement, per the class remarks.
    /// </summary>
    /// <param name="events">Raises <see cref="TaskEvents.TaskChanged"/>; Task 9.4 subscribes to it in <see cref="StartAsync"/>.</param>
    /// <param name="store">Reads a Task's latest state when a coalesced batch fires (Task 9.4).</param>
    /// <param name="activity">The per-Task wake budget (Spec §10.6) <see cref="Preview"/> checks for guard 6.</param>
    /// <param name="turns">Reports whether the assignee has a Turn running, for <see cref="Preview"/>'s presence.</param>
    /// <param name="chat">Posts the wake-up Message (Task 9.4).</param>
    /// <param name="directory">Resolves the assignee's <see cref="User"/> row, for <see cref="Preview"/>'s presence and Task 9.4's Room choice.</param>
    /// <param name="personas">Resolves whether the assignee is still a known Persona (guard 5).</param>
    /// <param name="gateway">Reports whether the assignee's pipe connection is live, for <see cref="Preview"/>'s presence.</param>
    /// <param name="health">Reports the assignee's latest health, for <see cref="Preview"/>'s presence.</param>
    /// <param name="prompts">Renders the <c>task.wake.message</c> Prompt (Task 9.4).</param>
    /// <param name="options">Supplies <see cref="TasksOptions"/> (guards 1 and 6) and <see cref="TeamOptions.HumanName"/> (guard 3).</param>
    /// <param name="clock">Drives the coalescing timer (Task 9.4).</param>
    /// <param name="logger">Records a failed wake attempt (Task 9.4).</param>
    public TaskTriggerService(
        TaskEvents events,
        TaskStore store,
        TaskActivity activity,
        TurnActivity turns,
        ChatService chat,
        ITeamDirectory directory,
        PersonaStore personas,
        IAgentGateway gateway,
        PersonaHealth health,
        IPromptSource prompts,
        IOptions<TeamOptions> options,
        TimeProvider clock,
        ILogger<TaskTriggerService> logger)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(turns);
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        this.activity = activity;
        this.turns = turns;
        this.directory = directory;
        this.personas = personas;
        this.gateway = gateway;
        this.health = health;
        this.teamOptions = options.Value;
    }

    /// <summary>
    /// What saving <paramref name="after"/> would do to its assignee (Spec §10.1-§10.2), applying
    /// every guard without posting anything or touching the wake budget. Pure over its inputs and
    /// this service's injected collaborators.
    /// </summary>
    /// <param name="before">The Task before the change, or <see langword="null"/> for a Create. Unused by the guards themselves - Spec §10.2 considers only <paramref name="after"/>'s assignee, so a reassignment never tells the previous assignee.</param>
    /// <param name="after">The Task as it would be saved.</param>
    /// <param name="actor">Who made the change.</param>
    /// <returns>The resolved <see cref="WakePreview"/>.</returns>
    public WakePreview Preview(TaskItem? before, TaskItem after, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(actor);
        _ = before;

        string? assignee = after.Assignee;

        if (!this.teamOptions.Tasks.Enabled || !this.teamOptions.Tasks.WakeEnabled)
        {
            return new WakePreview(assignee, null, WakeBlock.Disabled);
        }

        if (string.IsNullOrEmpty(assignee))
        {
            return new WakePreview(assignee, null, WakeBlock.NoAssignee);
        }

        if (string.Equals(assignee, this.teamOptions.HumanName, StringComparison.OrdinalIgnoreCase))
        {
            return new WakePreview(assignee, null, WakeBlock.AssigneeIsHuman);
        }

        if (string.Equals(assignee, actor.Name, StringComparison.OrdinalIgnoreCase))
        {
            return new WakePreview(assignee, null, WakeBlock.AssigneeIsActor);
        }

        if (personas.Get(assignee) is null)
        {
            return new WakePreview(assignee, null, WakeBlock.NoAssignee);
        }

        if (actor.Kind == TaskActorKind.Agent && activity.Budget(after.Id).Exhausted)
        {
            return new WakePreview(assignee, null, WakeBlock.BudgetPaused);
        }

        PresenceState? presence = TaskPresence.For(assignee, directory, gateway, health, turns);
        return new WakePreview(assignee, presence, WakeBlock.None);
    }

    /// <summary>
    /// A no-op until Task 9.4, which subscribes to <see cref="TaskEvents.TaskChanged"/> here (Spec
    /// §10.1: "subscribes ... in <c>StartAsync</c>").
    /// </summary>
    /// <param name="cancellationToken">Unused for now.</param>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// A no-op until Task 9.4, which unsubscribes from <see cref="TaskEvents.TaskChanged"/> and
    /// awaits any in-flight coalesced fire here.
    /// </summary>
    /// <param name="cancellationToken">Unused for now.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>A no-op until Task 9.4, which disposes the per-batch coalescing timers here.</summary>
    public void Dispose()
    {
    }
}

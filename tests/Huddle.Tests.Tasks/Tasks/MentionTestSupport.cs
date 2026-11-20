using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Teams;
using static Agency.Huddle.Tests.Tasks.TaskTriggerTestSupport;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// What the Mention tests (smart wakes spec 7, SW-T4) arrange: a third Agent Rex with the Alias <c>rexy</c>, a Platform Team managed by Rex, comment and
/// status-move changes, and the expected comment bullet. The usual assignee is Nova, the usual actor Kai.
/// </summary>
internal static class MentionTestSupport
{
    /// <summary>The prompt template the tests install: the woken Name, the actor and the change bullets, one Message per line group.</summary>
    internal const string Probe = "{{assignee}}|{{actor}}|{{changes}}";

    /// <summary>The marker a comment cut at 1,000 characters ends with, inside the closing quote.</summary>
    internal const string CutMarker = "… (get_task has the rest)";

    /// <summary>A catalog whose Platform Team names Rex as manager, so a test can show that a manager who is not Mentioned is not woken.</summary>
    internal static FakeTeamCatalog RexManagerCatalog() =>
        new() { Teams = [new TeamSummary("Platform", [], ["Nova", "Kai", "Rex"], true) { Manager = "Rex" }] };

    /// <summary>Registers Rex (Alias <c>rexy</c>) as a Persona and Agent user, builds the Room {Human, Nova, Kai, Rex} and installs <see cref="Probe"/>.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="cast">Nova, Kai and their Room.</param>
    /// <param name="ct">Cancels the directory writes.</param>
    /// <returns>The Room every wake is posted in.</returns>
    internal static async Task<Room> AddRexAsync(Harness harness, Cast cast, CancellationToken ct)
    {
        _ = harness.Personas.Add(new PersonaIdentity("Rex", "Rex", "rexy", ["Platform"]), "You are Rex.");
        User? rex = await harness.Directory.UpsertAgentUserAsync("Rex", null, ct);
        Assert.NotNull(rex);
        harness.Prompts.SetOverride(WakeMessageKey, Probe);
        return await harness.Directory.CreateRoomAsync("Everyone", [KnownIds.Human, cast.Nova.Id, cast.Kai.Id, rex.Id], ct);
    }

    /// <summary>Registers an Agent user called Ghost with no Persona behind it: a directory row that is no registered Persona.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="ct">Cancels the directory writes.</param>
    /// <returns>A task that completes when the user exists.</returns>
    internal static async Task AddGhostUserAsync(Harness harness, CancellationToken ct)
    {
        User? ghost = await harness.Directory.UpsertAgentUserAsync("Ghost", null, ct);
        Assert.NotNull(ghost);
    }

    /// <summary>A comment on <paramref name="task"/>: kind Comment, no fields, the Change log summary "comment added by X" the Feed reads.</summary>
    /// <param name="harness">The harness whose clock stamps the entry.</param>
    /// <param name="task">The Task.</param>
    /// <param name="actor">Who commented.</param>
    /// <param name="text">The comment text.</param>
    /// <returns>The change.</returns>
    internal static TaskChange Comment(Harness harness, TaskItem task, TaskActor actor, string text) =>
        new(task, task, [], actor, new ChangeLogEntry(harness.Clock.GetUtcNow(), actor.Name, $"comment added by {actor.Name}"), Kind: TaskChangeKind.Comment, Comment: text);

    /// <summary>A status move of <paramref name="task"/> from <paramref name="from"/> to <paramref name="to"/>, written the way the Task file writes it.</summary>
    /// <param name="harness">The harness whose clock stamps the entry.</param>
    /// <param name="task">The Task.</param>
    /// <param name="actor">Who moved it.</param>
    /// <param name="from">The old status.</param>
    /// <param name="to">The new status.</param>
    /// <returns>The change.</returns>
    internal static TaskChange Move(Harness harness, TaskItem task, TaskActor actor, TaskState from, TaskState to) =>
        new(task, task, [new FieldChange(TaskField.Status, from.ToWire(), to.ToWire())], actor, new ChangeLogEntry(harness.Clock.GetUtcNow(), actor.Name, MoveSummary(from, to)));

    /// <summary>The Change log summary, and so the Message bullet text, of a status move.</summary>
    /// <param name="from">The old status.</param>
    /// <param name="to">The new status.</param>
    /// <returns>The summary.</returns>
    internal static string MoveSummary(TaskState from, TaskState to) => $"Status: {from.ToWire()} → {to.ToWire()}";

    /// <summary>The bullet a comment gets in a Message: its author and its text, with every Mention neutralised.</summary>
    /// <param name="author">The author's Name.</param>
    /// <param name="shownText">The comment text as shown, already cut if it was cut, before neutralising.</param>
    /// <returns>The bullet line.</returns>
    internal static string Bullet(string author, string shownText) => $"- comment by {author}: \"{shownText.Replace("@", NeutralisedAt, StringComparison.Ordinal)}\"";

    /// <summary>Lets the coalescing window elapse and waits for every fire.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>A task that completes when every fire is done.</returns>
    internal static async Task FireAsync(Harness harness)
    {
        harness.Clock.Advance(DefaultWindow);
        await harness.Trigger.WhenIdleAsync();
    }

    /// <summary>The Message texts posted, in order.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The texts.</returns>
    internal static string[] Texts(Harness harness) => [.. harness.Posts.Select(static p => p.Message.Text)];
}

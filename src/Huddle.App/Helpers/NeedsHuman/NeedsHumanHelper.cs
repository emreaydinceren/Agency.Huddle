using System.Globalization;
using System.Text.Json.Serialization.Metadata;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Helpers.NeedsHuman;

/// <summary>
/// Turns the Human gate on when an Agent's Task leaves the team or can't be undone (Helpers Spec §6.10 and §6.11). It only
/// ever turns the gate on, and never judges a Task the Human has turned the gate off on.
/// </summary>
/// <param name="tasks">Reads the Task at apply time.</param>
/// <param name="service">Makes the change, through the same service the Human and the Agents use.</param>
/// <param name="overrides">The Tasks the Human turned the gate off on.</param>
/// <param name="settings">Tells whether the Helper has been turned off since the run started.</param>
/// <param name="levels">Resolves the Task's Needs You level, so the Helper is told when a local merge stays inside the Team (ADR-0088); <see langword="null"/> means Ask me.</param>
internal sealed class NeedsHumanHelper(TaskStore tasks, TaskService service, NeedsHumanOverrideStore overrides, HelperSettingsResolver settings, NeedsYouLevels? levels = null)
    : IHelperAgent<NeedsHumanInput, NeedsHumanVerdict>
{
    /// <summary>The id the Helper is registered, configured and stored under.</summary>
    public static readonly HelperId HelperId = new("NeedsHuman");

    /// <summary>The longest description shown to the model.</summary>
    internal const int MaxDescriptionLength = 6000;

    /// <summary>The Task Reason limit (Tasks Spec §9.2).</summary>
    private const int MaxReasonLength = 200;

    /// <summary>The Task Action limit (Human gate Spec §6.1).</summary>
    private const int MaxActionLength = 200;

    /// <inheritdoc/>
    public HelperId Id => HelperId;

    /// <inheritdoc/>
    public string DisplayName => "Needs Human";

    /// <inheritdoc/>
    public string Description => "Turns on the Human gate when an Agent's Task leaves the team or can't be undone. It never turns it off.";

    /// <inheritdoc/>
    public IReadOnlyList<string> PromptKeys { get; } = ["helper.needsHuman.system", "helper.needsHuman.task"];

    /// <inheritdoc/>
    public HelperDefaults Defaults { get; } = new(Enabled: true, Adapter: "claude", Model: "haiku", Effort: "low", DailyCap: 50);

    /// <inheritdoc/>
    public JsonTypeInfo<NeedsHumanVerdict> VerdictType => NeedsHumanJsonContext.Default.NeedsHumanVerdict;

    /// <inheritdoc/>
    public string Fingerprint(NeedsHumanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Fingerprint;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> PromptValues(NeedsHumanInput input) => ValuesFor(input, this.LevelNote(input));

    /// <summary>
    /// The note that tells the model the Human set the Task's scope to Run inside the Team, so a merge into the local main stays inside the
    /// Team (ADR-0088); empty under Ask me, when no levels are wired, or when the Task is gone.
    /// </summary>
    /// <param name="input">The input.</param>
    internal string LevelNote(NeedsHumanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return levels is { } source
            && tasks.Get(input.Id) is { } task
            && source.ForTask(task) is { Level: NeedsYouLevel.RunInsideTheTeam } level
            ? LevelNoteFor(level.Scope)
            : string.Empty;
    }

    /// <summary>The note for a scope that runs inside the Team.</summary>
    /// <param name="scope">The scope that set the level.</param>
    internal static string LevelNoteFor(NeedsYouScope scope) => string.Create(
        CultureInfo.InvariantCulture,
        $"\n\nThe human set this task's {scope.ToDisplay()} to run inside the team: a merge into the team's local main stays inside the team and does not need the human. A push to any remote still does, and so does everything else listed.");

    /// <summary>The prompt values for <paramref name="input"/>: the Task, its description without the Evidence section and cut to <see cref="MaxDescriptionLength"/> characters.</summary>
    /// <param name="input">The input.</param>
    /// <param name="level">The Needs You level note, empty under Ask me.</param>
    internal static Dictionary<string, string> ValuesFor(NeedsHumanInput input, string level = "")
    {
        ArgumentNullException.ThrowIfNull(input);

        string description = ContentFingerprint.WithoutEvidence(input.Description);
        if (description.Length > MaxDescriptionLength)
        {
            description = string.Concat(description.AsSpan(0, MaxDescriptionLength), "[…cut]");
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{taskId}}"] = input.Id.ToString(),
            ["{{team}}"] = input.Team,
            ["{{tags}}"] = input.Tags.Count == 0 ? "none" : string.Join(", ", input.Tags),
            ["{{title}}"] = input.Title,
            ["{{description}}"] = description,
            ["{{level}}"] = level,
        };
    }

    /// <inheritdoc/>
    public string? Validate(NeedsHumanVerdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        return verdict.NeedsHuman && string.IsNullOrWhiteSpace(verdict.Reason) ? "a true answer needs a reason" : null;
    }

    /// <inheritdoc/>
    public Task<HelperApplied> ApplyAsync(string key, NeedsHumanInput input, NeedsHumanVerdict verdict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(verdict);
        return Task.FromResult(this.Apply(input, verdict));
    }

    /// <summary>Applies a Verdict: re-reads the Task, drops a stale Verdict, and writes at most once more after a conflict.</summary>
    private HelperApplied Apply(NeedsHumanInput input, NeedsHumanVerdict verdict)
    {
        if (!verdict.NeedsHuman)
        {
            return new HelperApplied.NoChange();
        }

        TaskActor actor = TaskActors.Helper(this.DisplayName);
        string reason = CutReason(verdict.Reason ?? string.Empty);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (tasks.Get(input.Id) is not { } current)
            {
                return new HelperApplied.Dropped("the Task is gone");
            }

            string? drop = this.WhyDrop(current, input);
            if (drop is not null)
            {
                return new HelperApplied.Dropped(drop);
            }

            // A Task that already has an Action is gated, so WhyDrop has dropped it: the Action the Helper names is only ever added.
            TaskPatch patch = string.IsNullOrWhiteSpace(verdict.Action)
                ? new TaskPatch { HumanGate = Optional<bool>.Set(true), Reason = reason }
                : new TaskPatch { HumanGate = Optional<bool>.Set(true), Reason = reason, Action = Optional<string?>.Set(CutTo(verdict.Action, MaxActionLength)) };
            TaskResult result = service.Update(input.Id, patch, current.Version, actor);
            switch (result)
            {
                case TaskResult.Saved:
                    return new HelperApplied.Changed($"Turned the gate on for {input.Id}");
                case TaskResult.Unchanged:
                    return new HelperApplied.NoChange();
                case TaskResult.Conflict:
                    continue;
                case TaskResult.Refused refused:
                    return new HelperApplied.Refused(string.Join("; ", refused.Problems));
                case TaskResult.GateRefused gateRefused:
                    return new HelperApplied.Refused(gateRefused.Message);
                default:
                    // NotFound: the Task was deleted between the read and the write, so the Verdict has nothing to apply to.
                    return new HelperApplied.Dropped("the Task is gone");
            }
        }

        return new HelperApplied.Dropped($"{input.Id} changed twice while the Verdict was being applied");
    }

    /// <summary>Why the Verdict no longer applies, or <see langword="null"/> when it still does.</summary>
    private string? WhyDrop(TaskItem current, NeedsHumanInput input)
    {
        if (current.IsGated)
        {
            return "the Task is already gated";
        }

        if (current.Status.IsTerminal())
        {
            return "the Task is closed";
        }

        if (overrides.Contains(input.Id.ToString()))
        {
            return "the Human turned the gate off on this Task";
        }

        if (!string.Equals(ContentFingerprint.Of(current.Title, current.Action, current.Description), input.Fingerprint, StringComparison.Ordinal))
        {
            return "the Task was saved again after it was judged";
        }

        return settings.For(this).Enabled ? null : "the Helper was turned off";
    }

    /// <summary>Cuts a reason to the Task Reason limit: 197 characters and an ellipsis.</summary>
    private static string CutReason(string reason) => CutTo(reason, MaxReasonLength);

    /// <summary>Trims <paramref name="text"/> and cuts it to <paramref name="limit"/> characters, the last three an ellipsis.</summary>
    private static string CutTo(string text, int limit)
    {
        string trimmed = text.Trim();
        return trimmed.Length <= limit
            ? trimmed
            : string.Create(CultureInfo.InvariantCulture, $"{trimmed[..(limit - 3)]}...");
    }
}

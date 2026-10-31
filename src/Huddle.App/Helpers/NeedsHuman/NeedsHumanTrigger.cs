using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;

namespace Agency.Huddle.App.Helpers.NeedsHuman;

/// <summary>
/// Watches Task changes and queues a judgement for each Task an Agent creates or rewrites (Helpers Spec §6.9). It does
/// only fast, in-memory work, because <see cref="TaskEvents"/> runs handlers on the saver's thread and lets a throwing one
/// propagate: a Helper must never turn a successful save into an error.
/// </summary>
/// <param name="events">The Task change stream.</param>
/// <param name="dispatcher">Takes the judgement.</param>
/// <param name="overrides">Remembers where the Human turned the gate off.</param>
/// <param name="logger">Records a handler failure.</param>
internal sealed class NeedsHumanTrigger(TaskEvents events, HelperDispatcher dispatcher, NeedsHumanOverrideStore overrides, ILogger<NeedsHumanTrigger> logger)
    : IHostedService
{
    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        events.TaskChanged += this.OnTaskChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        events.TaskChanged -= this.OnTaskChanged;
        return Task.CompletedTask;
    }

    /// <summary>Applies the filters in order, then queues the Task.</summary>
    /// <param name="change">The change.</param>
    internal void OnTaskChanged(TaskChange change)
    {
        try
        {
            this.Handle(change);
        }
        catch (Exception ex)
        {
            // The saver is still waiting on this handler; a failure here must never reach it.
            logger.LogWarning(ex, "The Needs Human trigger failed for {TaskId}.", change.After.Id.ToString());
        }
    }

    /// <summary>Whether the change touched the title, the description or the Action.</summary>
    private static bool TouchesText(TaskChange change)
    {
        foreach (FieldChange field in change.Changes)
        {
            if (field.Field is TaskField.Title or TaskField.Description or TaskField.Action)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The filters of Helpers Spec §6.9, each a cheap check on the change.</summary>
    private void Handle(TaskChange change)
    {
        TaskItem after = change.After;

        // 1. The Human's decision is recorded whether or not the Helper is on, so an override made while it was off still counts.
        if (change.Actor.Kind == TaskActorKind.Human && change.Before is { HumanGate: true } && !after.HumanGate)
        {
            overrides.Add(after.Id.ToString());
            return;
        }

        // 2. Agents only: not the Human, an outside edit, or the Helper's own writes.
        if (change.Actor.Kind != TaskActorKind.Agent)
        {
            return;
        }

        // 3. A create, or a change to the text the Helper reads.
        if (change.Before is not null && !TouchesText(change))
        {
            return;
        }

        // 4. Open and ungated; an Action gates the Task already.
        if (after.IsGated || after.Status.IsTerminal())
        {
            return;
        }

        // 5. The content really changed: an Evidence line is not a change.
        string fingerprint = ContentFingerprint.Of(after.Title, after.Action, after.Description);
        if (change.Before is not null && string.Equals(fingerprint, ContentFingerprint.Of(change.Before.Title, change.Before.Action, change.Before.Description), StringComparison.Ordinal))
        {
            return;
        }

        // 6. The Human's word is final.
        string key = after.Id.ToString();
        if (overrides.Contains(key))
        {
            return;
        }

        dispatcher.Enqueue(NeedsHumanHelper.HelperId, key, new NeedsHumanInput(after.Id, fingerprint, after.Title, after.Description, after.Location.Team, after.Tags));
    }
}

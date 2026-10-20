using System.Globalization;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>The pure rules H-1 to H-5 that keep a Human-gated Task from skipping the Human (Human gate Spec §6.4).</summary>
internal static class HumanGateRules
{
    /// <summary>Checks a proposed change to a Task.</summary>
    /// <param name="before">The Task as it is, or <see langword="null"/> when the change creates it (H-5).</param>
    /// <param name="after">The Task as it would be.</param>
    /// <param name="actor">Who makes the change; the Human is the actor of kind <see cref="TaskActorKind.Human"/>.</param>
    /// <param name="proof">The Proof of <paramref name="after"/>.</param>
    /// <param name="humanName">The Human's Name, for messages.</param>
    /// <returns>The first refusal, or <see langword="null"/> when the change is allowed.</returns>
    public static GateRefusal? Check(TaskItem? before, TaskItem after, TaskActor actor, Proof proof, string humanName)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentException.ThrowIfNullOrWhiteSpace(humanName);

        string id = after.Id.ToString();
        if (before is null)
        {
            return CheckCreate(after, proof, id);
        }

        bool human = actor.Kind == TaskActorKind.Human;
        bool gateOff = before.HumanGate && !after.HumanGate && !(human && after.Action is null);
        bool actionCleared = before.Action is not null && after.Action is null && !human;
        if (gateOff || actionCleared)
        {
            return new GateRefusal(GateId.HumanGate, $"Only {humanName} can turn off the Human gate on {id}.");
        }

        if (after.Status == TaskState.NeedsYou && before.Status != TaskState.NeedsYou)
        {
            return CheckEntry(after, proof, id);
        }

        if (after.Status == TaskState.Done && before.Status != TaskState.Done && (before.IsGated || after.IsGated))
        {
            return CheckDone(before, after, proof, id, humanName);
        }

        return null;
    }

    /// <summary>H-5, and H-3 for an ungated Task created in Needs You.</summary>
    /// <param name="after">The new Task.</param>
    /// <param name="proof">Its Proof.</param>
    /// <param name="id">Its id text.</param>
    private static GateRefusal? CheckCreate(TaskItem after, Proof proof, string id)
    {
        if (after.IsGated && after.Status is TaskState.NeedsYou or TaskState.Done)
        {
            return new GateRefusal(GateId.HumanGate, $"{id} is Human-gated, so it starts before Needs You.");
        }

        return after.Status == TaskState.NeedsYou ? CheckEntry(after, proof, id) : null;
    }

    /// <summary>H-3: a move into Needs You.</summary>
    /// <param name="after">The Task as it would be.</param>
    /// <param name="proof">Its Proof.</param>
    /// <param name="id">Its id text.</param>
    private static GateRefusal? CheckEntry(TaskItem after, Proof proof, string id)
    {
        if (!after.IsGated)
        {
            return new GateRefusal(GateId.ProofIncomplete, $"{id} isn't Human-gated, so it goes from Review to Done.");
        }

        if (proof.IsComplete)
        {
            return null;
        }

        string label = proof.Items.First(static item => !item.Met).Label;
        string open = label.Length == 0 ? label : char.ToLowerInvariant(label[0]) + label[1..];
        string counts = string.Create(CultureInfo.InvariantCulture, $"{proof.MetCount}/{proof.Total}");
        return new GateRefusal(GateId.ProofIncomplete, $"{id} can go to Needs You once its Proof is complete ({counts}: {open} is open).");
    }

    /// <summary>H-4: a move to Done on a gated Task needs a valid acceptance of the content as it ends up.</summary>
    /// <param name="before">The Task as it is.</param>
    /// <param name="after">The Task as it would be.</param>
    /// <param name="proof">Its Proof.</param>
    /// <param name="id">Its id text.</param>
    /// <param name="humanName">The Human's Name.</param>
    private static GateRefusal? CheckDone(TaskItem before, TaskItem after, Proof proof, string id, string humanName)
    {
        AcceptanceInfo acceptance = Acceptance.Of(before);
        string fingerprint = ContentFingerprint.Of(after.Title, after.Action, after.Description);
        bool allowed = before.Status == TaskState.NeedsYou
            && proof.IsComplete
            && acceptance.State == AcceptanceState.Valid
            && string.Equals(fingerprint, acceptance.Hash, StringComparison.Ordinal);
        return allowed
            ? null
            : new GateRefusal(
                GateId.HumanGate,
                $"{id} needs {humanName}'s acceptance. Move it to Needs You when the Proof is complete; you'll be woken when {humanName} decides.");
    }
}

using System.Globalization;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>Turns a Team's Definition of Done into a <see cref="Proof"/> for one Task (Spec §6.3). Pure: every input is in the <see cref="ProofContext"/>.</summary>
internal static class ProofEvaluator
{
    private const string Verify = "verify";
    private const string QaLabel = "QA evidence";
    private const string FailingTestLabel = "A test failed before the fix";
    private const string UnreadableDetail = "Huddle can't read this rule";
    private const string NoTaskYet = "no Task yet";
    private const string NoDocument = "no document attached";
    private const string Dash = "—";

    /// <summary>Evaluates the Task's Proof.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="context">What the evaluation needs to know.</param>
    /// <returns>The Process items in written order, then "QA evidence" and "A test failed before the fix" when their gates are on.</returns>
    public static Proof Evaluate(TaskItem task, ProofContext context)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(context);

        List<ProofItem> items = [.. context.Definition.Select(item => EvaluateItem(task, context, item.Label, item.Rule, item.Source))];
        bool alreadyVerifies = context.Definition.Any(static item => item.Rule is DoneRule.EvidenceFrom from && string.Equals(from.Step, Verify, StringComparison.OrdinalIgnoreCase));
        if (context.Gates.Contains(GateId.QaEvidenceBeforeDone) && !alreadyVerifies)
        {
            items.Add(EvaluateItem(task, context, QaLabel, new DoneRule.EvidenceFrom(Verify), ProofItemSource.Gate));
        }

        if (context.Gates.Contains(GateId.FailingTestBeforeFix) && context.IsBug)
        {
            bool met = ProcessGateService.HasFailureBeforePass(context.Evidence);
            items.Add(new ProofItem(FailingTestLabel, met, met ? null : task.Assignee, null, ProofItemSource.Gate));
        }

        return new Proof(items);
    }

    private static ProofItem EvaluateItem(TaskItem task, ProofContext context, string label, DoneRule rule, ProofItemSource source)
    {
        (bool met, string? owner, string? detail) = rule switch
        {
            DoneRule.EvidenceFrom evidence => EvidenceFrom(context, evidence.Step),
            DoneRule.CommandExits command => CommandExits(task, context, command.Command),
            DoneRule.TaskTagged tagged => TaskTagged(context, tagged.Tag),
            DoneRule.DocumentReviewedBy review => DocumentReviewed(context, review.Step),
            _ => (false, null, UnreadableDetail),
        };
        return new ProofItem(label, met, met ? null : owner, met ? null : detail, source);
    }

    private static (bool Met, string? Owner, string? Detail) EvidenceFrom(ProofContext context, string step)
    {
        bool met = context.Evidence.Any(line => line.Command.Length > 0 && Covers(context, line.Author, step));
        return (met, FirstCovering(context, step), null);
    }

    private static (bool Met, string? Owner, string? Detail) CommandExits(TaskItem task, ProofContext context, string command)
    {
        EvidenceLine? latest = context.Evidence
            .Where(line => line.ExitCode is not null && string.Equals(line.Command.Trim(), command.Trim(), StringComparison.Ordinal))
            .OrderBy(static line => line.Time)
            .LastOrDefault();
        if (latest is null)
        {
            return (false, task.Assignee, "not run");
        }

        int code = latest.ExitCode ?? 0;
        return (code == 0, task.Assignee, string.Create(CultureInfo.InvariantCulture, $"last run exit {code}"));
    }

    private static (bool Met, string? Owner, string? Detail) TaskTagged(ProofContext context, string tag)
    {
        List<TaskItem> tagged = [.. context.Children.Where(child => child.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))];
        if (tagged.Any(static child => child.Status == TaskState.Done))
        {
            return (true, null, null);
        }

        if (tagged.Count == 0)
        {
            return (false, null, NoTaskYet);
        }

        TaskItem child = tagged[0];
        (int done, int boxes) = CountBoxes(child.Description);
        return (false, child.Assignee, string.Create(CultureInfo.InvariantCulture, $"{done} of {boxes} in #{child.Id}"));
    }

    private static (int Checked, int Total) CountBoxes(string description)
    {
        int ticked = 0;
        int total = 0;
        bool inFence = false;
        foreach (string raw in description.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && line.StartsWith("- [ ]", StringComparison.Ordinal))
            {
                total++;
            }
            else if (!inFence && (line.StartsWith("- [x]", StringComparison.Ordinal) || line.StartsWith("- [X]", StringComparison.Ordinal)))
            {
                total++;
                ticked++;
            }
        }

        return (ticked, total);
    }

    private static (bool Met, string? Owner, string? Detail) DocumentReviewed(ProofContext context, string step)
    {
        string? owner = FirstCovering(context, step);
        if (context.Documents.Count == 0)
        {
            return (false, owner, NoDocument);
        }

        (bool Met, string Detail, int Rank) best = (false, string.Empty, -1);
        foreach (ProofDocument document in context.Documents)
        {
            (bool Met, string Detail, int Rank) result = ReviewState(context, step, document);
            if (result.Met)
            {
                return (true, null, null);
            }

            if (result.Rank > best.Rank)
            {
                best = result;
            }
        }

        return (false, owner, best.Detail);
    }

    private static (bool Met, string Detail, int Rank) ReviewState(ProofContext context, string step, ProofDocument document)
    {
        IReadOnlyList<DocHistoryEntry> history = document.History;
        int lastChange = LastIndex(history, static entry => entry.Kind is DocChangeKind.Created or DocChangeKind.Edited);
        int review = LastIndex(history, entry => entry.Kind == DocChangeKind.Reviewed && Covers(context, entry.Actor, step));
        if (review > lastChange)
        {
            int objection = LastIndex(history, static entry => entry.Kind == DocChangeKind.Objected);
            if (objection < review)
            {
                return (true, string.Empty, 2);
            }

            DocHistoryEntry objected = history[objection];
            return (false, $"{document.File} {Dash} {objected.Actor} objected: {objected.Summary}", 1);
        }

        if (review >= 0)
        {
            return (false, $"{document.File} {Dash} edited after {history[review].Actor}'s review", 1);
        }

        return (false, $"{document.File} {Dash} not reviewed yet", 0);
    }

    private static int LastIndex(IReadOnlyList<DocHistoryEntry> history, Func<DocHistoryEntry, bool> match)
    {
        for (int index = history.Count - 1; index >= 0; index--)
        {
            if (match(history[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Covers(ProofContext context, string name, string step) =>
        context.Coverage.TryGetValue(name, out string? covers)
        && covers.Split(',', StringSplitOptions.TrimEntries).Contains(step, StringComparer.OrdinalIgnoreCase);

    private static string? FirstCovering(ProofContext context, string step) =>
        context.Roster.FirstOrDefault(name => Covers(context, name, step));
}

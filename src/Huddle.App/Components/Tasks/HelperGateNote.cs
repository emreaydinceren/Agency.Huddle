using Agency.Huddle.App.Components.Settings.Helpers;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// The line the Task panel shows under the Human gate switch when a Helper turned the gate on: it tells the Human, before they
/// act, that turning it off is final (Helpers Spec §6.13, policy 3).
/// </summary>
internal static class HelperGateNote
{
    private const string GateOn = "human gate on";
    private const string GateOff = "human gate off";
    private const string ReasonMarker = "(reason: ";

    /// <summary>The line to show, or <see langword="null"/> when the gate is off or the latest gate-on entry is not a Helper's.</summary>
    /// <param name="changeLog">The Task's Change log, oldest first.</param>
    /// <param name="gateOn">Whether the gate is on as the panel now shows it.</param>
    /// <param name="helpers">The registered Helpers; an entry counts when its actor is one of their Change log names.</param>
    /// <param name="hasAction">Whether the Task names an Action, which locks the gate on, so the line cannot offer to turn it off (#452).</param>
    public static string? For(IReadOnlyList<ChangeLogEntry>? changeLog, bool gateOn, IReadOnlyList<IHelperAgent> helpers, bool hasAction = false)
    {
        ArgumentNullException.ThrowIfNull(helpers);
        if (!gateOn || changeLog is null)
        {
            return null;
        }

        for (int i = changeLog.Count - 1; i >= 0; i--)
        {
            ChangeLogEntry entry = changeLog[i];
            if (entry.Summary.Contains(GateOff, StringComparison.Ordinal))
            {
                // The Human (or an Agent) turned it off after any earlier Helper entry; whatever holds it on now is not the Helper's word.
                return null;
            }

            if (!entry.Summary.Contains(GateOn, StringComparison.Ordinal))
            {
                continue;
            }

            // The latest "human gate on" entry decides: a Human who turned the gate on again after a Helper did made it their own.
            IHelperAgent? helper = helpers.FirstOrDefault(candidate => string.Equals(TaskActors.Helper(candidate.DisplayName).Name, entry.Actor, StringComparison.Ordinal));
            return helper is null ? null : HelpersCopy.TaskPanel(helper.DisplayName, ReasonOf(entry.Summary), hasAction);
        }

        return null;
    }

    /// <summary>The text of the trailing <c>(reason: …)</c>, or empty when there is none.</summary>
    private static string ReasonOf(string summary)
    {
        int start = summary.LastIndexOf(ReasonMarker, StringComparison.Ordinal);
        if (start < 0 || !summary.EndsWith(')'))
        {
            return string.Empty;
        }

        int from = start + ReasonMarker.Length;
        return summary[from..^1];
    }
}

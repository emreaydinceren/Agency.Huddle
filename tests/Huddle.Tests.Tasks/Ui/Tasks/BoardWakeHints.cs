namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.Views;

/// <summary>
/// Reads the Board's per-zone drag hints (smart wakes spec 5.5) the way the tests pin them: a hint is an element with
/// class <see cref="HintClass"/> inside the drop zone whose drop it describes - the single-state column's zone or the
/// ghost bucket, never the multi-state display zone around the buckets. A test compares the whole list, so a hint on the
/// wrong zone, a hint on too many zones and a hint with the wrong name each fail it.
/// </summary>
internal static class BoardWakeHints
{
    /// <summary>The class of one zone's hint, and the selector's source.</summary>
    public const string HintClass = "task-zone-wake-hint";

    /// <summary>The class of the retired Board-wide caption, which must be gone.</summary>
    public const string RetiredCaptionClass = "task-board-drag-caption";

    /// <summary>The hint on one zone, as <see cref="Of"/> reports it.</summary>
    /// <param name="lane">The lane key (an ungrouped Board uses the empty string).</param>
    /// <param name="state">The zone's state.</param>
    /// <param name="assignee">The name the hint says.</param>
    /// <returns>The entry.</returns>
    public static string Entry(string lane, TaskState state, string assignee) =>
        $"{BoardLayout.ZoneId(lane, state)} => Notifies {assignee}";

    /// <summary>
    /// Every hint on the Board in document order, as "zone id => text": each hint is attributed to the nearest enclosing
    /// drop zone, so a hint outside any zone reads " => ..." and fails the comparison.
    /// </summary>
    /// <param name="board">The rendered Board.</param>
    /// <returns>One entry per hint.</returns>
    public static string[] Of(IRenderedComponent<TaskBoard> board) =>
        [.. board.FindAll($".{HintClass}").Select(hint =>
            $"{hint.Closest(".mud-drop-zone")?.GetAttribute("identifier")} => {hint.TextContent.Trim()}")];
}

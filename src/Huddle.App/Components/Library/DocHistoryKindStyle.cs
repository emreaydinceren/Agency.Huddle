using Agency.Huddle.App.DocHistory;
using MudBlazor;

namespace Agency.Huddle.App.Components.Library;

/// <summary>The tinted <c>StatusChip</c> a History entry's kind wears: a colour family and an icon, so the kind is never colour alone.</summary>
internal static class DocHistoryKindStyle
{
    /// <summary>Created Info, Edited and a bare Human save Default, Reviewed Success, Objected Warning (amber means "needs attention", and an objection does).</summary>
    /// <param name="entry">An entry.</param>
    /// <returns>The chip's colour family.</returns>
    public static Color ColorFor(DocHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return DocHistoryView.IsBareHumanSave(entry)
            ? Color.Default
            : entry.Kind switch
            {
                DocChangeKind.Created => Color.Info,
                DocChangeKind.Edited => Color.Default,
                DocChangeKind.Reviewed => Color.Success,
                _ => Color.Warning,
            };
    }

    /// <summary>The icon that always accompanies the kind's colour and word.</summary>
    /// <param name="entry">An entry.</param>
    /// <returns>The leading icon.</returns>
    public static string IconFor(DocHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return DocHistoryView.IsBareHumanSave(entry)
            ? Icons.Material.Filled.Save
            : entry.Kind switch
            {
                DocChangeKind.Created => Icons.Material.Filled.NoteAdd,
                DocChangeKind.Edited => Icons.Material.Filled.Edit,
                DocChangeKind.Reviewed => Icons.Material.Filled.CheckCircle,
                _ => Icons.Material.Filled.ReportProblem,
            };
    }
}

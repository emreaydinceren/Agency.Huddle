namespace Agency.Huddle.App.Components.Shared;

/// <summary>One chip of a picker cloud: its label and whether it is currently selected.</summary>
/// <param name="Text">The chip's label.</param>
/// <param name="Selected">True when the chip is switched on.</param>
internal sealed record ChipItem(string Text, bool Selected);

/// <summary>
/// Arranges one chip cloud of the Avatar Library picker (Spec §6.5): selected chips first in the order they were
/// pressed, then the unselected pool in pool order. The arrangement is pure, so pressing a chip moves only that chip.
/// </summary>
internal static class AvatarChipCloud
{
    /// <summary>Builds the chips to show, in display order.</summary>
    /// <param name="pool">Every chip of the cloud in its fixed order.</param>
    /// <param name="selectedInPressOrder">The selected chips, oldest press first.</param>
    /// <param name="cap">How many pool entries to show before the selected ones, or null to show them all. Selected chips always show.</param>
    /// <returns>The selected chips (flagged) followed by the first <paramref name="cap"/> pool entries that are not selected.</returns>
    internal static IReadOnlyList<ChipItem> Arrange(IReadOnlyList<string> pool, IReadOnlyList<string> selectedInPressOrder, int? cap)
    {
        HashSet<string> selected = new(selectedInPressOrder, StringComparer.Ordinal);
        List<ChipItem> chips = [.. selectedInPressOrder.Distinct(StringComparer.Ordinal).Select(static text => new ChipItem(text, true))];
        IEnumerable<string> visible = cap is int limit ? pool.Take(limit) : pool;
        chips.AddRange(visible.Where(text => !selected.Contains(text)).Select(static text => new ChipItem(text, false)));
        return chips;
    }
}

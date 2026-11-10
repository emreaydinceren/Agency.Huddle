namespace Agency.Huddle.App.Components;

/// <summary>The three looks of the one <c>Chip</c>(design-system "Chips"); the fourth, count or meta, is plain caption text and no chip.</summary>
public enum ChipLook
{
    /// <summary>A state in a word, tinted in a status colour with a leading icon (<c>.status-chip-*</c>).</summary>
    Status,

    /// <summary>A quiet label such as a tag: a wash fill and a line (<c>.tag-chip</c>).</summary>
    Tag,

    /// <summary>A choice the Human makes: dashed while unset, an Accent line and tint once set (<c>.filter-chip</c>).</summary>
    Filter,
}

namespace Agency.Huddle.App.Themes.Skins;

/// <summary>How inline marks such as mentions and code are drawn (skin trait <c>marks</c>).</summary>
internal enum MarkStyle
{
    /// <summary>Today's Huddle: plain text marks.</summary>
    Plain,

    /// <summary>Chips, bands and wells: wiki links, references, mentions, quotes and tool summaries are drawn as shapes.</summary>
    Chip,
}

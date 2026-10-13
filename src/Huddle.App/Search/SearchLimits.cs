namespace Agency.Huddle.App.Search;

/// <summary>The constants of Global Search V1. They are not configuration (spec §7.2, D-24, OP-5).</summary>
internal static class SearchLimits
{
    /// <summary>The fewest characters a phrase needs before anything searches (spec §7.2).</summary>
    public const int MinChars = 3;

    /// <summary>The most hits one group shows under the All filter (spec §6.3).</summary>
    public const int GroupCap = 5;

    /// <summary>The most hits one group shows under a single filter (OP-5).</summary>
    public const int SingleFilterCap = 50;

    /// <summary>The width, in characters, of a content snippet (spec §8.3).</summary>
    public const int SnippetWidth = 120;

    /// <summary>How long an expensive provider waits after the last keystroke before it reads from disk (spec §7.3).</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(200);
}

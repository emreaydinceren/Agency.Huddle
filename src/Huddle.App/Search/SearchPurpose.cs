namespace Agency.Huddle.App.Search;

/// <summary>
/// Why a search runs (spec §5.2, D-4). <see cref="Box"/> is the Global Search box: content and file search.
/// <see cref="Reference"/> is the Composer's picker: names only, no Transcript or file is read, and an empty
/// phrase browses.
/// </summary>
public enum SearchPurpose
{
    /// <summary>The Global Search box.</summary>
    Box,

    /// <summary>The Composer's reference picker: names only.</summary>
    Reference,
}

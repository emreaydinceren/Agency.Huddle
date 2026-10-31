namespace Agency.Huddle.App.Search;

/// <summary>What search does with one Library file: read its contents, or match its name only.</summary>
internal enum ContentDecision
{
    /// <summary>The file is a text file within the size limit; its contents are read.</summary>
    Read,

    /// <summary>The file is a text file over the size limit; only its name is matched.</summary>
    NameOnlyTooLarge,

    /// <summary>The file is not a configured text file; only its name is matched.</summary>
    NameOnlyNotText,
}

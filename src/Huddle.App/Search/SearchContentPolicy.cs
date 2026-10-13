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

/// <summary>Decides whether a Library file's contents are read (spec §8.3).</summary>
internal static class SearchContentPolicy
{
    /// <summary>Decides from the file name and length alone, so no file is opened to choose.</summary>
    /// <param name="fileName">The file's name, with its extension.</param>
    /// <param name="length">The file's length in bytes.</param>
    /// <param name="options">The search options, for the text extensions and the size limit.</param>
    /// <returns>What to do with the file.</returns>
    public static ContentDecision Decide(string fileName, long length, SearchOptions options)
    {
        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !options.EffectiveTextExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return ContentDecision.NameOnlyNotText;
        }

        return length > options.MaxContentFileBytes ? ContentDecision.NameOnlyTooLarge : ContentDecision.Read;
    }
}

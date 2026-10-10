namespace Agency.Huddle.App.Library;

/// <summary>
/// The text handling for a <c>?library=</c> link (a wikilink, a chat Message) and for the
/// <c>/library</c> page address it opens. Pure string work; resolving the value to a file stays with
/// <see cref="LibraryPathResolver"/>.
/// </summary>
internal static class LibraryLinkQuery
{
    /// <summary>The <c>/library</c> page address for <paramref name="file"/>, with the root id and relative path escaped, in the shape the page reads back as <c>?root=</c> and <c>?path=</c>.</summary>
    /// <param name="file">The file to open.</param>
    public static string BuildPageUri(LibraryPath file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"/library?root={Uri.EscapeDataString(file.Root.Id)}&path={Uri.EscapeDataString(file.RelativePath)}");
    }

    /// <summary>The raw (still percent-encoded) value of a <c>library=</c> query parameter in <paramref name="location"/>, or <see langword="null"/> when there is none.</summary>
    /// <param name="location">The navigated-to location.</param>
    public static string? ExtractRawValue(string location)
    {
        ArgumentNullException.ThrowIfNull(location);

        int queryStart = location.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return null;
        }

        string query = location[(queryStart + 1)..];
        int fragmentStart = query.IndexOf('#', StringComparison.Ordinal);
        if (fragmentStart >= 0)
        {
            query = query[..fragmentStart];
        }

        foreach (string pair in query.Split('&'))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                continue;
            }

            if (string.Equals(pair[..equals], "library", StringComparison.Ordinal))
            {
                return pair[(equals + 1)..];
            }
        }

        return null;
    }

    /// <summary>Removes the <c>library</c> query parameter from <paramref name="uri"/>, keeping every other parameter and the fragment.</summary>
    /// <param name="uri">The current, absolute navigation URI.</param>
    public static string Strip(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        int queryStart = uri.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return uri;
        }

        string baseUri = uri[..queryStart];
        string rest = uri[(queryStart + 1)..];
        int fragmentStart = rest.IndexOf('#', StringComparison.Ordinal);
        string fragment = fragmentStart >= 0 ? rest[fragmentStart..] : string.Empty;
        string query = fragmentStart >= 0 ? rest[..fragmentStart] : rest;

        List<string> kept = [];
        foreach (string pair in query.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = equals < 0 ? pair : pair[..equals];
            if (!string.Equals(key, "library", StringComparison.Ordinal))
            {
                kept.Add(pair);
            }
        }

        string newQuery = kept.Count > 0 ? "?" + string.Join('&', kept) : string.Empty;
        return baseUri + newQuery + fragment;
    }
}

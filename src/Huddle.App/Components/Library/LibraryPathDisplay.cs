namespace Agency.Huddle.App.Components.Library;

/// <summary>How the document toolbar writes a file's full system path when there is not room for all of it.</summary>
internal static class LibraryPathDisplay
{
    /// <summary>The longest path shown whole; anything longer keeps its start and its file name.</summary>
    public const int MaxLength = 60;

    private const string Ellipsis = "…";

    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>
    /// Shortens <paramref name="fullPath"/> to its drive or root and first folder, an ellipsis, and the file
    /// name (<c>C:\Users\…\Email-Ideas.md</c>) when it is longer than <see cref="MaxLength"/>. A path that
    /// fits, or one too shallow to lose anything, is returned unchanged. The separator after the ellipsis is
    /// the one the path itself uses before its file name.
    /// </summary>
    /// <param name="fullPath">The file's full system path.</param>
    /// <returns>The path as the toolbar draws it.</returns>
    public static string Shorten(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        if (fullPath.Length <= MaxLength)
        {
            return fullPath;
        }

        int nameStart = fullPath.LastIndexOfAny(Separators);
        int firstSeparator = fullPath.IndexOfAny(Separators);
        int secondSeparator = firstSeparator < 0 ? -1 : fullPath.IndexOfAny(Separators, firstSeparator + 1);
        if (secondSeparator < 0 || secondSeparator >= nameStart)
        {
            return fullPath;
        }

        return string.Concat(fullPath.AsSpan(0, secondSeparator + 1), Ellipsis, fullPath.AsSpan(nameStart));
    }
}

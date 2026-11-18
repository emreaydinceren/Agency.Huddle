namespace Agency.Huddle.App.Goldfish;

/// <summary>
/// Builds the <c>Read(...)</c> deny rules a goldfish session is started with, so it cannot read the possessed Agent's memory or the
/// Rooms' history from disk (spec 8.4). Spike S-1 measured the form: <c>Read(//e/huddle/data/rooms/**)</c>, a double slash, forward
/// slashes and a lowercase drive letter with no colon; <c>Grep</c> and <c>Glob</c> honour a <c>Read</c> deny, so one rule per folder is enough.
/// </summary>
/// <remarks>
/// A <c>**</c> in the middle of a path and a path with spaces were not measured, so only a trailing <c>**</c> is ever written and a folder
/// whose path holds a space is passed through as it is.
/// </remarks>
internal static class GoldfishReadDeny
{
    /// <summary>Turns folders into deny rules: a folder that does not exist is dropped, and a repeated one yields one rule.</summary>
    /// <param name="folders">The folders to hide, in any spelling of the same path.</param>
    /// <returns>One rule per distinct existing folder, in first-seen order.</returns>
    internal static IReadOnlyList<string> Rules(IEnumerable<string> folders)
    {
        ArgumentNullException.ThrowIfNull(folders);

        List<string> rules = [];
        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            string rule = Rule(Path.GetFullPath(folder));
            if (!rules.Contains(rule, StringComparer.Ordinal))
            {
                rules.Add(rule);
            }
        }

        return rules;
    }

    /// <summary>Writes one rule for a full path: forward slashes, the drive letter lowercased with its colon dropped, and a trailing <c>/**</c>.</summary>
    /// <param name="fullPath">The folder's full path.</param>
    private static string Rule(string fullPath)
    {
        string slashed = fullPath.Replace('\\', '/').TrimEnd('/');
        string absolute = slashed.Length > 1 && slashed[1] == ':'
            ? string.Concat(char.ToLowerInvariant(slashed[0]).ToString(), slashed.AsSpan(2))
            : slashed;
        return $"Read(//{absolute.TrimStart('/')}/**)";
    }
}

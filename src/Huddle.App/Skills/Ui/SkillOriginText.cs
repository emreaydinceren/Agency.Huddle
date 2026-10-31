namespace Agency.Huddle.App.Skills.Ui;

/// <summary>Reads the parts of a pinned origin a person looks for: the repository and the short commit.</summary>
internal static class SkillOriginText
{
    /// <summary>For <c>https://host/owner/repo/tree/&lt;sha&gt;/path</c> (or <c>blob</c>): <c>owner/repo</c> and the first seven
    /// characters of the commit. For anything else, the origin as given and no commit.</summary>
    public static (string Repository, string ShortCommit) Parts(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            return (origin.Trim(), string.Empty);
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return (origin.Trim(), string.Empty);
        }

        string repository = $"{segments[0]}/{segments[1]}";
        string commit = segments.Length >= 4 && segments[2] is "tree" or "blob" && segments[3].Length >= 7 ? segments[3][..7] : string.Empty;
        return (repository, commit);
    }

    /// <summary>The Source column for a registry Skill with no copy: <i>publisher · repo · short commit · licence</i>, the
    /// empty parts left out.</summary>
    public static string Line(string? publisher, string origin, string? license)
    {
        (string repository, string commit) = Parts(origin);
        return string.Join(" · ", new[] { publisher, repository, commit, license }.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }
}

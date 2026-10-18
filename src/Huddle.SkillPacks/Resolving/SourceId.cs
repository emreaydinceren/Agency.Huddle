using System.Text.RegularExpressions;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>The Source id rule of §7.2: <c>host/owner/repo</c> or <c>local/name</c>, lowercase letters, digits, <c>.</c>
/// and <c>-</c>, with <c>/</c> between parts; no <c>..</c>, no leading <c>/</c>, no drive letter. So a Source folder can
/// never lie outside <c>sources/</c>.</summary>
public static partial class SourceId
{
    /// <summary>The prefix of a Source with no repository.</summary>
    public const string LocalPrefix = "local/";

    /// <summary>Whether <paramref name="id"/> follows the rule.</summary>
    public static bool IsValid(string? id)
    {
        if (string.IsNullOrEmpty(id) || !Shape().IsMatch(id))
        {
            return false;
        }

        string[] parts = id.Split('/');
        if (parts.Any(static part => part is "." or ".." || part.StartsWith('.')))
        {
            return false;
        }

        return id.StartsWith(LocalPrefix, StringComparison.Ordinal) ? parts.Length == 2 : parts.Length == 3;
    }

    /// <summary>The folder of a valid Source id under <paramref name="registryDir"/>: <c>sources/&lt;id&gt;</c>.</summary>
    public static string FolderOf(string registryDir, string id)
    {
        ArgumentNullException.ThrowIfNull(registryDir);
        ArgumentNullException.ThrowIfNull(id);
        return Path.Combine([registryDir, "sources", .. id.Split('/')]);
    }

    [GeneratedRegex(@"\A[a-z0-9.-]+(?:/[a-z0-9.-]+)+\z", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Shape();
}

/// <summary>Two origins name the same Skill when their identity is equal (S006, §6.3).</summary>
public static partial class OriginIdentity
{
    /// <summary>For <c>https://host/owner/repo/tree/&lt;ref&gt;/&lt;path&gt;</c> (or <c>blob</c>) the identity is
    /// <c>host/owner/repo/&lt;path&gt;</c>, lower-cased up to the path; for anything else the trimmed string without a trailing
    /// <c>/</c>.</summary>
    public static string Of(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        string trimmed = origin.Trim().TrimEnd('/');
        Match match = TreeUrl().Match(trimmed);
        if (!match.Success)
        {
            return trimmed;
        }

        string prefix = $"{match.Groups["host"].Value}/{match.Groups["owner"].Value}/{match.Groups["repo"].Value}".ToUpperInvariant();
        return $"{prefix}/{match.Groups["path"].Value}";
    }

    /// <summary>Whether two origins have the same identity.</summary>
    public static bool Same(string? first, string? second)
    {
        if (first is null || second is null)
        {
            return false;
        }

        return string.Equals(Of(first), Of(second), StringComparison.Ordinal);
    }

    /// <summary>Whether <paramref name="origin"/> is pinned: a <c>/tree/&lt;40 hex&gt;/</c> or <c>/blob/&lt;40 hex&gt;/</c>
    /// URL, or not a URL at all (a local origin). D004 warns otherwise.</summary>
    public static bool IsPinned(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        string trimmed = origin.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }

        return PinnedUrl().IsMatch(trimmed);
    }

    [GeneratedRegex(@"\Ahttps?://(?<host>[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+)/(?:tree|blob)/[^/]+/(?<path>.+)\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex TreeUrl();

    [GeneratedRegex(@"/(?:tree|blob)/[0-9a-fA-F]{40}(?:/|\z)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PinnedUrl();
}

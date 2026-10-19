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

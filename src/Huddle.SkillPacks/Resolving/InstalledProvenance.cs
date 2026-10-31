using System.Text.Json;
using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.SkillPacks.Resolving;

/// <summary>What the library needs from a copy's <c>Skills/&lt;name&gt;/.source.json</c> (§7.2): its origin, hash and
/// Source. Reads both the old seven-field file and the new one.</summary>
/// <param name="Origin">The pinned URL the copy came from; for an old file, derived from <c>repo</c>, <c>commit</c> and <c>path</c>.</param>
/// <param name="Sha256">The hash of the copy when it was made.</param>
/// <param name="Source">The Source id; for an old file, derived from <c>repo</c>.</param>
public sealed record InstalledProvenance(string Origin, string Sha256, string? Source)
{
    private const string GitHubPrefix = "https://github.com/";

    /// <summary>Reads the file's text; null when it is not a provenance file.</summary>
    public static InstalledProvenance? Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument? document = JsonFields.TryParse(json);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement root = document.RootElement;
        string? sha = JsonFields.String(root, "sha256");
        if (string.IsNullOrWhiteSpace(sha))
        {
            return null;
        }

        string? repo = JsonFields.String(root, "repo");
        string? origin = JsonFields.String(root, "origin") ?? DeriveOrigin(repo, JsonFields.String(root, "commit"), JsonFields.String(root, "path"));
        if (origin is null)
        {
            return null;
        }

        return new InstalledProvenance(origin, sha, JsonFields.String(root, "source") ?? DeriveSource(repo));
    }

    /// <summary><c>https://github.com/{owner/repo}/tree/{commit}/{path}</c> for an old file, so an imported Skill compares
    /// equal to its catalog entry after the move.</summary>
    public static string? DeriveOrigin(string? repo, string? commit, string? path)
    {
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(commit) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string ownerRepo = repo.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase) ? repo[GitHubPrefix.Length..].TrimEnd('/') : repo.Trim('/');
        return $"{GitHubPrefix}{ownerRepo}/tree/{commit}/{path.Trim('/')}";
    }

    /// <summary><c>github.com/&lt;owner&gt;/&lt;repo&gt;</c> for a GitHub repository URL, otherwise null.</summary>
    public static string? DeriveSource(string? repo)
    {
        if (repo is null || !repo.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return "github.com/" + new string([.. repo[GitHubPrefix.Length..].TrimEnd('/').Select(char.ToLowerInvariant)]);
    }
}

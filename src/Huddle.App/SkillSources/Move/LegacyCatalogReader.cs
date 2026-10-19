using System.Text.Json;

namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>The parse rules of the old <c>SkillsCatalogIndex</c>, kept privately for the move (§6.13). The embedded catalog is
/// replaced entirely by <c>{DataDir}/SkillsCatalog.json</c> when that file parses.</summary>
internal static class LegacyCatalogReader
{
    /// <summary>The embedded resource's name.</summary>
    public const string ResourceName = "Skills/SkillsCatalog.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The embedded catalog.</summary>
    /// <exception cref="InvalidOperationException">The resource is missing or malformed: a build defect.</exception>
    public static IReadOnlyList<LegacyCatalogEntry> ReadEmbedded()
    {
        using Stream? stream = typeof(LegacyCatalogReader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using StreamReader reader = new(stream);
        return Parse(reader.ReadToEnd()) ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is malformed.");
    }

    /// <summary>Parses catalog JSON: a list of complete entries, or null.</summary>
    public static IReadOnlyList<LegacyCatalogEntry>? Parse(string json)
    {
        List<Dto?>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<Dto?>>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (raw is null)
        {
            return null;
        }

        List<LegacyCatalogEntry> entries = [];
        foreach (Dto? entry in raw)
        {
            if (entry is not { Id: { Length: > 0 } id, Name: { Length: > 0 } name, Repo: { Length: > 0 } repo, Path: { } path })
            {
                return null;
            }

            entries.Add(new LegacyCatalogEntry(id, name, entry.Description ?? string.Empty, entry.Publisher ?? string.Empty, repo, path, entry.Link ?? string.Empty, entry.Commit ?? string.Empty, entry.License ?? string.Empty, entry.Category ?? string.Empty, entry.SubCategory ?? string.Empty, entry.Requires ?? []));
        }

        return entries;
    }

    /// <summary>The lookup key of a repository URL and path: <c>.git</c> and slashes trimmed, the repository's case ignored.</summary>
    public static string RepoPathKey(string repo, string path)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(path);
        string trimmed = repo.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4].TrimEnd('/');
        }

        return string.Concat(trimmed.ToUpperInvariant(), "\n", path.Trim().Trim('/'));
    }

    /// <summary>The JSON shape, every field nullable.</summary>
    private sealed record Dto(string? Id, string? Name, string? Description, string? Publisher, string? Repo, string? Path, string? Link, string? Commit, string? License, string? Category, string? SubCategory, List<string>? Requires);
}

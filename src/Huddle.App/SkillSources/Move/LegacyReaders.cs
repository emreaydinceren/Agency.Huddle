using System.Text.Json;

namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>One entry of the old <c>SkillsCatalog.json</c>.</summary>
/// <param name="Id">The old id, <c>owner/repo/skill</c>.</param>
/// <param name="Name">The Skill's folder name.</param>
/// <param name="Description">The description.</param>
/// <param name="Publisher">Who publishes it.</param>
/// <param name="Repo">The repository URL.</param>
/// <param name="Path">The folder inside the repository.</param>
/// <param name="Link">A commit-pinned browse URL.</param>
/// <param name="Commit">The commit the entry was generated from.</param>
/// <param name="License">The licence.</param>
/// <param name="Category">The category.</param>
/// <param name="SubCategory">The sub-category.</param>
/// <param name="Requires">What the Skill needs.</param>
internal sealed record LegacyCatalogEntry(
    string Id,
    string Name,
    string Description,
    string Publisher,
    string Repo,
    string Path,
    string Link,
    string Commit,
    string License,
    string Category,
    string SubCategory,
    IReadOnlyList<string> Requires);

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

/// <summary>One of the Human's old Packs.</summary>
/// <param name="File">The file it came from.</param>
/// <param name="Id">The Pack id.</param>
/// <param name="Version">The version.</param>
/// <param name="Title">The title.</param>
/// <param name="AppliesTo">Role ids.</param>
/// <param name="ConsultWhenAdds">The consult phrase.</param>
/// <param name="Skills">The Skills: name, source (<c>owner/repo</c> or <c>builtin</c>) and path.</param>
internal sealed record LegacyPack(string File, string Id, string Version, string Title, IReadOnlyList<string> AppliesTo, string ConsultWhenAdds, IReadOnlyList<(string Name, string Source, string? Path)> Skills);

/// <summary>The parse rules of the old <c>SkillPackStore.Parse</c>, kept privately for the move.</summary>
internal static class LegacyPackReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Parses one old Pack file.</summary>
    /// <returns>The Pack, or the reason it is not one.</returns>
    public static (LegacyPack? Pack, string? Problem) Parse(string text, string file)
    {
        PackDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PackDto>(text, Options);
        }
        catch (JsonException)
        {
            return (null, "not JSON");
        }

        if (dto is null)
        {
            return (null, "the file is empty");
        }

        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Version) || string.IsNullOrWhiteSpace(dto.Title))
        {
            return (null, "id, version and title are required");
        }

        List<(string, string, string?)> skills = [];
        foreach (SkillDto skill in dto.Skills ?? [])
        {
            if (string.IsNullOrWhiteSpace(skill.Name) || string.IsNullOrWhiteSpace(skill.Source))
            {
                return (null, "every Skill needs a name and a source");
            }

            skills.Add((skill.Name, skill.Source, skill.Path));
        }

        return (new LegacyPack(file, dto.Id, dto.Version, dto.Title, dto.AppliesTo ?? [], dto.ConsultWhenAdds ?? string.Empty, skills), null);
    }

    /// <summary>The JSON shape of a pack file.</summary>
    private sealed record PackDto(string? Id, string? Version, string? Title, List<string>? AppliesTo, string? ConsultWhenAdds, List<SkillDto>? Skills);

    /// <summary>The JSON shape of a Skill reference.</summary>
    private sealed record SkillDto(string? Name, string? Source, string? Path);
}

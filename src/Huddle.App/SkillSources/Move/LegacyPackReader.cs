using System.Text.Json;

namespace Agency.Huddle.App.SkillSources.Move;

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

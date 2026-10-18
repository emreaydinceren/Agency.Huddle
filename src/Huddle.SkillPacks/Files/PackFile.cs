using System.Text.Json;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>The outcome of reading one file: the record when its shape is usable, and the findings either way.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="Value">The record, or null when the file's shape is unusable.</param>
/// <param name="Findings">The shape findings.</param>
public sealed record ParseResult<T>(T? Value, IReadOnlyList<Finding> Findings) where T : class
{
    /// <summary>True when <see cref="Value"/> is set.</summary>
    public bool Ok => this.Value is not null;
}

/// <summary>A Skill inside a Pack: its own folder, or a named entry of a catalog. Exactly one of Path and Catalog is set (P009).</summary>
/// <param name="Name">The Skill name.</param>
/// <param name="Path">Where the folder is. Null when <paramref name="Catalog"/> is set. An empty entry means Not downloaded.</param>
/// <param name="Catalog">The id of a catalog that lists this Skill. Null when <paramref name="Path"/> is set.</param>
/// <param name="Origin">The pinned tree URL when the Pack gives its own path; ignored when <paramref name="Catalog"/> is set.</param>
public sealed record PackSkill(string Name, PathEntries? Path, string? Catalog, string? Origin);

/// <summary>A <c>*.skillpack.json</c> file (§7.2).</summary>
/// <param name="SchemaVersion">Always <see cref="KnownSchemaVersion"/> once parsed.</param>
/// <param name="Id">The Pack id.</param>
/// <param name="Version">Free text compared with a Teammate's <c>_packs</c> entry.</param>
/// <param name="Title">Shown on the card.</param>
/// <param name="Source">The Source id.</param>
/// <param name="AppliesTo">Role ids.</param>
/// <param name="ConsultWhenAdds">The phrase added to <c>consult_when</c>.</param>
/// <param name="Skills">The Pack's Skills, in file order.</param>
public sealed record PackFile(
    int SchemaVersion,
    string Id,
    string Version,
    string Title,
    string Source,
    IReadOnlyList<string> AppliesTo,
    string? ConsultWhenAdds,
    IReadOnlyList<PackSkill> Skills)
{
    /// <summary>The only schema version this Huddle reads.</summary>
    public const int KnownSchemaVersion = 1;

    /// <summary>The file name suffix.</summary>
    public const string Suffix = ".skillpack.json";

    /// <summary>Reads a pack file. <paramref name="filePath"/> is the file's full path: its name is checked
    /// against the id (P003) and it is the <see cref="Finding.File"/> of every finding.</summary>
    public static ParseResult<PackFile> Parse(string json, string filePath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(filePath);
        List<Finding> findings = [];
        using JsonDocument? document = JsonFields.TryParse(json);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return Fail("P001", filePath, "The Pack file is not a JSON object.");
        }

        JsonElement root = document.RootElement;
        int? schema = JsonFields.Int(root, "schemaVersion");
        if (schema != KnownSchemaVersion)
        {
            return Fail("P001", filePath, schema is null ? "schemaVersion is missing." : $"schemaVersion {schema} is not one this Huddle knows (1).");
        }

        string? id = JsonFields.String(root, "id");
        string? version = JsonFields.String(root, "version");
        string? title = JsonFields.String(root, "title");
        string? source = JsonFields.String(root, "source");
        JsonElement? skills = JsonFields.Get(root, "skills");
        List<string> missing = [];
        AddIfBlank(missing, "id", id);
        AddIfBlank(missing, "version", version);
        AddIfBlank(missing, "title", title);
        AddIfBlank(missing, "source", source);
        if (skills is not { ValueKind: JsonValueKind.Array })
        {
            missing.Add("skills");
        }

        if (missing.Count > 0 || id is null || version is null || title is null || source is null || skills is not { ValueKind: JsonValueKind.Array } skillArray)
        {
            return Fail("P002", filePath, $"Required field missing or not text: {string.Join(", ", missing)}.");
        }

        if (!Names.IsId(id))
        {
            return Fail("P003", filePath, $"id '{id}' must be lowercase letters, digits and single hyphens.");
        }

        string fileName = System.IO.Path.GetFileName(filePath);
        if (!string.Equals(fileName, id + Suffix, StringComparison.Ordinal))
        {
            return Fail("P003", filePath, $"The file is named '{fileName}'; a Pack with id '{id}' must be '{id}{Suffix}'.");
        }

        if (JsonFields.IsWrongType(root, "appliesTo", JsonValueKind.Array) || JsonFields.IsWrongType(root, "consultWhenAdds", JsonValueKind.String))
        {
            findings.Add(Finding.Error("P002", filePath, "appliesTo must be a list of role ids and consultWhenAdds a string."));
        }

        List<PackSkill> packSkills = ReadSkills(skillArray, filePath, findings);
        PackFile pack = new(
            KnownSchemaVersion,
            id,
            version,
            title,
            source,
            JsonFields.Strings(root, "appliesTo"),
            JsonFields.String(root, "consultWhenAdds"),
            packSkills);
        return new ParseResult<PackFile>(pack, findings);
    }

    /// <summary>Reads the <c>skills</c> list; a bad Skill is a finding and is dropped, the rest load.</summary>
    private static List<PackSkill> ReadSkills(JsonElement skills, string filePath, List<Finding> findings)
    {
        List<PackSkill> result = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        int position = 0;
        foreach (JsonElement item in skills.EnumerateArray())
        {
            position++;
            string? name = item.ValueKind == JsonValueKind.Object ? JsonFields.String(item, "name") : null;
            if (name is null || !Names.IsSkillName(name))
            {
                findings.Add(Finding.Error("P002", filePath, $"Skill {position}: name '{name}' is missing or breaks the Skill name rule."));
                continue;
            }

            JsonElement? pathValue = JsonFields.Get(item, "path");
            string? catalog = JsonFields.String(item, "catalog");
            bool hasPath = pathValue is { ValueKind: not JsonValueKind.Null };
            bool hasCatalog = !string.IsNullOrWhiteSpace(catalog);
            if (hasPath == hasCatalog)
            {
                findings.Add(Finding.Error("P009", filePath, hasPath
                    ? $"Skill \"{name}\" gives both a path and a catalog."
                    : $"Skill \"{name}\" gives neither a path nor a catalog."));
                continue;
            }

            PathEntries? path = null;
            if (pathValue is { ValueKind: not JsonValueKind.Null } pathElement)
            {
                path = PathEntries.FromJson(pathElement);
                if (path is null)
                {
                    findings.Add(Finding.Error("P002", filePath, $"Skill \"{name}\": path must be a string or an object of strings."));
                    continue;
                }
            }

            if (!names.Add(name))
            {
                findings.Add(Finding.Error("P008", filePath, $"Skill \"{name}\" is listed twice."));
                continue;
            }

            result.Add(new PackSkill(name, path, hasCatalog ? catalog?.Trim() : null, JsonFields.String(item, "origin")));
        }

        return result;
    }

    /// <summary>Adds <paramref name="field"/> when <paramref name="value"/> is blank.</summary>
    private static void AddIfBlank(List<string> missing, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(field);
        }
    }

    /// <summary>A failed parse with one error.</summary>
    private static ParseResult<PackFile> Fail(string code, string filePath, string message)
    {
        return new ParseResult<PackFile>(null, [Finding.Error(code, filePath, message)]);
    }
}

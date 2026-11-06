using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Reading;
using System.Text.Json;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>A <c>*.skillcatalog.json</c> file (§7.2).</summary>
/// <param name="SchemaVersion">Always 1 once parsed.</param>
/// <param name="Id">The catalog id.</param>
/// <param name="Title">For people; the id when absent.</param>
/// <param name="Source">The Source id.</param>
/// <param name="Skills">The entries, in file order.</param>
public sealed record CatalogFile(
    int SchemaVersion,
    string Id,
    string Title,
    string Source,
    IReadOnlyList<CatalogEntry> Skills)
{
    /// <summary>The file name suffix.</summary>
    public const string Suffix = ".skillcatalog.json";

    /// <summary>The entry named <paramref name="name"/>, or null.</summary>
    public CatalogEntry? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return this.Skills.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));
    }

    /// <summary>Reads a catalog file; <paramref name="filePath"/> is its full path. A bad entry is D002 and is
    /// dropped; the rest load.</summary>
    public static ParseResult<CatalogFile> Parse(string json, string filePath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(filePath);
        using JsonDocument? document = JsonFields.TryParse(json);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return Fail(filePath, "The catalog file is not a JSON object.");
        }

        JsonElement root = document.RootElement;
        int? schema = JsonFields.Int(root, "schemaVersion");
        if (schema != PackFile.KnownSchemaVersion)
        {
            return Fail(filePath, schema is null ? "schemaVersion is missing." : $"schemaVersion {schema} is not one this Huddle knows (1).");
        }

        string? id = JsonFields.String(root, "id");
        string? source = JsonFields.String(root, "source");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(source) || JsonFields.Get(root, "skills") is not { ValueKind: JsonValueKind.Array } skills)
        {
            return Fail(filePath, "id, source and skills are required.");
        }

        string fileName = System.IO.Path.GetFileName(filePath);
        if (!Names.IsId(id) || !string.Equals(fileName, id + Suffix, StringComparison.Ordinal))
        {
            return Fail(filePath, $"The catalog id '{id}' must follow the id rule and match the file name '{fileName}'.");
        }

        List<Finding> findings = [];
        List<CatalogEntry> entries = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        int position = 0;
        foreach (JsonElement item in skills.EnumerateArray())
        {
            position++;
            CatalogEntry? entry = ReadEntry(item, position, filePath, names, findings);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        string title = JsonFields.String(root, "title") is { Length: > 0 } given ? given : id;
        return new ParseResult<CatalogFile>(new CatalogFile(PackFile.KnownSchemaVersion, id, title, source, entries), findings);
    }

    /// <summary>Reads one entry, or adds a D002 finding and returns null.</summary>
    private static CatalogEntry? ReadEntry(JsonElement item, int position, string filePath, HashSet<string> names, List<Finding> findings)
    {
        string? name = item.ValueKind == JsonValueKind.Object ? JsonFields.String(item, "name") : null;
        string? description = item.ValueKind == JsonValueKind.Object ? JsonFields.String(item, "description") : null;
        string? origin = item.ValueKind == JsonValueKind.Object ? JsonFields.String(item, "origin") : null;
        string label = name is null ? $"Entry {position}" : $"Entry \"{name}\"";
        if (name is null || !Names.IsSkillName(name))
        {
            findings.Add(Finding.Error("D002", filePath, $"{label}: the name is missing or breaks the Skill name rule."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(origin))
        {
            findings.Add(Finding.Error("D002", filePath, $"{label}: description and origin are required."));
            return null;
        }

        PathEntries? path = JsonFields.Get(item, "path") is { } pathValue ? PathEntries.FromJson(pathValue) : PathEntries.Empty;
        if (path is null)
        {
            findings.Add(Finding.Error("D002", filePath, $"{label}: path must be a string or an object of strings."));
            return null;
        }

        if (!names.Add(name))
        {
            findings.Add(Finding.Error("D002", filePath, $"{label} is listed twice."));
            return null;
        }

        return new CatalogEntry(
            name,
            description.Trim(),
            origin.Trim(),
            JsonFields.String(item, "publisher"),
            JsonFields.String(item, "license"),
            JsonFields.String(item, "category"),
            JsonFields.String(item, "subCategory"),
            JsonFields.Strings(item, "requires"),
            path,
            JsonFields.String(item, "sha256") is { Length: > 0 } pinned ? pinned.Trim() : null);
    }

    /// <summary>A failed parse with one D001.</summary>
    private static ParseResult<CatalogFile> Fail(string filePath, string message)
    {
        return new ParseResult<CatalogFile>(null, [Finding.Error("D001", filePath, message)]);
    }
}

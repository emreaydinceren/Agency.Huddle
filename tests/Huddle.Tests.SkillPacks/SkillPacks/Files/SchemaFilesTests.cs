using System.Text.Json;
using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.Tests.SkillPacks.Files;

/// <summary>Pins the three embedded JSON Schema files: each parses, and each names as required exactly the fields
/// the reader refuses a file without (the "step" test of §6.4).</summary>
public sealed class SchemaFilesTests
{
    /// <summary>Each schema is embedded and parses as JSON; an unknown name gives null.</summary>
    [Fact]
    public void Read_EachSchema_ParsesAsJson()
    {
        foreach (string name in SchemaFiles.Names)
        {
            string? text = SchemaFiles.Read(name);

            Assert.NotNull(text);
            using JsonDocument document = JsonDocument.Parse(text);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }

        Assert.Null(SchemaFiles.Read("agent"));
    }

    /// <summary>The pack schema's required list is the set of fields whose absence the reader rejects.</summary>
    [Fact]
    public void PackSchema_Required_MatchesReader()
    {
        string[] required = Required("pack");
        const string full = """{ "schemaVersion": 1, "id": "a", "version": "1", "title": "A", "source": "local/a", "skills": [{ "name": "x", "path": "" }] }""";
        string path = InMemoryFileReader.At("a.skillpack.json");

        Assert.True(PackFile.Parse(full, path).Ok);
        Assert.All(required, field => Assert.False(PackFile.Parse(Without(full, field), path).Ok, field));
        Assert.True(PackFile.Parse(Without(full, "appliesTo"), path).Ok);
    }

    /// <summary>The catalog schema's required list is the set of fields whose absence the reader rejects.</summary>
    [Fact]
    public void CatalogSchema_Required_MatchesReader()
    {
        string[] required = Required("catalog");
        const string full = """{ "schemaVersion": 1, "id": "a", "title": "A", "source": "local/a", "skills": [] }""";
        string path = InMemoryFileReader.At("a.skillcatalog.json");

        Assert.True(CatalogFile.Parse(full, path).Ok);
        Assert.All(required, field => Assert.False(CatalogFile.Parse(Without(full, field), path).Ok, field));
        Assert.True(CatalogFile.Parse(Without(full, "title"), path).Ok);
    }

    /// <summary>The source schema's required list is the set of fields whose absence the reader rejects.</summary>
    [Fact]
    public void SourceSchema_Required_MatchesReader()
    {
        string[] required = Required("source");
        const string full = """{ "schemaVersion": 1, "kind": "local", "fetchedAt": "2026-10-17T16:40:00+02:00", "fetchedBy": "Me", "signIn": "none", "status": "active", "ref": "main" }""";
        string path = InMemoryFileReader.At("source.json");

        Assert.True(SourceFile.Parse(full, path).Ok);
        Assert.All(required, field => Assert.False(SourceFile.Parse(Without(full, field), path).Ok, field));
        Assert.True(SourceFile.Parse(Without(full, "ref"), path).Ok);
    }

    /// <summary>The top-level <c>required</c> list of a schema.</summary>
    private static string[] Required(string name)
    {
        using JsonDocument document = JsonDocument.Parse(SchemaFiles.Read(name) ?? "{}");
        return [.. document.RootElement.GetProperty("required").EnumerateArray().Select(static item => item.GetString() ?? string.Empty)];
    }

    /// <summary>The JSON object <paramref name="json"/> without its top-level property <paramref name="field"/>.</summary>
    private static string Without(string json, string field)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Dictionary<string, JsonElement> properties = document.RootElement.EnumerateObject()
            .Where(property => !string.Equals(property.Name, field, StringComparison.Ordinal))
            .ToDictionary(static property => property.Name, static property => property.Value.Clone(), StringComparer.Ordinal);
        return JsonSerializer.Serialize(properties);
    }
}

using System.Text.Json;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Themes.Skins;

/// <summary>
/// Pins <c>skin.schema.json</c> to the skin model and the token registry, and every skin file to that schema by name.
/// </summary>
public sealed class SkinSchemaTests
{
    /// <summary>The environment variable that makes <see cref="Skins_SchemaIsCurrent"/> rewrite the schema instead of failing.</summary>
    private const string RegenerateVariable = "HUDDLE_REGEN_SKIN_SCHEMA";

    private static string SkinFolder => CssSource.RepoPath("src", "Huddle.App", "Themes", "Skins");

    /// <summary>
    /// The schema on disk equals the one generated from the model and the registry. When it does not, the failure
    /// message carries the command that rewrites it.
    /// </summary>
    [Fact]
    public void Skins_SchemaIsCurrent()
    {
        string path = Path.Combine(SkinFolder, "skin.schema.json");
        string expected = SkinSchemaGenerator.Generate();

        if (string.Equals(Environment.GetEnvironmentVariable(RegenerateVariable), "1", StringComparison.Ordinal))
        {
            System.IO.File.WriteAllText(path, expected.Replace("\n", "\r\n", StringComparison.Ordinal));
        }

        string actual = System.IO.File.Exists(path)
            ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal)
            : string.Empty;
        Assert.True(
            string.Equals(expected, actual, StringComparison.Ordinal),
            "skin.schema.json is out of date: regenerate the schema with "
            + "`$env:HUDDLE_REGEN_SKIN_SCHEMA='1'; pwsh -NoProfile -File agents/scripts/Run-Tests.ps1 -FilterClass '*SkinSchemaTests' -Label schema` "
            + "and review the diff.");
    }

    /// <summary>The generated schema is valid JSON, and every skin file points at it with <c>$schema</c>.</summary>
    [Fact]
    public void Skins_ReferenceTheSchema()
    {
        using JsonDocument schema = JsonDocument.Parse(SkinSchemaGenerator.Generate());
        Assert.Equal("object", schema.RootElement.GetProperty("type").GetString());

        string[] files = Directory.GetFiles(SkinFolder, "*.json").Where(static file => !file.EndsWith("skin.schema.json", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            using JsonDocument skin = JsonDocument.Parse(System.IO.File.ReadAllText(file));
            Assert.Equal("skin.schema.json", skin.RootElement.GetProperty("$schema").GetString());
        }
    }
}

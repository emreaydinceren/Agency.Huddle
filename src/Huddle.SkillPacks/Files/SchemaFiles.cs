namespace Agency.Huddle.SkillPacks.Files;

/// <summary>The JSON Schema files embedded in the library, which <c>huddle-packs schema</c> prints.</summary>
public static class SchemaFiles
{
    /// <summary>The schema names, in the order <c>huddle-packs schema</c> lists them.</summary>
    public static IReadOnlyList<string> Names { get; } = ["pack", "catalog", "source"];

    /// <summary>The text of the schema called <paramref name="name"/>, or null for an unknown name.</summary>
    public static string? Read(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Names.Contains(name, StringComparer.Ordinal))
        {
            return null;
        }

        using Stream? stream = typeof(SchemaFiles).Assembly.GetManifestResourceStream($"Agency.Huddle.SkillPacks.Schemas.{name}.schema.json");
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}

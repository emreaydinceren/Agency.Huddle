using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.Packs.Commands;

/// <summary><c>huddle-packs schema pack|catalog|source</c>: the embedded JSON Schema.</summary>
internal static class SchemaCommand
{
    /// <summary>Prints the schema text, unchanged, then a newline.</summary>
    public static int Run(string kind, TextWriter output, TextWriter error)
    {
        string? text = SchemaFiles.Read(kind);
        if (text is null)
        {
            error.WriteLine($"Unknown schema {kind}.");
            return 2;
        }

        output.WriteLine(text.TrimEnd('\r', '\n'));
        return 0;
    }
}

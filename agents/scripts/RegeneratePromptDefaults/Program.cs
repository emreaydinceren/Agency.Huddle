using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Agency.Huddle.Contracts;

namespace HuddleConversationTools.RegeneratePromptDefaults;

/// <summary>
/// Regenerates <c>src/Huddle.App/prompts.default.json</c> from the current contents of
/// <c>Agency.Huddle.App.Prompts.PromptCatalog.All</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>PromptCatalog</c> and <c>PromptDefinition</c> are <c>internal</c> to <c>Huddle.App</c>, whose only
/// <c>InternalsVisibleTo</c> grant is to <c>Huddle.Tests</c> (see <c>Huddle.App.csproj</c>). This helper
/// is deliberately a separate, throwaway assembly under <c>agents/scripts/</c> rather than a second
/// internals grant added to that shared, root-owned <c>.csproj</c>, so it reaches the catalog through
/// reflection on the already-built <c>Huddle.App.dll</c> instead.
/// </para>
/// <para>
/// The serialisation settings mirror <c>PromptStore.IndentedJsonOptions</c> exactly (see
/// <c>src/Huddle.App/Prompts/PromptStore.cs</c>, around line 86): <see cref="ProtocolJson.Options"/>
/// copied with <c>WriteIndented = true</c> and <c>Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping</c>,
/// so this file round-trips through the same serializer the app itself uses to write it.
/// </para>
/// </remarks>
internal static class Program
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(ProtocolJson.Options)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static void Main(string[] args)
    {
        string repoRoot = args.Length > 0 ? args[0] : FindRepoRoot(AppContext.BaseDirectory);
        string outputPath = Path.Combine(repoRoot, "src", "Huddle.App", "prompts.default.json");

        Assembly appAssembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Huddle.App.dll"));
        Type catalogType = appAssembly.GetType("Agency.Huddle.App.Prompts.PromptCatalog", throwOnError: true)
            ?? throw new InvalidOperationException("PromptCatalog type was not found via reflection.");

        PropertyInfo allProperty = catalogType.GetProperty("All", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("PromptCatalog.All was not found via reflection.");

        object allValue = allProperty.GetValue(null)
            ?? throw new InvalidOperationException("PromptCatalog.All returned null.");

        var entries = new List<(string Key, string Default)>();
        PropertyInfo? keyProperty = null;
        PropertyInfo? defaultProperty = null;

        foreach (object? prompt in (System.Collections.IEnumerable)allValue)
        {
            if (prompt is null)
            {
                continue;
            }

            keyProperty ??= prompt.GetType().GetProperty("Key")
                ?? throw new InvalidOperationException("PromptDefinition.Key was not found via reflection.");
            defaultProperty ??= prompt.GetType().GetProperty("Default")
                ?? throw new InvalidOperationException("PromptDefinition.Default was not found via reflection.");

            string key = keyProperty.GetValue(prompt) as string
                ?? throw new InvalidOperationException("PromptDefinition.Key returned a non-string or null value.");
            string defaultText = defaultProperty.GetValue(prompt) as string
                ?? throw new InvalidOperationException("PromptDefinition.Default returned a non-string or null value.");
            entries.Add((key, defaultText));
        }

        // Build a Dictionary<string, string> (not a Dictionary<string, object>) so serialization matches
        // exactly what PromptDefaultsFileTests deserializes into.
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string defaultText) in entries)
        {
            // Normalise to '\n' first - PromptDefinition.Default already normalises on construction, but
            // do it here too so this generator does not depend on that invariant holding.
            dictionary[key] = defaultText.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        string json = JsonSerializer.Serialize(dictionary, IndentedJsonOptions);

        // JsonSerializer only ever emits '\n' for WriteIndented output; convert to CRLF explicitly so we
        // do not depend on that. Doing replace-then-replace (as Fix-Crlf.ps1 does) avoids '\r\r\n'.
        string crlf = json.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        // Match the existing file's conventions exactly: no BOM, no trailing newline after the final '}'.
        File.WriteAllText(outputPath, crlf, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Wrote {dictionary.Count} prompt defaults to {outputPath}");
    }

    /// <summary>Walks up from <paramref name="startDirectory"/> until a directory containing <c>Huddle.slnx</c> is found.</summary>
    /// <param name="startDirectory">The directory to start searching from.</param>
    /// <returns>The repository root directory.</returns>
    private static string FindRepoRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException($"Could not locate Huddle.slnx by walking up from '{startDirectory}'.");
        }

        return directory.FullName;
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Guide;

/// <summary>
/// The catalog of built-in diagrams (ADR-0087): <c>Guide/guide.json</c> and one <c>Guide/&lt;id&gt;.svg</c> per entry,
/// all embedded in this assembly and read once. It is fixed data shipped with the release, so it is static: nothing
/// at run time can add, change or remove a diagram. A catalog entry with a bad id, a duplicate id or no SVG throws
/// when the catalog is first read, which the catalog tests do on every build.
/// </summary>
internal static partial class GuideDiagrams
{
    /// <summary>The prefix of an image address that names a diagram, as in <c>guide:layers</c>.</summary>
    internal const string Scheme = "guide:";

    /// <summary>The manifest name of the catalog file.</summary>
    private const string CatalogResource = "Guide/guide.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

    private static readonly Lazy<IReadOnlyDictionary<string, GuideDiagram>> Catalog = new(GuideDiagrams.Load);

    /// <summary>Every diagram, keyed by id (ordinal).</summary>
    internal static IReadOnlyDictionary<string, GuideDiagram> All => GuideDiagrams.Catalog.Value;

    /// <summary>Looks a diagram up by its id.</summary>
    /// <param name="id">The id after <see cref="Scheme"/>.</param>
    /// <param name="diagram">The diagram, when the catalog holds <paramref name="id"/>.</param>
    /// <returns>Whether the catalog holds <paramref name="id"/>.</returns>
    internal static bool TryGet(string id, [MaybeNullWhen(false)] out GuideDiagram diagram) =>
        GuideDiagrams.Catalog.Value.TryGetValue(id, out diagram);

    /// <summary>Reads the catalog and every diagram it names from the assembly.</summary>
    /// <returns>The diagrams, keyed by id.</returns>
    /// <exception cref="InvalidOperationException">An entry is malformed, repeated or has no SVG.</exception>
    private static Dictionary<string, GuideDiagram> Load()
    {
        Assembly assembly = typeof(GuideDiagrams).Assembly;
        string json = EmbeddedResources.ReadText(assembly, GuideDiagrams.CatalogResource);
        List<Entry> entries = JsonSerializer.Deserialize<List<Entry>>(json, GuideDiagrams.JsonOptions)
            ?? throw new InvalidOperationException($"{GuideDiagrams.CatalogResource} is empty.");

        Dictionary<string, GuideDiagram> diagrams = new(StringComparer.Ordinal);
        foreach (Entry entry in entries)
        {
            if (!GuideDiagrams.IdPattern().IsMatch(entry.Id))
            {
                throw new InvalidOperationException($"Guide diagram id '{entry.Id}' must be lower-case words joined by '-'.");
            }

            string svg = EmbeddedResources.ReadText(assembly, $"Guide/{entry.Id}.svg").Trim();
            if (!diagrams.TryAdd(entry.Id, new GuideDiagram(entry.Id, entry.Title, entry.ShowWhen, svg)))
            {
                throw new InvalidOperationException($"Guide diagram id '{entry.Id}' is listed twice.");
            }
        }

        return diagrams;
    }

    /// <summary>A diagram id: lower-case letters and digits, in words joined by single hyphens.</summary>
    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>One entry of <c>guide.json</c>.</summary>
    /// <param name="Id">The diagram's id.</param>
    /// <param name="Title">What it shows.</param>
    /// <param name="ShowWhen">When to show it.</param>
    private sealed record Entry(
        [property: JsonPropertyName("id"), JsonRequired] string Id,
        [property: JsonPropertyName("title"), JsonRequired] string Title,
        [property: JsonPropertyName("showWhen"), JsonRequired] string ShowWhen);
}

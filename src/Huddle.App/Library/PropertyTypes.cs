using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Library;

/// <summary>
/// Reads one Library Root's types file (front matter properties spec D-2 step 1, D-10, D-12): the JSON file in the root's settings folder
/// that assigns each key a type. It is read through <see cref="LibraryPathResolver"/>, which does not apply the hidden-folder rule, so the
/// folder stays hidden from the tree and the link index. The parsed types are cached per root and dropped when the file's length or
/// last-write time changes, checked each time a note is drawn. A missing, oversized, unreadable or invalid file gives no types and no
/// exception. Nothing here writes the file.
/// </summary>
/// <param name="resolver">Resolves the types file inside a root.</param>
/// <param name="options">Holds <see cref="LibraryOptions.PropertyTypesFile"/>.</param>
internal sealed class PropertyTypes(LibraryPathResolver resolver, IOptions<TeamOptions> options)
{
    /// <summary>The largest types file that is read.</summary>
    internal const long MaxBytes = 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, NotePropertyKind> NoTypes =
        new Dictionary<string, NotePropertyKind>(StringComparer.OrdinalIgnoreCase);

    private readonly LibraryPathResolver resolver = resolver;
    private readonly IOptions<TeamOptions> options = options;
    private readonly Lock gate = new();
    private readonly Dictionary<string, Cached> cache = new(StringComparer.Ordinal);

    /// <summary>The types assigned in <paramref name="note"/>'s root, keyed case-insensitively; empty when there are none.</summary>
    /// <param name="note">A note in the root, whose draw asks for the types.</param>
    internal IReadOnlyDictionary<string, NotePropertyKind> For(LibraryPath note)
    {
        ArgumentNullException.ThrowIfNull(note);

        string rootId = note.Root.Id;
        string setting = this.options.Value.Library.PropertyTypesFile;
        if (string.IsNullOrWhiteSpace(setting)
            || !this.resolver.TryResolve(rootId, setting.Trim(), out LibraryPath? file, out _)
            || !TryStat(file.FullPath, out long length, out long writeTicks))
        {
            this.Forget(rootId);
            return NoTypes;
        }

        lock (this.gate)
        {
            if (this.cache.TryGetValue(rootId, out Cached? cached) && cached.Length == length && cached.WriteTicks == writeTicks)
            {
                return cached.Types;
            }
        }

        IReadOnlyDictionary<string, NotePropertyKind>? types = length > MaxBytes ? NoTypes : Load(file.FullPath);
        if (types is null)
        {
            // The read itself failed (a locked file, a permission error): nothing is cached, so the next draw tries again.
            this.Forget(rootId);
            return NoTypes;
        }

        lock (this.gate)
        {
            this.cache[rootId] = new Cached(length, writeTicks, types);
        }

        return types;
    }

    private void Forget(string rootId)
    {
        lock (this.gate)
        {
            _ = this.cache.Remove(rootId);
        }
    }

    /// <summary>Reads the file's length and last-write time; <see langword="false"/> when it is not a file that can be statted.</summary>
    private static bool TryStat(string fullPath, out long length, out long writeTicks)
    {
        length = 0;
        writeTicks = 0;
        try
        {
            FileInfo info = new(fullPath);
            if (!info.Exists)
            {
                return false;
            }

            length = info.Length;
            writeTicks = info.LastWriteTimeUtc.Ticks;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads and maps the file. Text that is not valid JSON gives no types; a read that fails gives <see langword="null"/>, so it is not remembered (D-10).</summary>
    private static IReadOnlyDictionary<string, NotePropertyKind>? Load(string fullPath)
    {
        try
        {
            return Parse(File.ReadAllText(fullPath));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Maps the text of a types file; text that is not valid JSON, or not the expected shape, gives no types.</summary>
    /// <param name="json">The file's text.</param>
    internal static IReadOnlyDictionary<string, NotePropertyKind> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return Map(document.RootElement);
        }
        catch (JsonException)
        {
            return NoTypes;
        }
    }

    /// <summary>Maps <c>{ "types": { "key": "name" } }</c>; an unknown name is left out so its key falls through to the next step of D-2.</summary>
    private static IReadOnlyDictionary<string, NotePropertyKind> Map(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("types", out JsonElement types)
            || types.ValueKind != JsonValueKind.Object)
        {
            return NoTypes;
        }

        Dictionary<string, NotePropertyKind> mapped = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty entry in types.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String && TryMapName(entry.Value.GetString(), out NotePropertyKind kind))
            {
                mapped[entry.Name] = kind;
            }
        }

        return mapped;
    }

    /// <summary>The types-file name for each type (D-2's table).</summary>
    private static bool TryMapName(string? name, out NotePropertyKind kind)
    {
        NotePropertyKind? found = name?.ToUpperInvariant() switch
        {
            "TEXT" => NotePropertyKind.Text,
            "MULTITEXT" => NotePropertyKind.List,
            "NUMBER" => NotePropertyKind.Number,
            "CHECKBOX" => NotePropertyKind.Checkbox,
            "DATE" => NotePropertyKind.Date,
            "DATETIME" => NotePropertyKind.DateTime,
            "ALIASES" => NotePropertyKind.Aliases,
            "TAGS" => NotePropertyKind.Tags,
            _ => null,
        };
        kind = found.GetValueOrDefault();
        return found is not null;
    }

    private sealed record Cached(long Length, long WriteTicks, IReadOnlyDictionary<string, NotePropertyKind> Types);
}

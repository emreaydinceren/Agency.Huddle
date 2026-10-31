using Agency.Huddle.App.Library;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agency.Huddle.App.SkillSources;

/// <summary><c>SkillSources/hidden.json</c> (§6.12.4, §7.2): items hidden from lists, shared by every Huddle on the folder.
/// The hide and unhide commands are its only writers; a malformed file is read as empty and never overwritten.</summary>
internal sealed class HiddenStore(SkillSourcesSettings settings)
{
    /// <summary>The file's name in the Skill Sources folder.</summary>
    public const string FileName = "hidden.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Lock gate = new();

    /// <summary>Raised after a write.</summary>
    public event Action? Changed;

    /// <summary>The full path of <c>hidden.json</c>.</summary>
    public string FilePath => Path.Combine(settings.Dir, FileName);

    /// <summary>Reads the file now; a missing file hides nothing.</summary>
    public HiddenSet Read()
    {
        if (!File.Exists(this.FilePath))
        {
            return HiddenSet.None;
        }

        try
        {
            if (JsonNode.Parse(File.ReadAllText(this.FilePath)) is not JsonObject root)
            {
                return HiddenSet.None with { Malformed = true };
            }

            return new HiddenSet(
                new HashSet<string>(Strings(root["packs"]), StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(Strings(root["skills"]), StringComparer.Ordinal),
                false);
        }
        catch (JsonException)
        {
            return HiddenSet.None with { Malformed = true };
        }
        catch (IOException)
        {
            return HiddenSet.None with { Malformed = true };
        }
    }

    /// <summary>Adds <paramref name="name"/> to the list of <paramref name="kind"/>.</summary>
    public HiddenWriteResult Hide(HiddenKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return this.Change(kind, name, add: true);
    }

    /// <summary>Removes <paramref name="name"/> from the list of <paramref name="kind"/>.</summary>
    public HiddenWriteResult Unhide(HiddenKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return this.Change(kind, name, add: false);
    }

    /// <summary>Applies one change and writes the whole file through a temporary file and a move.</summary>
    private HiddenWriteResult Change(HiddenKind kind, string name, bool add)
    {
        HiddenWriteResult result;
        lock (this.gate)
        {
            HiddenSet current = this.Read();
            if (current.Malformed)
            {
                return HiddenWriteResult.Refused;
            }

            HashSet<string> packs = new(current.Packs, StringComparer.OrdinalIgnoreCase);
            HashSet<string> skills = new(current.Skills, StringComparer.Ordinal);
            HashSet<string> target = kind == HiddenKind.Pack ? packs : skills;
            bool changed = add ? target.Add(name) : target.Remove(name);
            if (!changed)
            {
                return HiddenWriteResult.Unchanged;
            }

            JsonObject root = new()
            {
                ["schemaVersion"] = 1,
                ["packs"] = new JsonArray([.. packs.Order(StringComparer.Ordinal).Select(static id => (JsonNode?)JsonValue.Create(id))]),
                ["skills"] = new JsonArray([.. skills.Order(StringComparer.Ordinal).Select(static id => (JsonNode?)JsonValue.Create(id))]),
            };
            Directory.CreateDirectory(settings.Dir);
            AtomicFile.WriteText(this.FilePath, root.ToJsonString(WriteOptions) + "\n");
            result = HiddenWriteResult.Written;
        }

        this.Changed?.Invoke();
        return result;
    }

    /// <summary>The strings of a JSON array node.</summary>
    private static IEnumerable<string> Strings(JsonNode? node)
    {
        return node is JsonArray array
            ? [.. array.OfType<JsonValue>().Select(static item => item.TryGetValue(out string? text) ? text : null).OfType<string>()]
            : [];
    }
}

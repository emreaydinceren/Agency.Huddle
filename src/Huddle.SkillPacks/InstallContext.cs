using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Files;

namespace Agency.Huddle.SkillPacks;

/// <summary><c>install-context.json</c> (§6.7, §7.2): the facts about one Huddle that <c>huddle-packs</c> needs. Huddle's
/// <c>InstallFileWriter</c> writes it with <see cref="ToJson"/>; <c>huddle-packs</c> reads it with <see cref="Parse"/>, so the
/// two cannot disagree on a key.</summary>
/// <param name="SchemaVersion">The file's schema version.</param>
/// <param name="HuddleVersion">The Huddle that wrote it.</param>
/// <param name="Computer">This computer's name for per-computer paths.</param>
/// <param name="DataDir">Huddle's data folder, absolute.</param>
/// <param name="SkillSourcesDir">The Skill Sources folder, absolute.</param>
/// <param name="SkillsDir">The folder holding the copies, absolute.</param>
/// <param name="Roles">Every role a Pack may name.</param>
/// <param name="Installed">Every Skill in <c>Skills/</c> and each built-in default.</param>
/// <param name="AppTools">The App Tool names only built-in Skills may grant, sorted ordinally.</param>
/// <param name="Written">When it was written, with its offset.</param>
public sealed record InstallContext(
    int SchemaVersion,
    string HuddleVersion,
    string Computer,
    string DataDir,
    string SkillSourcesDir,
    string SkillsDir,
    IReadOnlyList<RoleInfo> Roles,
    IReadOnlyList<InstalledSkill> Installed,
    IReadOnlyList<string> AppTools,
    DateTimeOffset Written)
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "install-context.json";

    /// <summary>The schema version this code reads and writes.</summary>
    public const int KnownSchemaVersion = 1;

    /// <summary>The format of <c>written</c>.</summary>
    public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] RequiredFields = ["schemaVersion", "huddleVersion", "computer", "dataDir", "skillSourcesDir", "skillsDir", "roles", "installed", "appTools", "written"];

    /// <summary>Reads the file's text.</summary>
    /// <returns>The context, or the reason it cannot be used.</returns>
    public static InstallContextReadResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using JsonDocument? document = JsonFields.TryParse(json);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return InstallContextReadResult.Invalid("not valid JSON");
        }

        JsonElement root = document.RootElement;
        foreach (string field in RequiredFields)
        {
            if (JsonFields.Get(root, field) is not { ValueKind: not JsonValueKind.Null })
            {
                return InstallContextReadResult.Invalid($"missing {field}");
            }
        }

        int? schema = JsonFields.Int(root, "schemaVersion");
        if (schema is not { } version)
        {
            return InstallContextReadResult.Invalid("schemaVersion is not a number");
        }

        if (version > KnownSchemaVersion)
        {
            return InstallContextReadResult.Newer(version);
        }

        if (!DateTimeOffset.TryParse(JsonFields.String(root, "written"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset written))
        {
            return InstallContextReadResult.Invalid("written is not a time");
        }

        List<RoleInfo> roles = [];
        foreach (JsonElement role in Array(root, "roles"))
        {
            string? id = JsonFields.String(role, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                return InstallContextReadResult.Invalid("missing roles[].id");
            }

            roles.Add(new RoleInfo(id, JsonFields.String(role, "title") ?? id, JsonFields.String(role, "description") ?? string.Empty, JsonFields.Strings(role, "suggestedPacks")));
        }

        List<InstalledSkill> installed = [];
        foreach (JsonElement skill in Array(root, "installed"))
        {
            string? name = JsonFields.String(skill, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return InstallContextReadResult.Invalid("missing installed[].name");
            }

            bool builtIn = JsonFields.Get(skill, "builtIn") is { ValueKind: JsonValueKind.True };
            installed.Add(new InstalledSkill(name, JsonFields.String(skill, "origin"), JsonFields.String(skill, "sha256"), builtIn));
        }

        string? Text(string field)
        {
            return JsonFields.String(root, field);
        }

        if (Text("huddleVersion") is not { } huddle || Text("computer") is not { } computer || Text("dataDir") is not { } dataDir
            || Text("skillSourcesDir") is not { } sources || Text("skillsDir") is not { } skillsDir)
        {
            return InstallContextReadResult.Invalid("a path or version is not text");
        }

        return InstallContextReadResult.Read(new InstallContext(version, huddle, computer, dataDir, sources, skillsDir, roles, installed, JsonFields.Strings(root, "appTools"), written));
    }

    /// <summary>The file's text: camelCase, indented two spaces, LF line endings, every field written.</summary>
    public string ToJson()
    {
        JsonObject root = new()
        {
            ["schemaVersion"] = this.SchemaVersion,
            ["huddleVersion"] = this.HuddleVersion,
            ["computer"] = this.Computer,
            ["dataDir"] = this.DataDir,
            ["skillSourcesDir"] = this.SkillSourcesDir,
            ["skillsDir"] = this.SkillsDir,
            ["roles"] = new JsonArray([.. this.Roles.Select(static role => (JsonNode)new JsonObject
            {
                ["id"] = role.Id,
                ["title"] = role.Title,
                ["description"] = role.Description,
                ["suggestedPacks"] = new JsonArray([.. role.SuggestedPacks.Select(static id => (JsonNode?)JsonValue.Create(id))]),
            })]),
            ["installed"] = new JsonArray([.. this.Installed.Select(static skill => (JsonNode)new JsonObject
            {
                ["name"] = skill.Name,
                ["origin"] = skill.Origin,
                ["sha256"] = skill.Sha256,
                ["builtIn"] = skill.BuiltIn,
            })]),
            ["appTools"] = new JsonArray([.. this.AppTools.Select(static tool => (JsonNode?)JsonValue.Create(tool))]),
            ["written"] = this.Written.ToString(TimeFormat, CultureInfo.InvariantCulture),
        };
        return root.ToJsonString(WriteOptions) + "\n";
    }

    /// <summary>The elements of an array property; empty when it is not an array.</summary>
    private static IEnumerable<JsonElement> Array(JsonElement root, string name)
    {
        return JsonFields.Get(root, name) is { ValueKind: JsonValueKind.Array } array ? [.. array.EnumerateArray()] : [];
    }
}

/// <summary>What <see cref="InstallContext.Parse"/> found.</summary>
/// <param name="Value">The context, or null.</param>
/// <param name="Reason">Why it cannot be used, for the exit-2 message; null when read.</param>
/// <param name="NewerSchemaVersion">The schema version when the file is newer than this program; otherwise null.</param>
public sealed record InstallContextReadResult(InstallContext? Value, string? Reason, int? NewerSchemaVersion)
{
    /// <summary>A file that was read.</summary>
    public static InstallContextReadResult Read(InstallContext value)
    {
        return new InstallContextReadResult(value, null, null);
    }

    /// <summary>A file that cannot be read, with the reason.</summary>
    public static InstallContextReadResult Invalid(string reason)
    {
        return new InstallContextReadResult(null, reason, null);
    }

    /// <summary>A file written for a newer schema.</summary>
    public static InstallContextReadResult Newer(int schemaVersion)
    {
        return new InstallContextReadResult(null, $"schemaVersion {schemaVersion}", schemaVersion);
    }
}

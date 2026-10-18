using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.App.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.SkillSources;

/// <summary>Why <see cref="SkillSourcesSettings.SetDir"/> refused a folder.</summary>
/// <param name="Changed">True when the folder was set.</param>
/// <param name="Problem">One sentence for the Human when it was refused; null when set.</param>
internal sealed record SetDirResult(bool Changed, string? Problem);

/// <summary>
/// This Huddle's own Skills settings in <c>{DataDir}/skill-sources.json</c> (§7.2): the folder override <c>dir</c> and the
/// <c>seen</c> keys. It is the one writer of the file, and each write keeps the other key as it is, so Sources › Folder and
/// <c>MarkSeenAsync</c> never overwrite each other. It also answers the configuration questions of §8.7.
/// </summary>
internal sealed class SkillSourcesSettings
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "skill-sources.json";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly TeamOptions options;
    private readonly ILogger<SkillSourcesSettings> logger;
    private readonly Lock gate = new();
    private string? fileDir;
    private IReadOnlyList<string>? seen;

    /// <summary>Reads <c>skill-sources.json</c> once; an unreadable file is treated as absent and logged.</summary>
    public SkillSourcesSettings(IOptions<TeamOptions> options, ILogger<SkillSourcesSettings> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options.Value;
        this.logger = logger;
        this.DataDir = Path.GetFullPath(this.options.DataDir);
        this.ComputerName = string.IsNullOrWhiteSpace(this.options.ComputerName) ? Environment.MachineName : this.options.ComputerName.Trim();
        this.Load();
    }

    /// <summary>Raised after the folder changes (not after a <c>seen</c> write).</summary>
    public event Action? Changed;

    /// <summary>Huddle's data folder, absolute.</summary>
    public string DataDir { get; }

    /// <summary>The name per-computer paths are matched against: <c>Team:ComputerName</c>, else the machine name.</summary>
    public string ComputerName { get; }

    /// <summary>The full path of <c>skill-sources.json</c>.</summary>
    public string FilePath => Path.Combine(this.DataDir, FileName);

    /// <summary>The Skill Sources folder: <c>skill-sources.json</c> <c>dir</c>, else <c>Team:SkillSourcesDir</c>, else
    /// <c>{DataDir}/SkillSources</c>; absolute.</summary>
    public string Dir
    {
        get
        {
            string? configured = this.fileDir ?? (string.IsNullOrWhiteSpace(this.options.SkillSourcesDir) ? null : this.options.SkillSourcesDir);
            return configured is null ? Path.Combine(this.DataDir, "SkillSources") : Path.GetFullPath(configured, this.DataDir);
        }
    }

    /// <summary>The folder set on the Sources tab, or null when none is (the configuration key or default applies).</summary>
    public string? FileDir => this.fileDir;

    /// <summary>The <c>Skills/</c> folder that holds the copies Teammates use.</summary>
    public string SkillsDir => Path.GetFullPath(this.options.Acp.SkillsDir, this.DataDir);

    /// <summary>The keys the Human has seen; null means "no baseline yet" (§6.5, the New overlay).</summary>
    public IReadOnlyList<string>? Seen => this.seen;

    /// <summary>The Huddle version: the app's informational version with any <c>+commit</c> suffix cut.</summary>
    public static string HuddleVersion
    {
        get
        {
            string? version = typeof(SkillSourcesSettings).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(SkillSourcesSettings).Assembly.GetName().Version?.ToString();
            version ??= "0.0.0";
            int plus = version.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? version : version[..plus];
        }
    }

    /// <summary>The <c>huddle-packs</c> program: <c>Team:HuddlePacksPath</c>, else <c>huddle-packs.exe</c> beside the app when it
    /// exists, else <c>huddle-packs.dll</c> there.</summary>
    public string HuddlePacksPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(this.options.HuddlePacksPath))
            {
                return Path.GetFullPath(this.options.HuddlePacksPath);
            }

            string exe = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "huddle-packs.exe" : "huddle-packs");
            return File.Exists(exe) ? exe : Path.Combine(AppContext.BaseDirectory, "huddle-packs.dll");
        }
    }

    /// <summary>Sets or clears the folder override. A folder that does not exist is refused; nothing moves.</summary>
    public SetDirResult SetDir(string? folder)
    {
        string? value = string.IsNullOrWhiteSpace(folder) ? null : Path.GetFullPath(folder.Trim(), this.DataDir);
        if (value is not null && !Directory.Exists(value))
        {
            return new SetDirResult(false, $"{value} does not exist. Create it first, or choose another folder.");
        }

        lock (this.gate)
        {
            if (string.Equals(value, this.fileDir, StringComparison.OrdinalIgnoreCase))
            {
                return new SetDirResult(false, null);
            }

            this.fileDir = value;
            this.Write();
        }

        this.Changed?.Invoke();
        return new SetDirResult(true, null);
    }

    /// <summary>Replaces the <c>seen</c> keys, keeping <c>dir</c> as it is.</summary>
    public void SetSeen(IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (this.gate)
        {
            this.seen = [.. keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            this.Write();
        }
    }

    /// <summary>Reads the file; anything unreadable leaves both keys absent.</summary>
    private void Load()
    {
        if (!File.Exists(this.FilePath))
        {
            return;
        }

        try
        {
            if (JsonNode.Parse(File.ReadAllText(this.FilePath)) is not JsonObject root)
            {
                this.logger.LogWarning("{File} is not a JSON object; it is treated as absent.", this.FilePath);
                return;
            }

            this.fileDir = root["dir"] is JsonValue dir && dir.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text) ? text : null;
            if (root["seen"] is JsonArray keys)
            {
                this.seen = [.. keys.OfType<JsonValue>().Select(static key => key.TryGetValue(out string? value) ? value : null).OfType<string>()];
            }
        }
        catch (JsonException exception)
        {
            this.logger.LogWarning(exception, "{File} is not valid JSON; it is treated as absent.", this.FilePath);
        }
        catch (IOException exception)
        {
            this.logger.LogWarning(exception, "{File} could not be read; it is treated as absent.", this.FilePath);
        }
    }

    /// <summary>Writes both keys through a temporary file and a move. Called under the gate.</summary>
    private void Write()
    {
        JsonObject root = new() { ["schemaVersion"] = 1 };
        if (this.fileDir is not null)
        {
            root["dir"] = this.fileDir;
        }

        if (this.seen is not null)
        {
            root["seen"] = new JsonArray([.. this.seen.Select(static key => (JsonNode?)JsonValue.Create(key))]);
        }

        Directory.CreateDirectory(this.DataDir);
        AtomicFile.WriteText(this.FilePath, root.ToJsonString(WriteOptions).ReplaceLineEndings("\n") + "\n");
    }
}

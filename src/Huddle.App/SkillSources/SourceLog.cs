using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Resolving;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agency.Huddle.App.SkillSources;

/// <summary>Appends lines to a Source's <c>log.jsonl</c>: one write per line with <see cref="FileShare.ReadWrite"/>, so two
/// Huddles interleave whole lines; never rewritten, never rotated.</summary>
internal sealed class SourceLog(SkillSourcesSettings settings, TimeProvider? timeProvider = null)
{
    /// <summary>The file's name in a Source's folder.</summary>
    public const string FileName = "log.jsonl";

    /// <summary>The longest line written, in bytes.</summary>
    public const int MaxLineBytes = 4096;

    private static readonly JsonSerializerOptions LineOptions = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();

    /// <summary>Appends one line to the Source's log. Writes nothing, and creates no folder, when the id is not a Source
    /// id or its folder does not exist.</summary>
    /// <returns>True when a line was written.</returns>
    public bool Append(string sourceId, LogLine line)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        ArgumentNullException.ThrowIfNull(line);
        if (!SourceId.IsValid(sourceId))
        {
            return false;
        }

        string folder = SourceId.FolderOf(settings.Dir, sourceId);
        if (!Directory.Exists(folder))
        {
            return false;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(this.Format(line) + "\n");
        lock (this.gate)
        {
            using FileStream stream = new(Path.Combine(folder, FileName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            stream.Write(bytes);
        }

        return true;
    }

    /// <summary>The line's JSON, keys in the order of §7.2, shortened below <see cref="MaxLineBytes"/> by cutting the detail.</summary>
    public string Format(LogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        string text = Build(line, this.clock.GetLocalNow(), settings.ComputerName);
        if (Encoding.UTF8.GetByteCount(text) <= MaxLineBytes || line.Detail is null)
        {
            return text;
        }

        return Build(line with { Detail = line.Detail[..Math.Min(line.Detail.Length, 200)] + "…", Data = null }, this.clock.GetLocalNow(), settings.ComputerName);
    }

    /// <summary>Builds the JSON object of one line.</summary>
    private static string Build(LogLine line, DateTimeOffset at, string computer)
    {
        JsonObject root = new()
        {
            ["at"] = at.ToString(InstallContext.TimeFormat, CultureInfo.InvariantCulture),
            ["computer"] = computer,
            ["huddle"] = SkillSourcesSettings.HuddleVersion,
            ["event"] = line.Event,
            ["subject"] = line.Subject,
        };
        if (line.Action is not null)
        {
            root["action"] = line.Action;
        }

        if (line.Detail is not null)
        {
            root["detail"] = line.Detail;
        }

        if (line.Code is not null)
        {
            root["code"] = line.Code;
        }

        if (line.Data is { Count: > 0 } data)
        {
            root["data"] = JsonSerializer.SerializeToNode(data, LineOptions);
        }

        return root.ToJsonString(LineOptions);
    }
}

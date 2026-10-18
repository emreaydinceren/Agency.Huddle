using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agency.Huddle.SkillPacks;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>One <c>log.jsonl</c> line as its writer names it; <see cref="SourceLog"/> fills <c>at</c>, <c>computer</c> and
/// <c>huddle</c> (§7.2, "sources/&lt;source&gt;/log.jsonl").</summary>
/// <param name="Event">One of the events of §7.2.</param>
/// <param name="Subject"><c>pack:&lt;id&gt;</c>, <c>catalog:&lt;id&gt;</c>, <c>skill:&lt;name&gt;</c> or <c>file:&lt;path&gt;</c>.</param>
/// <param name="Action">A finer label; only <c>moved</c> uses it.</param>
/// <param name="Detail">One sentence for people.</param>
/// <param name="Code">A check code, for <c>row-broken</c>.</param>
/// <param name="Data">Event-specific values.</param>
internal sealed record LogLine(string Event, string Subject, string? Action = null, string? Detail = null, string? Code = null, IReadOnlyDictionary<string, object?>? Data = null);

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

using Agency.Huddle.App.Library;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>Keeps the move's report in <c>{DataDir}/skill-sources-move.json</c> so the UI shows it once, even across a restart.</summary>
internal sealed class MoveReportStore(SkillSourcesSettings settings)
{
    /// <summary>The file's name in the data folder.</summary>
    public const string FileName = "skill-sources-move.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Lock gate = new();

    /// <summary>The file's full path.</summary>
    public string FilePath => Path.Combine(settings.DataDir, FileName);

    /// <summary>Saves a report, either waiting to be shown or already marked shown (a move that moved nothing of the Human's).</summary>
    /// <param name="report">The report to keep.</param>
    /// <param name="shown">True when there is nothing to announce, so <see cref="TakeUnshown"/> never returns it.</param>
    public void Save(MoveReport report, bool shown)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (this.gate)
        {
            Directory.CreateDirectory(settings.DataDir);
            AtomicFile.WriteText(this.FilePath, JsonSerializer.Serialize(new StoredReport(report, shown), Options).ReplaceLineEndings("\n") + "\n");
        }
    }

    /// <summary>The report when it has not been shown yet, marking it shown; null otherwise.</summary>
    public MoveReport? TakeUnshown()
    {
        lock (this.gate)
        {
            if (!File.Exists(this.FilePath))
            {
                return null;
            }

            StoredReport? stored;
            try
            {
                stored = JsonSerializer.Deserialize<StoredReport>(File.ReadAllText(this.FilePath), Options);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }

            if (stored is not { Shown: false, Report: { } report })
            {
                return null;
            }

            AtomicFile.WriteText(this.FilePath, JsonSerializer.Serialize(stored with { Shown = true }, Options).ReplaceLineEndings("\n") + "\n");
            return report;
        }
    }

    /// <summary>The file's shape.</summary>
    /// <param name="Report">The report.</param>
    /// <param name="Shown">Whether the snackbar has been shown.</param>
    private sealed record StoredReport([property: JsonPropertyName("report")] MoveReport? Report, [property: JsonPropertyName("shown")] bool Shown);
}

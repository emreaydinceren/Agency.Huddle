using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>Places one staged file; the seam a crash test replaces (§6.13, "Crash safety").</summary>
internal interface IMoveFiles
{
    /// <summary>Moves <paramref name="from"/> to <paramref name="to"/>, creating the target's folder.</summary>
    /// <exception cref="IOException">The target exists and <paramref name="overwrite"/> is false, or the move failed.</exception>
    void Place(string from, string to, bool overwrite);
}

/// <summary>The production placement: <see cref="File.Move(string, string, bool)"/> after creating the folder.</summary>
internal sealed class PhysicalMoveFiles : IMoveFiles
{
    /// <inheritdoc/>
    public void Place(string from, string to, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to) ?? to);
        File.Move(from, to, overwrite);
    }
}

/// <summary>One thing the move could not carry over.</summary>
/// <param name="Subject">The file or item.</param>
/// <param name="Message">One sentence for the Human.</param>
internal sealed record MoveProblem(string Subject, string Message);

/// <summary>What the move did, shown once as a snackbar and kept in <c>skill-sources-move.json</c>.</summary>
/// <param name="At">When it ran.</param>
/// <param name="Failed">True when it could not finish; it runs again at the next start.</param>
/// <param name="Catalogs">Catalog files written.</param>
/// <param name="CatalogSkills">Skills listed across them.</param>
/// <param name="OmittedDuplicates">Entries left out because another catalog kept the name.</param>
/// <param name="ShippedPackRows">Rows written for the two Packs Huddle used to ship.</param>
/// <param name="UserPacksMoved">The Human's Packs moved from <c>SkillPacks/</c>.</param>
/// <param name="SkillsPointedAt">Imported Skills copied to <c>fetched/</c> and pointed at.</param>
/// <param name="SkillsLeftInPlace">Imported Skills left only in <c>Skills/</c>.</param>
/// <param name="SkillsWithIgnoredTools">Imported Skills whose <c>tools:</c> line Huddle now ignores.</param>
/// <param name="Problems">What could not be moved.</param>
internal sealed record MoveReport(
    DateTimeOffset At,
    bool Failed,
    int Catalogs,
    int CatalogSkills,
    int OmittedDuplicates,
    int ShippedPackRows,
    int UserPacksMoved,
    int SkillsPointedAt,
    int SkillsLeftInPlace,
    IReadOnlyList<string> SkillsWithIgnoredTools,
    IReadOnlyList<MoveProblem> Problems)
{
    /// <summary>A report for a move that could not finish.</summary>
    public static MoveReport Failure(DateTimeOffset at, IReadOnlyList<MoveProblem> problems)
    {
        return new MoveReport(at, true, 0, 0, 0, 0, 0, 0, 0, [], problems);
    }

    /// <summary>The snackbar text for this report (§6.13, the report table).</summary>
    public string SnackbarText()
    {
        if (this.Failed)
        {
            return "Huddle could not move Packs and Skills to SKILLS.md. It will try again next start.";
        }

        if (this.Problems.Count > 0)
        {
            return $"Packs and Skills moved to SKILLS.md · {this.Problems.Count} could not be moved";
        }

        return this.SkillsWithIgnoredTools.Count > 0
            ? $"Packs and Skills moved to SKILLS.md · {this.SkillsWithIgnoredTools.Count} Skills no longer grant tools"
            : "Packs and Skills moved to SKILLS.md";
    }
}

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

    /// <summary>Saves a report as not yet shown.</summary>
    public void Save(MoveReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (this.gate)
        {
            Directory.CreateDirectory(settings.DataDir);
            AtomicFile.WriteText(this.FilePath, JsonSerializer.Serialize(new StoredReport(report, false), Options).ReplaceLineEndings("\n") + "\n");
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

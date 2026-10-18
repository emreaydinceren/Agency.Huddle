using System.Globalization;
using System.Text.Json;
using Agency.Huddle.App.Library;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.Skills;

/// <summary>
/// Where an imported Skill came from: the <c>Skills/&lt;name&gt;/.source.json</c> record written at import
/// and read back to show provenance and detect local edits (Spec §Provenance, SK-7).
/// </summary>
/// <param name="Repo">The repository URL the Skill was downloaded from.</param>
/// <param name="Path">The Skill's folder inside the repository.</param>
/// <param name="Commit">The commit the download was pinned to.</param>
/// <param name="Sha256">The hash of the downloaded folder.</param>
/// <param name="Publisher">Who publishes the Skill.</param>
/// <param name="License">The Skill's license identifier.</param>
/// <param name="ImportedAt">When the Skill was imported or copied. An old file is written as UTC ISO-8601; a new one
/// (with <paramref name="SchemaVersion"/>) as local time with its offset.</param>
/// <param name="SchemaVersion">1 for a copy written by <c>SkillCopier</c>; null for an old file (Skill Packs spec §7.2).</param>
/// <param name="Source">The Source id the copy came from, such as <c>github.com/anthropics/skills</c>; null in an old file.</param>
/// <param name="Origin">The pinned URL the copy came from; null in an old file.</param>
/// <param name="CopiedFrom">The pointer folder on the computer that made the copy. Informational only.</param>
public sealed record SkillProvenance(
    string Repo,
    string Path,
    string Commit,
    string Sha256,
    string Publisher,
    string License,
    DateTimeOffset ImportedAt,
    int? SchemaVersion = null,
    string? Source = null,
    string? Origin = null,
    string? CopiedFrom = null)
{
    /// <summary>The origin, or for an old file the one derived as <c>https://github.com/{repo}/tree/{commit}/{path}</c>, so an
    /// imported Skill compares equal to its catalog entry after the move.</summary>
    public string? EffectiveOrigin => this.Origin ?? InstalledProvenance.DeriveOrigin(this.Repo, this.Commit, this.Path);

    /// <summary>The Source id, or for an old file the one derived from <see cref="Repo"/>.</summary>
    public string? EffectiveSource => this.Source ?? InstalledProvenance.DeriveSource(this.Repo);

    /// <summary>The file name of the provenance record inside a Skill's folder.</summary>
    public const string FileName = ".source.json";

    private const string ImportedAtFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    private const string LocalTimeFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Reads the provenance record from a Skill's folder.
    /// </summary>
    /// <param name="skillFolder">The Skill's folder on disk.</param>
    /// <param name="source">The record when the file is present and valid; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a valid record was read; <see langword="false"/> when the file is missing, not JSON, or incomplete.</returns>
    public static bool TryRead(string skillFolder, out SkillProvenance? source)
    {
        source = null;
        string file = System.IO.Path.Combine(skillFolder, SkillProvenance.FileName);
        if (!File.Exists(file))
        {
            return false;
        }

        SkillProvenanceFile? wire;
        try
        {
            wire = JsonSerializer.Deserialize<SkillProvenanceFile>(File.ReadAllText(file), SkillProvenance.JsonOptions);
        }
        catch (JsonException)
        {
            // A malformed record is treated as absent; the Skill still loads without provenance.
            return false;
        }
        catch (IOException)
        {
            // The file vanished or is locked mid-read; treated as absent, the same as a missing file.
            return false;
        }

        if (wire is null
            || string.IsNullOrWhiteSpace(wire.Repo)
            || string.IsNullOrWhiteSpace(wire.Path)
            || string.IsNullOrWhiteSpace(wire.Commit)
            || string.IsNullOrWhiteSpace(wire.Sha256)
            || string.IsNullOrWhiteSpace(wire.Publisher)
            || string.IsNullOrWhiteSpace(wire.License)
            || !DateTimeOffset.TryParse(wire.ImportedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset importedAt))
        {
            return false;
        }

        source = new SkillProvenance(wire.Repo, wire.Path, wire.Commit, wire.Sha256, wire.Publisher, wire.License, importedAt, wire.SchemaVersion, wire.Source, wire.Origin, wire.CopiedFrom);
        return true;
    }

    /// <summary>
    /// Writes this record to the Skill's folder as indented camelCase JSON with LF line endings.
    /// The text goes to a temporary file that then replaces <c>.source.json</c>, so a crash never leaves a half-written record.
    /// </summary>
    /// <param name="skillFolder">The Skill's folder on disk.</param>
    public void Write(string skillFolder)
    {
        SkillProvenanceFile wire = new(
            this.Repo,
            this.Path,
            this.Commit,
            this.Sha256,
            this.Publisher,
            this.License,
            this.SchemaVersion is null
                ? this.ImportedAt.ToUniversalTime().ToString(SkillProvenance.ImportedAtFormat, CultureInfo.InvariantCulture)
                : this.ImportedAt.ToString(SkillProvenance.LocalTimeFormat, CultureInfo.InvariantCulture),
            this.SchemaVersion,
            this.Source,
            this.Origin,
            this.CopiedFrom);
        string json = JsonSerializer.Serialize(wire, SkillProvenance.JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

        string target = System.IO.Path.Combine(skillFolder, SkillProvenance.FileName);
        AtomicFile.WriteText(target, json);
    }

    /// <summary>The JSON shape of the record: every value as text, with <c>importedAt</c> as a UTC ISO-8601 string.</summary>
    /// <param name="Repo">The repository URL.</param>
    /// <param name="Path">The Skill's folder inside the repository.</param>
    /// <param name="Commit">The pinned commit.</param>
    /// <param name="Sha256">The folder hash.</param>
    /// <param name="Publisher">The publisher.</param>
    /// <param name="License">The license identifier.</param>
    /// <param name="ImportedAt">The import time as text.</param>
    /// <param name="SchemaVersion">1 for a new file; absent in an old one.</param>
    /// <param name="Source">The Source id.</param>
    /// <param name="Origin">The pinned URL.</param>
    /// <param name="CopiedFrom">The pointer folder.</param>
    private sealed record SkillProvenanceFile(
        string Repo,
        string Path,
        string Commit,
        string Sha256,
        string Publisher,
        string License,
        string ImportedAt,
        int? SchemaVersion = null,
        string? Source = null,
        string? Origin = null,
        string? CopiedFrom = null);
}

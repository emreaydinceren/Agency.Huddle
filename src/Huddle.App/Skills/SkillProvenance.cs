using System.Globalization;
using System.Text;
using System.Text.Json;

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
/// <param name="ImportedAt">When the Skill was imported; written as UTC ISO-8601.</param>
public sealed record SkillProvenance(
    string Repo,
    string Path,
    string Commit,
    string Sha256,
    string Publisher,
    string License,
    DateTimeOffset ImportedAt)
{
    /// <summary>The file name of the provenance record inside a Skill's folder.</summary>
    public const string FileName = ".source.json";

    private const string ImportedAtFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
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

        source = new SkillProvenance(wire.Repo, wire.Path, wire.Commit, wire.Sha256, wire.Publisher, wire.License, importedAt);
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
            this.ImportedAt.ToUniversalTime().ToString(SkillProvenance.ImportedAtFormat, CultureInfo.InvariantCulture));
        string json = JsonSerializer.Serialize(wire, SkillProvenance.JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

        string target = System.IO.Path.Combine(skillFolder, SkillProvenance.FileName);
        string temp = target + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, target, overwrite: true);
    }

    /// <summary>The JSON shape of the record: every value as text, with <c>importedAt</c> as a UTC ISO-8601 string.</summary>
    /// <param name="Repo">The repository URL.</param>
    /// <param name="Path">The Skill's folder inside the repository.</param>
    /// <param name="Commit">The pinned commit.</param>
    /// <param name="Sha256">The folder hash.</param>
    /// <param name="Publisher">The publisher.</param>
    /// <param name="License">The license identifier.</param>
    /// <param name="ImportedAt">The import time as text.</param>
    private sealed record SkillProvenanceFile(
        string Repo,
        string Path,
        string Commit,
        string Sha256,
        string Publisher,
        string License,
        string ImportedAt);
}

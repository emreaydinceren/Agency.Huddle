using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>The outcome of saving a user candidate.</summary>
/// <param name="Success">Whether the candidate file and manifest were written.</param>
/// <param name="Error">Why nothing was saved; <see langword="null"/> on success.</param>
internal sealed record WriteResult(bool Success, string? Error)
{
    /// <summary>The result of a save that wrote its files.</summary>
    public static WriteResult Saved { get; } = new(true, null);

    /// <summary>Creates the result of a refused or failed save.</summary>
    /// <param name="error">The reason shown to the user.</param>
    /// <returns>A failed result.</returns>
    public static WriteResult Refused(string error)
    {
        return new(false, error);
    }
}

/// <summary>
/// Saves a custom role as a user candidate file and adds it to the user's copy of the team type manifest (HR-11), so it
/// shows in the hiring pool next time. Only the user's <c>TeamTypes</c> folder is written; built-ins are never touched.
/// </summary>
/// <param name="options">Supplies <see cref="TeamOptions.DataDir"/>.</param>
/// <param name="types">The catalog that supplies the manifest to extend.</param>
internal sealed partial class UserCandidateWriter(IOptions<TeamOptions> options, TeamTypeCatalog types)
{
    private const string CandidatesFolder = "candidates";
    private const string ManifestFile = "team-type.json";

    private static readonly JsonSerializerOptions WriteOptions = new(TeamTypeJson.Options) { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly string userDir = Path.Combine(options.Value.DataDir, TeamPaths.TeamTypesDir);
    private readonly Lock saveGate = new();

    /// <summary>Writes the candidate file and appends the role to the user manifest of the type.</summary>
    /// <param name="typeId">The id of an existing team type.</param>
    /// <param name="roleId">The new role id: lower-case letters, digits and hyphens.</param>
    /// <param name="title">The role title.</param>
    /// <param name="consultWhen">When to consult the role; may be blank.</param>
    /// <param name="stance">The stance text, the body of the candidate file.</param>
    /// <returns>Success, or the reason nothing was written. The caller reloads the catalog.</returns>
    public WriteResult Save(string typeId, string roleId, string title, string consultWhen, string stance)
    {
        ArgumentNullException.ThrowIfNull(typeId);
        ArgumentNullException.ThrowIfNull(roleId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(consultWhen);
        ArgumentNullException.ThrowIfNull(stance);

        TeamTypeManifest? type = types.Find(typeId);
        if (type is null || !IdPattern().IsMatch(typeId))
        {
            return WriteResult.Refused("That team type does not exist.");
        }

        if (!IdPattern().IsMatch(roleId))
        {
            return WriteResult.Refused("The role id must be lower-case letters, digits and hyphens, and start with a letter or digit.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return WriteResult.Refused("The title is required.");
        }

        if (string.IsNullOrWhiteSpace(stance))
        {
            return WriteResult.Refused("The stance is required.");
        }

        lock (this.saveGate)
        {
            string typeDir = Path.GetFullPath(Path.Combine(this.userDir, typeId));
            string candidateDir = Path.Combine(typeDir, CandidatesFolder);
            string candidatePath = Path.GetFullPath(Path.Combine(candidateDir, roleId + ".md"));
            if (!candidatePath.StartsWith(candidateDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || type.Roles.Any(r => string.Equals(r.Id, roleId, StringComparison.Ordinal))
                || File.Exists(candidatePath))
            {
                return WriteResult.Refused("A role with that id already exists in this team type.");
            }

            RoleSpec role = new(roleId, CollapseLines(title), CandidatesFolder + "/" + roleId + ".md", Manager: false, Required: false, MaxPerTeam: 1);
            TeamTypeManifest extended = type with { Roles = [.. type.Roles, role] };
            if (TeamTypeValidator.Validate(extended, static _ => true).Count > 0)
            {
                return WriteResult.Refused("The role could not be saved.");
            }

            return Write(typeDir, candidateDir, candidatePath, ComposeCandidate(title, consultWhen, stance), extended);
        }
    }

    /// <summary>Collapses line breaks to single spaces and trims, for a one-line frontmatter value.</summary>
    /// <param name="value">The raw text.</param>
    /// <returns>The text on one line.</returns>
    private static string CollapseLines(string value)
    {
        return string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Quotes a frontmatter scalar in single quotes, doubling any single quote inside it.</summary>
    /// <param name="value">The one-line value.</param>
    /// <returns>The quoted scalar.</returns>
    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    /// <summary>Builds the candidate file text: frontmatter with title and consult_when, then the stance.</summary>
    /// <param name="title">The role title.</param>
    /// <param name="consultWhen">When to consult the role; omitted from the file when blank.</param>
    /// <param name="stance">The stance text.</param>
    /// <returns>The file text with <c>\n</c> line endings.</returns>
    private static string ComposeCandidate(string title, string consultWhen, string stance)
    {
        StringBuilder text = new();
        text.Append("---\n").Append("title: ").Append(Quote(CollapseLines(title))).Append('\n');
        string when = CollapseLines(consultWhen);
        if (when.Length > 0)
        {
            text.Append("consult_when: ").Append(Quote(when)).Append('\n');
        }

        text.Append("---\n").Append(stance.Trim().ReplaceLineEndings("\n")).Append('\n');
        return text.ToString();
    }

    /// <summary>Writes the candidate file, then the manifest through a temporary file, removing the candidate if the manifest fails.</summary>
    /// <param name="typeDir">The user folder of the type.</param>
    /// <param name="candidateDir">The candidates folder below it.</param>
    /// <param name="candidatePath">The candidate file to create.</param>
    /// <param name="candidateText">The candidate file text.</param>
    /// <param name="manifest">The manifest to write.</param>
    /// <returns>Success, or a refusal when the disk write failed.</returns>
    private static WriteResult Write(string typeDir, string candidateDir, string candidatePath, string candidateText, TeamTypeManifest manifest)
    {
        bool candidateCreated = false;
        string temp = Path.Combine(typeDir, ManifestFile + ".tmp");
        try
        {
            Directory.CreateDirectory(candidateDir);
            using (FileStream stream = new(candidatePath, FileMode.CreateNew, FileAccess.Write))
            {
                candidateCreated = true;
                stream.Write(Utf8NoBom.GetBytes(candidateText));
            }

            string manifestPath = Path.Combine(typeDir, ManifestFile);
            File.WriteAllText(temp, JsonSerializer.Serialize(manifest, WriteOptions).ReplaceLineEndings("\n") + "\n", Utf8NoBom);
            File.Move(temp, manifestPath, overwrite: true);
            return WriteResult.Saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (candidateCreated)
            {
                TryDelete(candidatePath);
            }

            TryDelete(temp);

            return WriteResult.Refused("The role could not be saved.");
        }
    }

    /// <summary>Deletes a file, ignoring a failure.</summary>
    /// <param name="path">The file to delete.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original failure is what the caller reports, and a leftover file only blocks that role id.
        }
    }

    /// <summary>Matches a valid type or role id.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}

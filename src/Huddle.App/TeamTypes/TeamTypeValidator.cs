using System.Text.RegularExpressions;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>Checks a parsed manifest against the Spec's rules and says why it is invalid (TT-4, TT-5).</summary>
internal static partial class TeamTypeValidator
{
    private const string ProcessesFolder = "processes/";

    /// <summary>Validates a manifest that has no hiring pool behind it, so a role that names a job is reported as <c>job-unknown</c>.</summary>
    /// <param name="manifest">The manifest to check.</param>
    /// <param name="fileExists">Tells whether a path relative to the type folder, such as <c>candidates/developer.md</c>, exists.</param>
    /// <returns>Every problem found; empty when the manifest is valid.</returns>
    public static IReadOnlyList<TeamTypeProblem> Validate(TeamTypeManifest manifest, Func<string, bool> fileExists)
    {
        return Validate(manifest, fileExists, static _ => false);
    }

    /// <summary>Validates a manifest whose roles may refer to hiring-pool jobs.</summary>
    /// <param name="manifest">The manifest to check.</param>
    /// <param name="fileExists">Tells whether a path relative to the type folder, such as <c>candidates/developer.md</c>, exists.</param>
    /// <param name="jobExists">Tells whether the hiring pool holds a job with the given id.</param>
    /// <returns>Every problem found; empty when the manifest is valid.</returns>
    public static IReadOnlyList<TeamTypeProblem> Validate(TeamTypeManifest manifest, Func<string, bool> fileExists, Func<string, bool> jobExists)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(jobExists);

        List<TeamTypeProblem> problems = [];
        HashSet<string> roleIds = new(manifest.Roles.Select(static r => r.Id), StringComparer.Ordinal);

        if (manifest.SchemaVersion != 1)
        {
            problems.Add(new("schema", $"Unsupported schemaVersion {manifest.SchemaVersion}; only 1 is understood."));
        }

        if (!IdPattern().IsMatch(manifest.Id))
        {
            problems.Add(new("id", $"The id '{manifest.Id}' must be lower-case letters, digits and hyphens, and start with a letter or digit."));
        }

        if (!VersionPattern().IsMatch(manifest.Version))
        {
            problems.Add(new("version", $"The version '{manifest.Version}' must be major.minor.patch."));
        }

        ValidateManagers(manifest, problems);
        ValidateRoles(manifest, roleIds, fileExists, jobExists, problems);
        ValidateCoverage(manifest, problems);
        ValidateProcesses(manifest, fileExists, problems);

        if (manifest.RetroRole is not null && !roleIds.Contains(manifest.RetroRole))
        {
            problems.Add(new("retro-role-unknown", $"The retroRole '{manifest.RetroRole}' is not a role of this type."));
        }

        return problems;
    }

    /// <summary>Matches a valid type id.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>Matches a major.minor.patch version.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    /// <summary>Requires exactly one manager role.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="problems">Receives the problems.</param>
    private static void ValidateManagers(TeamTypeManifest manifest, List<TeamTypeProblem> problems)
    {
        int managers = manifest.Roles.Count(static r => r.Manager);
        if (managers == 0)
        {
            problems.Add(new("no-manager", "The type must declare exactly one manager role; none does."));
        }
        else if (managers > 1)
        {
            string names = string.Join(", ", manifest.Roles.Where(static r => r.Manager).Select(static r => r.Id));
            problems.Add(new("many-managers", $"The type must declare exactly one manager role; found {managers} ({names})."));
        }
    }

    /// <summary>Checks each role for a unique id, a candidate file, a sane limit and known vendor references.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="roleIds">The role ids of the manifest.</param>
    /// <param name="fileExists">Tells whether a type-relative path exists.</param>
    /// <param name="jobExists">Tells whether the hiring pool holds a job with the given id.</param>
    /// <param name="problems">Receives the problems.</param>
    private static void ValidateRoles(TeamTypeManifest manifest, HashSet<string> roleIds, Func<string, bool> fileExists, Func<string, bool> jobExists, List<TeamTypeProblem> problems)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        HashSet<string> reported = new(StringComparer.Ordinal);
        foreach (RoleSpec role in manifest.Roles)
        {
            if (!seen.Add(role.Id) && reported.Add(role.Id))
            {
                problems.Add(new("duplicate-role", $"The role id '{role.Id}' is used more than once."));
            }

            if (role.Job is not null)
            {
                if (!jobExists(role.Job))
                {
                    problems.Add(new("job-unknown", $"The role '{role.Id}' names the hiring-pool job '{role.Job}', which the pool does not hold."));
                }
            }
            else if (!fileExists(role.Candidate))
            {
                problems.Add(new("candidate-missing", $"The role '{role.Id}' has no candidate file at '{role.Candidate}'."));
            }

            if (role.Required && role.MaxPerTeam < 0)
            {
                problems.Add(new("limit", $"The role '{role.Id}' is required but its maxPerTeam is {role.MaxPerTeam}."));
            }

            foreach (string other in role.Model.DifferentVendorFrom.Where(id => !roleIds.Contains(id)))
            {
                problems.Add(new("vendor-unknown-role", $"The role '{role.Id}' asks for a different vendor from '{other}', which is not a role of this type."));
            }
        }
    }

    /// <summary>Requires every coverage step to be covered by some role.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="problems">Receives the problems.</param>
    private static void ValidateCoverage(TeamTypeManifest manifest, List<TeamTypeProblem> problems)
    {
        HashSet<string> covered = new(manifest.Roles.SelectMany(static r => r.Covers), StringComparer.Ordinal);
        foreach (string step in manifest.Coverage.Where(step => !covered.Contains(step)))
        {
            problems.Add(new("coverage-uncovered", $"No role covers the coverage step '{step}'."));
        }
    }

    /// <summary>Requires exactly one default process (when any process exists) and every process file to exist.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="fileExists">Tells whether a type-relative path exists.</param>
    /// <param name="problems">Receives the problems.</param>
    private static void ValidateProcesses(TeamTypeManifest manifest, Func<string, bool> fileExists, List<TeamTypeProblem> problems)
    {
        if (manifest.Processes.Count == 0)
        {
            return;
        }

        int defaults = manifest.Processes.Count(static p => p.Default);
        if (defaults != 1)
        {
            problems.Add(new("process-default", $"Exactly one process must be the default; {defaults} are."));
        }

        foreach (ProcessRef process in manifest.Processes)
        {
            string path = process.File.StartsWith(ProcessesFolder, StringComparison.Ordinal) ? process.File : ProcessesFolder + process.File;
            if (!fileExists(path))
            {
                problems.Add(new("process-missing", $"The process '{process.Id}' has no file at '{path}'."));
            }
        }
    }
}

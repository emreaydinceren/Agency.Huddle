using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>A proposed Task status change, with everything a gate needs to judge it (PR-7).</summary>
/// <param name="Team">The Team the Task belongs to.</param>
/// <param name="TaskId">The Task being changed.</param>
/// <param name="FromStatus">The wire name of the current status.</param>
/// <param name="ToStatus">The wire name of the proposed status.</param>
/// <param name="IsBug">Whether the Task is a bug.</param>
/// <param name="ParentSpecApproved">Whether the parent spec Task is approved; <see langword="true"/> when the Task has no parent spec.</param>
/// <param name="Evidence">The Task's Evidence lines.</param>
/// <param name="AuthorRoleCovers">Each Evidence author's role coverage, comma-joined.</param>
/// <param name="Actor">Who makes the change; the Human bypasses nothing here, the override lives in the caller.</param>
internal sealed record GateContext(
    string Team,
    TaskId TaskId,
    string FromStatus,
    string ToStatus,
    bool IsBug,
    bool ParentSpecApproved,
    IReadOnlyList<EvidenceLine> Evidence,
    IReadOnlyDictionary<string, string> AuthorRoleCovers,
    string? Actor);

/// <summary>A gate's refusal of a change.</summary>
/// <param name="Gate">The gate that refused.</param>
/// <param name="Message">The text returned to the caller, naming the gate.</param>
internal sealed record GateRefusal(GateId Gate, string Message);

/// <summary>Decides whether the gates enabled in a Team's <c>Process.md</c> refuse a Task change or a merge (PR-7, Spec §Gates).</summary>
/// <param name="teams">Finds the Team.</param>
/// <param name="folders">Locates the Team's folder and its <c>_team.md</c>.</param>
/// <param name="types">Confirms the Team's type exists.</param>
internal sealed class ProcessGateService(ITeamCatalog teams, ITeamFolders folders, TeamTypeCatalog types)
{
    private const string VerifyCoverage = "verify";

    /// <summary>Checks a proposed status change against the Team's enabled Task gates.</summary>
    /// <param name="context">The proposed change.</param>
    /// <returns>The first refusal, or <see langword="null"/> when no enabled gate refuses.</returns>
    public GateRefusal? CheckTaskChange(GateContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<GateId> enabled = this.EnabledGates(context.Team);
        bool toDone = string.Equals(context.ToStatus, TaskState.Done.ToWire(), StringComparison.Ordinal);
        bool toInProgress = string.Equals(context.ToStatus, TaskState.InProgress.ToWire(), StringComparison.Ordinal);
        bool toReview = string.Equals(context.ToStatus, TaskState.Review.ToWire(), StringComparison.Ordinal);

        if (toDone && enabled.Contains(GateId.QaEvidenceBeforeDone) && !HasVerifierEvidence(context))
        {
            return new GateRefusal(GateId.QaEvidenceBeforeDone, "Gate qa-evidence-before-done: an Evidence line from a member who verifies (for example QA Engineer) is needed before this Task can be Done.");
        }

        if (toInProgress && enabled.Contains(GateId.SpecApprovedBeforeBuild) && !context.ParentSpecApproved)
        {
            return new GateRefusal(GateId.SpecApprovedBeforeBuild, "Gate spec-approved-before-build: the parent spec Task must be approved before this Task can move to In Progress.");
        }

        if (toReview && context.IsBug && enabled.Contains(GateId.FailingTestBeforeFix) && !HasFailureBeforePass(context.Evidence))
        {
            return new GateRefusal(GateId.FailingTestBeforeFix, "Gate failing-test-before-fix: an Evidence line showing a failing test before the fix is needed before this bug can move to Review.");
        }

        return null;
    }

    /// <summary>Checks a merge or push against the Team's <c>checks-before-merge</c> gate.</summary>
    /// <param name="team">The Team merging.</param>
    /// <param name="teamEvidence">The Evidence lines of the Team's Tasks.</param>
    /// <returns>The refusal, or <see langword="null"/> when the gate is off, has no check command, or is met.</returns>
    public GateRefusal? CheckMerge(string team, IReadOnlyList<EvidenceLine> teamEvidence)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(teamEvidence);

        ProcessDocument? document = this.Load(team);
        if (document is null || !document.Gates.Contains(GateId.ChecksBeforeMerge) || string.IsNullOrWhiteSpace(document.CheckCommand))
        {
            return null;
        }

        string command = document.CheckCommand.Trim();
        EvidenceLine? latest = teamEvidence
            .Where(line => line.ExitCode is not null && string.Equals(line.Command.Trim(), command, StringComparison.Ordinal))
            .OrderBy(static line => line.Time)
            .LastOrDefault();
        return latest?.ExitCode == 0
            ? null
            : new GateRefusal(GateId.ChecksBeforeMerge, $"Gate checks-before-merge: an Evidence line with exit code 0 for '{command}' is needed before merging or pushing.");
    }

    /// <summary>Lists the gates the Team's <c>Process.md</c> enables.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>The gates in file order; empty when the Team has no process file or no known team type.</returns>
    public IReadOnlyList<GateId> EnabledGates(string team)
    {
        ArgumentNullException.ThrowIfNull(team);
        return this.Load(team)?.Gates ?? [];
    }

    /// <summary>Looks up the coverage steps of a role in its team type.</summary>
    /// <param name="role">The role a member was hired into.</param>
    /// <returns>The role's coverage steps, comma-joined, the shape of the context's author coverage map; <see langword="null"/> when the type or role is unknown.</returns>
    public string? CoverageOf(RoleRef role)
    {
        ArgumentNullException.ThrowIfNull(role);
        RoleSpec? spec = types.Find(role.TypeId)?.Roles?.FirstOrDefault(r => string.Equals(r.Id, role.RoleId, StringComparison.Ordinal));
        return spec is null ? null : string.Join(',', spec.Covers);
    }

    private static bool HasVerifierEvidence(GateContext context) =>
        context.Evidence.Any(line =>
            line.Command.Length > 0
            && context.AuthorRoleCovers.TryGetValue(line.Author, out string? covers)
            && covers.Split(',', StringSplitOptions.TrimEntries).Contains(VerifyCoverage, StringComparer.OrdinalIgnoreCase));

    private static bool HasFailureBeforePass(IReadOnlyList<EvidenceLine> evidence)
    {
        List<EvidenceLine> runs = [.. evidence.Where(static line => line.ExitCode is not null).OrderBy(static line => line.Time)];
        EvidenceLine? failure = runs.FirstOrDefault(static line => line.ExitCode != 0);
        return failure is not null && runs.Any(line => line.ExitCode == 0 && line.Time > failure.Time);
    }

    private ProcessDocument? Load(string team)
    {
        if (teams.Find(team) is null)
        {
            return null;
        }

        TeamFile? file = folders.ReadTeamFile(team);
        if (file is null || types.Find(file.TeamType.Id) is null)
        {
            return null;
        }

        LibraryResult<LibraryPath> folder = folders.EnsureTeam(team);
        if (folder.Value is null)
        {
            return null;
        }

        string path = Path.Combine(folder.Value.FullPath, Path.GetFileName(file.Process));
        try
        {
            return File.Exists(path) && ProcessDocument.TryParse(File.ReadAllText(path), out ProcessDocument? document) ? document : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

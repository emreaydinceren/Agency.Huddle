using System.Collections.Concurrent;
using System.Globalization;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>Decides whether the gates enabled in a Team's <c>_team.md</c> refuse a Task change or a merge (PR-7, Spec §Gates).</summary>
/// <param name="teams">Finds the Team.</param>
/// <param name="folders">Locates the Team's folder and its <c>_team.md</c>.</param>
/// <param name="types">Confirms the Team's type exists.</param>
/// <param name="logger">Warns once per Team about gate ids it does not know; optional.</param>
/// <param name="renames">Resolves a Team's old name to today's; optional.</param>
internal sealed class ProcessGateService(ITeamCatalog teams, ITeamFolders folders, TeamTypeCatalog types, ILogger<ProcessGateService>? logger = null, ITeamRenames? renames = null)
{
    private const string VerifyCoverage = "verify";

    private readonly ConcurrentDictionary<string, bool> warned = new(StringComparer.Ordinal);

    /// <summary>The one-sentence risk shown when a gate is turned off.</summary>
    /// <param name="gate">The gate.</param>
    /// <returns>The risk sentence.</returns>
    public static string GateRisk(GateId gate) => gate switch
    {
        GateId.QaEvidenceBeforeDone => "A Task can then be marked Done with no verified Evidence.",
        GateId.ChecksBeforeMerge => "Code can then be merged or pushed without a passing check run on record.",
        GateId.SpecApprovedBeforeBuild => "Work can then start on a Task before its spec is approved.",
        GateId.FailingTestBeforeFix => "A bug can then move to Review without a test that failed first.",
        GateId.AcceptBeforeMerge => "Code can then be merged into main without the Human's Accept.",
        GateId.BlockersDoneBeforeStart => "A Task can then start, or reach Done, while a Task it is blocked by is still open.",
        GateId.HumanGate => "A Task can then reach Done without the Human's acceptance.",
        GateId.ProofIncomplete => "A Task can then reach Needs You with its Proof incomplete.",
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
        _ => throw new ArgumentOutOfRangeException(nameof(gate), gate, null),
#pragma warning restore HAA0601
    };

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

        if (context.OpenBlockers.Count > 0 && enabled.Contains(GateId.BlockersDoneBeforeStart) && IsBlockerGated(context.ToStatus))
        {
            return new GateRefusal(GateId.BlockersDoneBeforeStart, BlockerRefusal(context.OpenBlockers));
        }

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

        ProcessSettings? settings = this.Load(team);
        if (settings is null || !settings.Gates.Contains(GateId.ChecksBeforeMerge) || string.IsNullOrWhiteSpace(settings.CheckCommand))
        {
            return null;
        }

        string command = settings.CheckCommand.Trim();
        EvidenceLine? latest = teamEvidence
            .Where(line => line.ExitCode is not null && line.RunsCommand(command))
            .OrderBy(static line => line.Time)
            .LastOrDefault();
        return latest?.ExitCode == 0
            ? null
            : new GateRefusal(GateId.ChecksBeforeMerge, $"Gate checks-before-merge: an Evidence line with exit code 0 for '{command}' is needed before merging or pushing.");
    }

    /// <summary>Lists the gates the Team's <c>_team.md</c> enables.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>The gates in file order; empty when the Team is blank (a new Task whose Team is not chosen yet), has no <c>_team.md</c> or no known team type.</returns>
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

    /// <summary>Tells whether a role covers the <c>verify</c> step: the one test the request confirmers and the document history read reminder share.</summary>
    /// <param name="role">The role a member was hired into, or <see langword="null"/> for an Agent with none.</param>
    /// <returns><see langword="true"/> when the role's coverage lists verify; <see langword="false"/> for no role, an unknown role or any other coverage.</returns>
    public bool CoversVerify(RoleRef? role) =>
        role is not null
        && this.CoverageOf(role) is { } coverage
        && coverage.Split(',', StringSplitOptions.TrimEntries).Contains(VerifyCoverage, StringComparer.OrdinalIgnoreCase);

    /// <summary>Tells whether a role covers the <c>challenge</c> step, which is what reviewing a Task in Review takes (#403).</summary>
    /// <param name="role">The role a member was hired into, or <see langword="null"/> for an Agent with none.</param>
    /// <returns><see langword="true"/> when the role's coverage lists challenge.</returns>
    public bool CoversChallenge(RoleRef? role) =>
        role is not null
        && this.CoverageOf(role) is { } coverage
        && coverage.Split(',', StringSplitOptions.TrimEntries).Contains("challenge", StringComparer.OrdinalIgnoreCase);

    /// <summary>Finds the manager the Team's catalog entry names.</summary>
    /// <param name="team">The Team name; an old name is resolved to today's.</param>
    /// <returns>The manager's name, or <see langword="null"/> for a blank or unknown Team or one with no manager.</returns>
    internal string? ManagerOf(string team)
    {
        if (string.IsNullOrWhiteSpace(team))
        {
            return null;
        }

        return teams.Find(renames?.Resolve(team) ?? team)?.Manager;
    }

    /// <summary>Whether <c>blockers-done-before-start</c> covers a move into a status: In Progress, Review, Needs You and Done.</summary>
    /// <param name="toStatus">The wire name of the proposed status.</param>
    private static bool IsBlockerGated(string toStatus) =>
        string.Equals(toStatus, TaskState.InProgress.ToWire(), StringComparison.Ordinal)
        || string.Equals(toStatus, TaskState.Review.ToWire(), StringComparison.Ordinal)
        || string.Equals(toStatus, TaskState.NeedsYou.ToWire(), StringComparison.Ordinal)
        || string.Equals(toStatus, TaskState.Done.ToWire(), StringComparison.Ordinal);

    /// <summary>The refusal that names every open blocker with its status, in <c>blocked_by</c> order.</summary>
    /// <param name="open">The open blockers; never empty.</param>
    private static string BlockerRefusal(IReadOnlyList<OpenBlocker> open)
    {
        string list = string.Join(", ", open.Select(static blocker => string.Create(CultureInfo.InvariantCulture, $"{blocker.Id} ({blocker.Status.ToWire()})")));
        return $"Gate blockers-done-before-start: this Task is blocked by {list}. A blocked Task waits until every blocker is Done, Cancelled, Duplicate or Rejected, and for the manager's go-ahead.";
    }

    private static bool HasVerifierEvidence(GateContext context) =>
        context.Evidence.Any(line =>
            line.Command.Length > 0
            && context.AuthorRoleCovers.TryGetValue(line.Author, out string? covers)
            && covers.Split(',', StringSplitOptions.TrimEntries).Contains(VerifyCoverage, StringComparer.OrdinalIgnoreCase));

    /// <summary>Whether a run with a non-zero exit code is followed by a later run with exit code 0.</summary>
    /// <param name="evidence">The Task's Evidence lines.</param>
    /// <returns><see langword="true"/> when a failing run came before a passing one.</returns>
    internal static bool HasFailureBeforePass(IReadOnlyList<EvidenceLine> evidence)
    {
        List<EvidenceLine> runs = [.. evidence.Where(static line => line.ExitCode is not null).OrderBy(static line => line.Time)];
        EvidenceLine? failure = runs.FirstOrDefault(static line => line.ExitCode != 0);
        return failure is not null && runs.Any(line => line.ExitCode == 0 && line.Time > failure.Time);
    }

    /// <summary>The Team's parsed Process, for the Proof builder.</summary>
    /// <param name="team">The Team name.</param>
    /// <returns>The Process document, or <see langword="null"/> when the Team or its Process is unknown.</returns>
    internal ProcessDocument? ProcessOf(string team)
    {
        if (this.Load(team) is null)
        {
            return null;
        }

        string? text = TeamProcessFile.Read(folders, team);
        return text is not null && ProcessDocument.TryParse(text, out ProcessDocument? document) ? document : null;
    }

    private ProcessSettings? Load(string team)
    {
        // A blank Team (a new Task whose Team is not chosen yet) names no Team, so it has no gates; the rename lookup refuses a blank name.
        if (string.IsNullOrWhiteSpace(team))
        {
            return null;
        }

        // A label written before a rename still carries the old name; resolve it so the gates never fail open.
        team = renames?.Resolve(team) ?? team;
        if (teams.Find(team) is not { } summary || folders.ReadTeamFile(team) is not { } file)
        {
            return null;
        }

        // The Team type comes from the same _team.md the gates come from; the catalog summary is only the fallback for a file that names none.
        string? typeId = file.TeamType is { Id.Length: > 0 } written ? written.Id : summary.TeamType?.Id;
        if (typeId is null || types.Find(typeId) is null)
        {
            return null;
        }

        if (file.Process.UnknownGates.Count > 0 && this.warned.TryAdd(team, true))
        {
            logger?.LogWarning(
                "Team {Team} lists gates this version does not know and will not enforce: {UnknownGates}.",
                team,
                string.Join(", ", file.Process.UnknownGates));
        }

        return file.Process;
    }
}

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.App.Teams;

/// <summary>One hire: a resolved card for a role of a team type, to be written as a Teammate of a Team.</summary>
/// <param name="Team">The Team the Teammate joins.</param>
/// <param name="TypeId">The team type id.</param>
/// <param name="RoleId">The role id within the type.</param>
/// <param name="Card">The resolved card carrying the name, model pick, skills and packs.</param>
internal sealed record HireRequest(string Team, string TypeId, string RoleId, CardState Card);

/// <summary>The outcome of one hire.</summary>
/// <param name="Ok">Whether the Teammate file was written.</param>
/// <param name="Error">Why the hire was refused, or what failed after the Teammate was written; <see langword="null"/> when nothing went wrong.</param>
/// <param name="PersonaName">The new Teammate's name when the file was written; otherwise <see langword="null"/>.</param>
internal sealed record HireResult(bool Ok, string? Error, string? PersonaName);

/// <summary>Makes sure a Team's skills are present before its members start (HR-9).</summary>
internal interface ISkillInstaller
{
    /// <summary>Installs what is missing.</summary>
    /// <param name="skills">The skills the Team's cards carry.</param>
    /// <param name="ct">Cancels the installation.</param>
    /// <returns>The skills that could not be installed; empty when all are present.</returns>
    Task<IReadOnlyList<SkillRef>> EnsureInstalledAsync(IReadOnlyList<SkillRef> skills, CancellationToken ct);
}

/// <summary>Tells when a freshly hired Teammate's host is running.</summary>
internal interface IPersonaStartGate
{
    /// <summary>Waits until the Teammate has a running host or the timeout passes.</summary>
    /// <param name="name">The Teammate name.</param>
    /// <param name="timeout">The longest wait.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns><see langword="true"/> when the Teammate is running; <see langword="false"/> on timeout.</returns>
    Task<bool> WaitUntilRunningAsync(string name, TimeSpan timeout, CancellationToken ct);
}

/// <summary>The outcome of starting a Team.</summary>
/// <param name="Ok">Whether every member was hired and the manager is running.</param>
/// <param name="Error">Why the start stopped; <see langword="null"/> when it did not.</param>
/// <param name="Started">The Teammates hired before the start finished or stopped, in hire order.</param>
internal sealed record StartTeamResult(bool Ok, string? Error, IReadOnlyList<string> Started);

/// <summary>
/// Turns a resolved card into an ordinary Teammate file, a model row and an avatar (HR-5, HR-6,
/// Spec §Hiring steps 4-5). Refusals write nothing; a failure after the file is written is logged and
/// reported, and the Teammate stays. <see cref="StartTeamAsync"/> hires a whole Team, manager first.
/// </summary>
/// <param name="personas">Where the Teammate file is written.</param>
/// <param name="types">The team types, for the role and its candidate.</param>
/// <param name="packs">The skill packs, for their consult-when additions.</param>
/// <param name="avatars">Where the avatar is saved.</param>
/// <param name="limits">The shared hire-limit check.</param>
/// <param name="installer">Installs the Team's skills before any member is hired.</param>
/// <param name="startGate">Tells when the manager is running.</param>
/// <param name="logger">Receives post-write failures.</param>
/// <param name="processProvisioner">Gives a started Team the type's default process when it has none; <see langword="null"/> skips that step.</param>
internal sealed class HireService(
    PersonaStore personas,
    TeamTypeCatalog types,
    SkillPackStore packs,
    AvatarStore avatars,
    HireLimitGuard limits,
    ISkillInstaller installer,
    IPersonaStartGate startGate,
    ILogger<HireService> logger,
    ProcessProvisioner? processProvisioner = null)
{
    /// <summary>How long the manager gets to report running before the start stops.</summary>
    internal static readonly TimeSpan ManagerStartTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Serialises check-and-write so concurrent hires cannot both pass the same limit.</summary>
    private readonly Lock gate = new();

    /// <summary>
    /// Starts a Team: checks every card and required role, installs the skills, hires the manager and
    /// waits until it runs, then hires the rest in request order. Checks and installation write nothing;
    /// once hiring begins the first failure stops the start and reports who already started.
    /// </summary>
    /// <param name="team">The Team to start.</param>
    /// <param name="hires">One request per member, all for the same team type.</param>
    /// <param name="ct">Cancels the start; a cancelled start keeps the members already hired.</param>
    /// <returns>The outcome with the Teammates hired so far.</returns>
    public async Task<StartTeamResult> StartTeamAsync(string team, IReadOnlyList<HireRequest> hires, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentNullException.ThrowIfNull(hires);

        StartTeamResult? blocked = this.CheckStart(team, hires, out RoleSpec? managerRole);
        if (blocked is not null || managerRole is null)
        {
            return blocked ?? HireService.Stop("The team needs a manager.", []);
        }

        IReadOnlyList<SkillRef> skills = [.. hires.SelectMany(static h => h.Card.Skills).DistinctBy(static s => s.Name, StringComparer.Ordinal)];
        IReadOnlyList<SkillRef> missing = await installer.EnsureInstalledAsync(skills, ct).ConfigureAwait(false);
        if (missing.Count > 0)
        {
            string names = string.Join(", ", missing.Select(static s => s.Name));
            return HireService.Stop($"Skills not installed: {names}.", []);
        }

        _ = processProvisioner?.ProvisionDefault(team);
        HireRequest manager = hires.First(h => string.Equals(h.RoleId, managerRole.Id, StringComparison.Ordinal));
        List<string> started = [];
        string? failure = this.HireOrKeep(manager, started);
        if (failure is not null)
        {
            return HireService.Stop(failure, started);
        }

        bool running;
        try
        {
            running = await startGate.WaitUntilRunningAsync(manager.Card.Name, HireService.ManagerStartTimeout, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return HireService.Stop("The start was cancelled.", started);
        }

        if (!running)
        {
            return HireService.Stop($"{manager.Card.Name} did not start in time.", started);
        }

        foreach (HireRequest hire in hires.Where(h => !ReferenceEquals(h, manager)))
        {
            if (ct.IsCancellationRequested)
            {
                return HireService.Stop("The start was cancelled.", started);
            }

            failure = this.HireOrKeep(hire, started);
            if (failure is not null)
            {
                return HireService.Stop(failure, started);
            }
        }

        return new StartTeamResult(true, null, started);
    }

    /// <summary>Checks the start before anything is written.</summary>
    /// <returns>The blocking result, or <see langword="null"/> when the start may go ahead.</returns>
    private StartTeamResult? CheckStart(string team, IReadOnlyList<HireRequest> hires, out RoleSpec? managerRole)
    {
        managerRole = null;
        if (hires.Count == 0)
        {
            return HireService.Stop("There is nobody to start.", []);
        }

        string typeId = hires[0].TypeId;
        if (hires.Any(h => !string.Equals(h.TypeId, typeId, StringComparison.Ordinal) || !string.Equals(h.Team, team, StringComparison.Ordinal)))
        {
            return HireService.Stop("Every member must belong to the same team and team type.", []);
        }

        TeamTypeManifest? type = types.Find(typeId);
        if (type is null)
        {
            return HireService.Stop($"Unknown team type '{typeId}'.", []);
        }

        HireRequest? unready = hires.FirstOrDefault(static h => h.Card.Readiness != CardReadiness.Ready);
        if (unready is not null)
        {
            return HireService.Stop($"{unready.Card.Name} still needs setup.", []);
        }

        foreach (RoleSpec role in type.Roles.Where(static r => r.Required))
        {
            if (!hires.Any(h => string.Equals(h.RoleId, role.Id, StringComparison.Ordinal)))
            {
                return HireService.Stop($"The team needs a {role.Title}.", []);
            }
        }

        StartTeamResult? refused = this.CheckMembers(team, type, hires);
        if (refused is not null)
        {
            return refused;
        }

        managerRole = type.Roles.FirstOrDefault(r => r.Manager && hires.Any(h => string.Equals(h.RoleId, r.Id, StringComparison.Ordinal)));
        return null;
    }

    /// <summary>
    /// Checks names, role limits and hire limits for every member at once. A member already hired into
    /// the same Team and role (an earlier, interrupted start) counts as started, not as a collision.
    /// </summary>
    private StartTeamResult? CheckMembers(string team, TeamTypeManifest type, IReadOnlyList<HireRequest> hires)
    {
        lock (this.gate)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<HireRequest> pending = [];
            foreach (HireRequest hire in hires)
            {
                string name = hire.Card.Name;
                if (this.AlreadyHired(hire))
                {
                    _ = seen.Add(name);
                    continue;
                }

                if (!seen.Add(name) || this.NameInUse(name))
                {
                    return HireService.Stop($"The name '{name}' is already in use.", []);
                }

                pending.Add(hire);
            }

            foreach (IGrouping<string, HireRequest> group in pending.GroupBy(static h => h.RoleId, StringComparer.Ordinal))
            {
                RoleSpec? role = type.Roles.FirstOrDefault(r => string.Equals(r.Id, group.Key, StringComparison.Ordinal));
                if (role is null)
                {
                    return HireService.Stop($"Team type '{type.Id}' has no role '{group.Key}'.", []);
                }

                if (role.MaxPerTeam > 0 && (this.CountInRole(team, type.Id, role.Id) + group.Count()) > role.MaxPerTeam)
                {
                    return HireService.Stop($"Team '{team}' already has the most {role.Title} Teammates it allows ({role.MaxPerTeam}).", []);
                }
            }

            List<Candidate> candidates = [];
            foreach (HireRequest hire in pending)
            {
                CandidateTemplate? template = types.ReadCandidate(hire.TypeId, hire.RoleId);
                if (template is null)
                {
                    return HireService.Stop($"Team type '{hire.TypeId}' has no role '{hire.RoleId}'.", []);
                }

                candidates.Add(this.BuildCandidate(hire, template));
            }

            HireRefusal? refusal = candidates.Count == 0 ? null : limits.Check(candidates);
            return refusal is null ? null : HireService.Stop(HireService.Describe(refusal), []);
        }
    }

    /// <summary>Builds the Candidate the hire limits are checked for.</summary>
    private Candidate BuildCandidate(HireRequest request, CandidateTemplate template)
    {
        string name = request.Card.Name;
        return new Candidate(name, name.ToLowerInvariant(), template.Title, template.Stance, [request.Team], this.MergeConsultWhen(template, request.Card));
    }

    /// <summary>Whether a Teammate by this name or alias exists.</summary>
    private bool NameInUse(string name)
    {
        return personas.ResolveByNameOrAlias(name) is not null || personas.ResolveByNameOrAlias(name.ToLowerInvariant()) is not null;
    }

    /// <summary>Whether the request's Teammate already exists in the same Team and role of the same team type.</summary>
    private bool AlreadyHired(HireRequest request)
    {
        PersonaEntry? entry = personas.ResolveByNameOrAlias(request.Card.Name);
        return entry is not null
            && entry.Teams.Contains(request.Team, StringComparer.OrdinalIgnoreCase)
            && PersonaFrontmatter.TryReadIdentity(entry.Text, out PersonaIdentity? identity, out _)
            && identity.Role is { } hired
            && string.Equals(hired.TypeId, request.TypeId, StringComparison.Ordinal)
            && string.Equals(hired.RoleId, request.RoleId, StringComparison.Ordinal);
    }

    /// <summary>Keeps a member that is already hired, otherwise hires it; either way the name is recorded.</summary>
    /// <returns>The refusal text, or <see langword="null"/> when the member is in place.</returns>
    private string? HireOrKeep(HireRequest request, List<string> started)
    {
        if (this.AlreadyHired(request))
        {
            started.Add(request.Card.Name);
            return null;
        }

        return this.HireOne(request, started);
    }

    /// <summary>Hires one member and records the name.</summary>
    /// <returns>The refusal text, or <see langword="null"/> when the Teammate was written.</returns>
    private string? HireOne(HireRequest request, List<string> started)
    {
        HireResult result = this.Hire(request);
        if (result.PersonaName is not null)
        {
            started.Add(result.PersonaName);
        }

        return result.Ok ? null : result.Error ?? string.Empty;
    }

    /// <summary>Builds the result of a stopped start.</summary>
    private static StartTeamResult Stop(string error, IReadOnlyList<string> started)
    {
        return new StartTeamResult(false, error, started);
    }

    /// <summary>Hires one Teammate.</summary>
    /// <param name="request">What to hire.</param>
    /// <returns>The outcome; a refusal writes nothing.</returns>
    public HireResult Hire(HireRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (this.gate)
        {
            return this.HireLocked(request);
        }
    }

    /// <summary>Checks and writes one hire; the caller holds <see cref="gate"/> so no other hire interleaves.</summary>
    private HireResult HireLocked(HireRequest request)
    {
        TeamTypeManifest? type = types.Find(request.TypeId);
        if (type is null)
        {
            return HireService.Refuse($"Unknown team type '{request.TypeId}'.");
        }

        RoleSpec? role = type.Roles.FirstOrDefault(r => string.Equals(r.Id, request.RoleId, StringComparison.Ordinal));
        CandidateTemplate? template = role is null ? null : types.ReadCandidate(request.TypeId, request.RoleId);
        if (role is null || template is null)
        {
            return HireService.Refuse($"Team type '{request.TypeId}' has no role '{request.RoleId}'.");
        }

        string name = request.Card.Name;
        string alias = name.ToLowerInvariant();
        if (personas.ResolveByNameOrAlias(name) is not null || personas.ResolveByNameOrAlias(alias) is not null)
        {
            return HireService.Refuse($"The name '{name}' is already in use.");
        }

        if (role.MaxPerTeam > 0 && this.CountInRole(request.Team, request.TypeId, request.RoleId) >= role.MaxPerTeam)
        {
            return HireService.Refuse($"Team '{request.Team}' already has the most {role.Title} Teammates it allows ({role.MaxPerTeam}).");
        }

        string? consultWhen = this.MergeConsultWhen(template, request.Card);
        Candidate candidate = new(name, alias, template.Title, template.Stance, [request.Team], consultWhen);
        HireRefusal? refusal = limits.Check([candidate]);
        if (refusal is not null)
        {
            return HireService.Refuse(HireService.Describe(refusal));
        }

        PersonaIdentity identity = new(
            name,
            template.Title,
            alias,
            [request.Team],
            request.Card.Pick?.AdapterId ?? template.Adapter,
            [.. request.Card.Skills.Select(static s => s.Name)],
            Role: new RoleRef(request.TypeId, request.RoleId, type.Version),
            Packs: request.Card.Packs,
            ConsultWhen: consultWhen);

        try
        {
            _ = personas.Add(identity, template.Stance, request.Card.Pick?.Model);
        }
        catch (ChatException ex)
        {
            return HireService.Refuse(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Teammate {PersonaName} could not be written.", name);
            return HireService.Refuse($"{name} could not be hired: the Teammate file could not be written.");
        }

        return this.SaveAvatar(name);
    }

    /// <summary>Saves the new Teammate's default avatar; a failure is logged and reported while the Teammate stays.</summary>
    private HireResult SaveAvatar(string name)
    {
        try
        {
            avatars.Save(name, Avatar.None);
            return new HireResult(true, null, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Teammate {PersonaName} was hired but its avatar could not be saved.", name);
            return new HireResult(true, $"{name} was hired, but the avatar could not be saved.", name);
        }
    }

    /// <summary>Counts the Team's Teammates hired into a role.</summary>
    private int CountInRole(string team, string typeId, string roleId)
    {
        int count = 0;
        foreach (PersonaEntry entry in personas.Entries)
        {
            if (entry.Teams.Contains(team, StringComparer.OrdinalIgnoreCase)
                && PersonaFrontmatter.TryReadIdentity(entry.Text, out PersonaIdentity? identity, out _)
                && identity.Role is { } hired
                && string.Equals(hired.TypeId, typeId, StringComparison.Ordinal)
                && string.Equals(hired.RoleId, roleId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Joins the candidate's consult-when with each pack's addition by <c>"; "</c> (MS-4).</summary>
    private string? MergeConsultWhen(CandidateTemplate template, CardState card)
    {
        List<string> parts = [];
        string? own = template.JobDescriptionFields
            .FirstOrDefault(static f => string.Equals(f.Key, "consult_when", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(own))
        {
            parts.Add(own.Trim());
        }

        foreach (VersionedRef reference in card.Packs)
        {
            string? adds = packs.Get(reference.Id)?.ConsultWhenAdds;
            if (!string.IsNullOrWhiteSpace(adds))
            {
                parts.Add(adds.Trim());
            }
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Words a hire-limit refusal for the user.</summary>
    private static string Describe(HireRefusal refusal)
    {
        return refusal.Kind == HireLimitKind.PerTeam
            ? $"Team '{refusal.Team}' is full: {refusal.Used} of {refusal.Limit} Teammates."
            : $"{refusal.Used} of {refusal.Limit} agent processes are running, so no more Teammates can be hired.";
    }

    /// <summary>Builds the result of a refused hire.</summary>
    private static HireResult Refuse(string error)
    {
        return new HireResult(false, error, null);
    }
}

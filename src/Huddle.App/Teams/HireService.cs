using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.SkillSources;
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

/// <summary>One Teammate Build a team has drafted: written as is, not resolved from a team type's candidate file.</summary>
/// <param name="Name">The Teammate's name.</param>
/// <param name="Title">The job title.</param>
/// <param name="Text">The persona text, which becomes the Teammate's system prompt.</param>
/// <param name="Role">The role of the team type this Teammate fills, or <see langword="null"/> for a Teammate outside the type's roles.</param>
/// <param name="Avatar">The avatar to save (its colour and label); an <paramref name="Image"/> is saved with it.</param>
/// <param name="Image">The bytes of the avatar image, or <see langword="null"/>.</param>
/// <param name="ImageExtension">The extension, with its dot, of <paramref name="Image"/>.</param>
/// <param name="Adapter">The adapter the Teammate runs on, or <see langword="null"/> for the default.</param>
/// <param name="Model">The model, or <see langword="null"/> for the adapter's default.</param>
/// <param name="Effort">The effort, or <see langword="null"/> for the default.</param>
/// <param name="Skills">The names of the Skills the Teammate carries; they must be in <c>Skills/</c> (see <see cref="HireService.EnsureSkillsAsync"/>).</param>
/// <param name="Packs">The skill packs the Teammate was drawn from.</param>
internal sealed record DraftHire(
    string Name,
    string Title,
    string Text,
    RoleRef? Role,
    Avatar Avatar,
    byte[]? Image,
    string? ImageExtension,
    string? Adapter = null,
    string? Model = null,
    string? Effort = null,
    IReadOnlyList<string>? Skills = null,
    IReadOnlyList<VersionedRef>? Packs = null)
{
    /// <summary>The names of the Skills the Teammate carries; empty when omitted.</summary>
    public IReadOnlyList<string> Skills { get; init; } = Skills ?? [];

    /// <summary>The skill packs the Teammate was drawn from; empty when omitted.</summary>
    public IReadOnlyList<VersionedRef> Packs { get; init; } = Packs ?? [];
}

/// <summary>The outcome of hiring a batch of drafts.</summary>
/// <param name="Error">Why the batch stopped, or <see langword="null"/> when every draft was hired.</param>
/// <param name="Hired">The names written before the batch stopped, in request order.</param>
internal sealed record DraftHireResult(string? Error, IReadOnlyList<string> Hired);

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
/// <param name="skills">The registry, for each Pack's consult-when addition.</param>
/// <param name="avatars">Where the avatar is saved.</param>
/// <param name="limits">The shared hire-limit check.</param>
/// <param name="copier">Copies the Team's Skills into <c>Skills/</c> before any member is hired.</param>
/// <param name="startGate">Tells when the manager is running.</param>
/// <param name="logger">Receives post-write failures.</param>
/// <param name="processProvisioner">Gives a started Team the type's default process when it has none; <see langword="null"/> skips that step.</param>
/// <param name="onboarding">Seeds the running manager's first Turn with the process's onboarding steps, once per Team; <see langword="null"/> skips that step.</param>
internal sealed class HireService(
    PersonaStore personas,
    TeamTypeCatalog types,
    ISkillAvailability skills,
    AvatarStore avatars,
    HireLimitGuard limits,
    ISkillCopier copier,
    IPersonaStartGate startGate,
    ILogger<HireService> logger,
    ProcessProvisioner? processProvisioner = null,
    TeamOnboarding? onboarding = null)
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

        // CardResolver normally stops a card that is not ready earlier; this covers a clone removed in the meantime.
        IReadOnlyList<SkillRef> needed = [.. hires.SelectMany(static h => h.Card.Skills)];
        IReadOnlyList<SkillSetupItem> notReady = await this.EnsureSkillsAsync(needed, ct).ConfigureAwait(false);
        if (notReady.Count > 0)
        {
            string names = string.Join(", ", notReady.Select(static item => item.Name));
            return HireService.Stop($"Skills not ready: {names}. {SkillSetupText.Reason(notReady[0])}.", []);
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

        if (onboarding is not null)
        {
            _ = await onboarding.PostIfDueAsync(team, manager.Card.Name, ct).ConfigureAwait(false);
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

    /// <summary>Makes sure every named Skill is in <c>Skills/</c>, copying the copyable ones (Skill Packs spec §6.10.3). It
    /// downloads nothing and never replaces a folder that is there. A <see cref="SkillRef"/>'s source and path are ignored.</summary>
    /// <param name="skills">The Skills the cards or drafts carry.</param>
    /// <param name="ct">Cancels the copy.</param>
    /// <returns>One item per Skill that is not ready; empty when every one is in place.</returns>
    public Task<IReadOnlyList<SkillSetupItem>> EnsureSkillsAsync(IReadOnlyList<SkillRef> skills, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(skills);
        return copier.EnsureAvailableAsync([.. skills.Select(static skill => skill.Name).Distinct(StringComparer.Ordinal)], ct);
    }

    /// <summary>Checks that a batch of drafts can be hired together, writing nothing: no repeated name, no name in use, and the hire limits hold for the whole batch.</summary>
    /// <param name="team">The Team they would join.</param>
    /// <param name="drafts">The drafts.</param>
    /// <returns>The refusal, or <see langword="null"/> when the batch fits.</returns>
    public string? CheckDrafts(string team, IReadOnlyList<DraftHire> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        lock (this.gate)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (DraftHire draft in drafts)
            {
                if (!seen.Add(draft.Name) || personas.ResolveByNameOrAlias(draft.Name) is not null || personas.ResolveByNameOrAlias(draft.Name.ToLowerInvariant()) is not null)
                {
                    return $"The name '{draft.Name}' is already in use.";
                }
            }

            HireRefusal? refusal = limits.Check([.. drafts.Select(draft => HireService.AsCandidate(team, draft))]);
            return refusal is null ? null : HireService.Describe(refusal);
        }
    }

    /// <summary>Writes the drafts as Teammates of <paramref name="team"/>, in order; the first failure stops the batch and the ones already written stay.</summary>
    /// <param name="team">The Team they join.</param>
    /// <param name="drafts">The drafts; call <see cref="CheckDrafts"/> first.</param>
    /// <returns>The outcome with the names written.</returns>
    public DraftHireResult HireDrafts(string team, IReadOnlyList<DraftHire> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        List<string> hired = [];
        lock (this.gate)
        {
            foreach (DraftHire draft in drafts)
            {
                string alias = draft.Name.ToLowerInvariant();
                PersonaIdentity identity = new(draft.Name, draft.Title, alias, [team], draft.Adapter, draft.Skills, Role: draft.Role, Packs: draft.Packs);
                try
                {
                    _ = personas.Add(identity, draft.Text, draft.Model, draft.Effort);
                }
                catch (ChatException ex)
                {
                    return new DraftHireResult(ex.Message, hired);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Teammate {PersonaName} could not be written.", draft.Name);
                    return new DraftHireResult($"{draft.Name} could not be hired: the Teammate file could not be written.", hired);
                }

                hired.Add(draft.Name);
                this.SaveDraftAvatar(draft);
            }
        }

        return new DraftHireResult(null, hired);
    }

    /// <summary>Saves a drafted Teammate's avatar and image; a failure is logged and the Teammate stays.</summary>
    private void SaveDraftAvatar(DraftHire draft)
    {
        try
        {
            Avatar toSave = draft.Image is not null && draft.ImageExtension is not null
                ? draft.Avatar with { Image = avatars.WriteImage(draft.Image, draft.ImageExtension) }
                : draft.Avatar;
            avatars.Save(draft.Name, toSave);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ChatException)
        {
            logger.LogWarning(ex, "Teammate {PersonaName} was hired but its avatar could not be saved.", draft.Name);
        }
    }

    private static Candidate AsCandidate(string team, DraftHire draft) =>
        new(draft.Name, draft.Name.ToLowerInvariant(), draft.Title, draft.Text, [team], null);

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
            string? adds = skills.FindPack(reference.Id)?.ConsultWhenAdds;
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

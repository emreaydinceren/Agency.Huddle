using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Turns a resolved card into an ordinary Agent file, a model row and an avatar (HR-5, HR-6,
/// Spec §Hiring steps 4-5). Refusals write nothing; a failure after the file is written is logged and
/// reported, and the Agent stays.
/// </summary>
/// <param name="personas">Where the Agent file is written.</param>
/// <param name="types">The team types, for the role and its candidate.</param>
/// <param name="skills">The registry, for each Pack's consult-when addition.</param>
/// <param name="avatars">Where the avatar is saved.</param>
/// <param name="limits">The shared hire-limit check.</param>
/// <param name="copier">Copies the Team's Skills into <c>Skills/</c> before any member is hired.</param>
/// <param name="startGate">Tells when the manager is running, for <see cref="OnboardManagerAsync"/>.</param>
/// <param name="logger">Receives post-write failures.</param>
/// <param name="processProvisioner">Gives a new Team the chosen or default process; <see langword="null"/> skips that step.</param>
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
    /// <summary>How long the manager gets to report running before its onboarding is given up.</summary>
    internal static readonly TimeSpan ManagerStartTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Serialises check-and-write so concurrent hires cannot both pass the same limit.</summary>
    private readonly Lock gate = new();

    /// <summary>Gives a Team just created by Build a team a process: the one chosen, or the type's default when none was.</summary>
    /// <param name="team">The Team.</param>
    /// <param name="processId">The chosen process, or <see langword="null"/> for the default.</param>
    /// <returns>How the call ended; <see cref="ProvisionResult.UnknownProcess"/> when the type offers no process, as a Blank Team's does not.</returns>
    public ProvisionResult ProvisionProcess(string team, string? processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        if (processProvisioner is null)
        {
            return ProvisionResult.UnknownProcess;
        }

        return processId is null ? processProvisioner.ProvisionDefault(team) : processProvisioner.Provision(team, processId);
    }

    /// <summary>
    /// Posts a new Team's onboarding steps to its manager once the manager runs. Build a team does not wait for this, so it
    /// never throws: a manager that does not start in time, as with Adapters off, leaves the Team not onboarded and logged.
    /// </summary>
    /// <param name="team">The Team.</param>
    /// <param name="manager">The manager's Name.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns><see langword="true"/> when the steps were posted.</returns>
    public async Task<bool> OnboardManagerAsync(string team, string manager, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        ArgumentException.ThrowIfNullOrWhiteSpace(manager);

        if (onboarding is null)
        {
            return false;
        }

        try
        {
            if (!await startGate.WaitUntilRunningAsync(manager, HireService.ManagerStartTimeout, ct).ConfigureAwait(false))
            {
                logger.LogWarning("{PersonaName} did not start in time, so Team {Team} was not onboarded.", manager, team);
                return false;
            }

            return await onboarding.PostIfDueAsync(team, manager, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Nothing awaits this call: the Team is already hired, and a failure here must only cost its onboarding Turn.
            logger.LogWarning(ex, "Team {Team} could not be onboarded.", team);
            return false;
        }
    }

    /// <summary>Hires one Agent.</summary>
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
                // PersonaStore refuses blank text at the write, which for a new Team comes after the Team exists.
                if (string.IsNullOrWhiteSpace(draft.Text))
                {
                    return $"{draft.Name} has no instructions. Edit the draft and write what it should do.";
                }

                // PersonaStore refuses the Human's Name at the write, which for a new Team also comes after the Team exists.
                if (personas.IsHuman(draft.Name))
                {
                    return $"'{draft.Name}' is your own name. Choose another for this Agent.";
                }

                if (!seen.Add(draft.Name) || personas.ResolveByNameOrAlias(draft.Name) is not null || personas.ResolveByNameOrAlias(draft.Name.ToLowerInvariant()) is not null)
                {
                    return $"The name '{draft.Name}' is already in use.";
                }
            }

            HireRefusal? refusal = limits.Check([.. drafts.Select(draft => HireService.AsCandidate(team, draft))]);
            return refusal is null ? null : HireService.Describe(refusal);
        }
    }

    /// <summary>Writes the drafts as Agents of <paramref name="team"/>, in order; the first failure stops the batch and the ones already written stay.</summary>
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
                    logger.LogWarning(ex, "Agent {PersonaName} could not be written.", draft.Name);
                    return new DraftHireResult($"{draft.Name} could not be hired: the Agent file could not be written.", hired);
                }

                hired.Add(draft.Name);
                this.SaveDraftAvatar(draft);
            }
        }

        return new DraftHireResult(null, hired);
    }

    /// <summary>Saves a drafted Agent's avatar and image; a failure is logged and the Agent stays.</summary>
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
            logger.LogWarning(ex, "Agent {PersonaName} was hired but its avatar could not be saved.", draft.Name);
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
            return HireService.Refuse($"Team '{request.Team}' already has the most {role.Title} Agents it allows ({role.MaxPerTeam}).");
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
            _ = personas.Add(identity, template.Stance, request.Card.Pick?.Model, request.Effort, request.WorkMode);
        }
        catch (ChatException ex)
        {
            return HireService.Refuse(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Agent {PersonaName} could not be written.", name);
            return HireService.Refuse($"{name} could not be hired: the Agent file could not be written.");
        }

        return this.SaveAvatar(name);
    }

    /// <summary>Saves the new Agent's default avatar; a failure is logged and reported while the Agent stays.</summary>
    private HireResult SaveAvatar(string name)
    {
        try
        {
            avatars.Save(name, Avatar.None);
            return new HireResult(true, null, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Agent {PersonaName} was hired but its avatar could not be saved.", name);
            return new HireResult(true, $"{name} was hired, but the avatar could not be saved.", name);
        }
    }

    /// <summary>Counts the Team's Agents hired into a role.</summary>
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
            ? $"Team '{refusal.Team}' is full: {refusal.Used} of {refusal.Limit} Agents."
            : $"{refusal.Used} of {refusal.Limit} agent processes are running, so no more Agents can be hired.";
    }

    /// <summary>Builds the result of a refused hire.</summary>
    private static HireResult Refuse(string error)
    {
        return new HireResult(false, error, null);
    }
}

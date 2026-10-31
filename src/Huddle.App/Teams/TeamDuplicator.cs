using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Copies a Team's roster to a new Team with new names, a new workspace and fresh memory (HR-12). Only the
/// team type and the roles are carried over: no Library file, memory or Task content is copied, and
/// <c>owns</c> starts empty.
/// </summary>
internal sealed class TeamDuplicator(
    ITeamCatalog teams,
    TeamTypeCatalog types,
    ITeamFolders folders,
    HireService hire,
    PersonaStore personas,
    ISkillAvailability skills,
    IHireContext context,
    AvatarStore avatars)
{
    /// <summary>The refusal for a target name that is taken.</summary>
    internal const string ExistsMessage = "A Team with that name exists.";

    /// <summary>The refusal for a target name a Team cannot have.</summary>
    internal const string NameNotAllowedMessage = "That Team name is not allowed.";

    /// <summary>The refusal for a source Team that is not in the catalog.</summary>
    internal const string UnknownTeamMessage = "Unknown Team.";

    /// <summary>The refusal for a source Team that has no <c>_team.md</c>.</summary>
    internal const string NoTeamFileMessage = "That Team has no team file.";

    /// <summary>The refusal for a missing workspace when the team type requires one.</summary>
    internal const string WorkspaceRequiredMessage = "Choose a workspace folder.";

    /// <summary>The refusal for a workspace folder that does not exist.</summary>
    internal const string WorkspaceMissingMessage = "That folder does not exist.";

    /// <summary>The reason a member without a role is left out.</summary>
    internal const string NoRoleReason = "It has no role.";

    /// <summary>The reason a member whose role left the team type is left out.</summary>
    internal const string RoleGoneReason = "Its role no longer exists.";

    /// <summary>Creates <paramref name="newTeam"/> with the same team type and a fresh hire for each role of <paramref name="sourceTeam"/>.</summary>
    /// <param name="sourceTeam">The Team to copy.</param>
    /// <param name="newTeam">The new Team's name.</param>
    /// <param name="newWorkspace">The new Team's workspace folder, or <see langword="null"/> or blank for none.</param>
    /// <returns>The new Agents and the source members that were skipped, or the refusal.</returns>
    public DuplicateResult Duplicate(string sourceTeam, string newTeam, string? newWorkspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTeam);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTeam);

        TeamSummary? source = teams.Find(sourceTeam);
        if (source is null)
        {
            return TeamDuplicator.Refuse(UnknownTeamMessage);
        }

        TeamFile? sourceFile = folders.ReadTeamFile(source.Name);
        if (sourceFile is null)
        {
            return TeamDuplicator.Refuse(NoTeamFileMessage);
        }

        string target = newTeam.Trim();
        string? workspace = string.IsNullOrWhiteSpace(newWorkspace) ? null : newWorkspace.Trim();
        string? workspaceProblem = TeamDuplicator.WorkspaceProblem(types.Find(sourceFile.TeamType.Id)?.Workspace, workspace);
        if (workspaceProblem is not null)
        {
            return TeamDuplicator.Refuse(workspaceProblem);
        }

        TeamFile file = new(sourceFile.TeamType, null, workspace, null, TeamPaths.ProcessFileName, string.Empty)
        {
            Avatar = this.CopyAvatar(sourceFile.Avatar),
        };
        TeamCreateResult created = folders.CreateTeam(target, file);
        if (created.Outcome != TeamCreateOutcome.Created)
        {
            return TeamDuplicator.Refuse(created.Outcome == TeamCreateOutcome.AlreadyExists ? ExistsMessage : NameNotAllowedMessage);
        }

        List<string> hired = [];
        List<SkippedMember> skipped = [];
        foreach (SourceMember member in this.ReadMembers(source.Members, skipped).OrderBy(static m => m.Spec.Manager ? 0 : 1))
        {
            this.CopyOne(member, target, hired, skipped);
        }

        return new DuplicateResult(true, null, hired, skipped);
    }

    /// <summary>
    /// Gives the new Team its own copy of the source's avatar. An image is read and written under a new file name, never shared:
    /// replacing or deleting one Team's picture must not remove the other's. An image whose file is gone is dropped.
    /// </summary>
    /// <param name="source">The source Team's avatar.</param>
    /// <returns>The avatar to store for the new Team.</returns>
    private Avatar CopyAvatar(Avatar source)
    {
        if (source.Image is null)
        {
            return source;
        }

        byte[]? bytes = avatars.ReadImage(source.Image);
        return bytes is null
            ? source with { Image = null }
            : source with { Image = avatars.WriteImage(bytes, Path.GetExtension(source.Image)) };
    }

    /// <summary>Reads each source member's role; members without a usable role go to <paramref name="skipped"/>.</summary>
    private List<SourceMember> ReadMembers(IReadOnlyList<string> names, List<SkippedMember> skipped)
    {
        List<SourceMember> members = [];
        foreach (string name in names)
        {
            Persona? persona = personas.Get(name);
            if (persona is null || !PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _) || identity.Role is null)
            {
                skipped.Add(new SkippedMember(name, NoRoleReason));
                continue;
            }

            RoleSpec? spec = types.Find(identity.Role.TypeId)?.Roles.FirstOrDefault(r => string.Equals(r.Id, identity.Role.RoleId, StringComparison.Ordinal));
            if (spec is null)
            {
                skipped.Add(new SkippedMember(name, RoleGoneReason));
                continue;
            }

            members.Add(new SourceMember(name, identity.Role.TypeId, spec));
        }

        return members;
    }

    /// <summary>Resolves a fresh card for the member's role and hires it into <paramref name="target"/>.</summary>
    private void CopyOne(SourceMember member, string target, List<string> hired, List<SkippedMember> skipped)
    {
        CardState card = CardResolver.Resolve(member.Spec, personas.ListNames(), context.Answered, context.TierMap, context.AvoidAdapters, skills, context.Connectors);
        if (card.Readiness != CardReadiness.Ready)
        {
            skipped.Add(new SkippedMember(member.Name, string.Join(", ", card.Reasons)));
            return;
        }

        HireResult result = hire.Hire(new HireRequest(target, member.TypeId, member.Spec.Id, card));
        if (result.PersonaName is null)
        {
            skipped.Add(new SkippedMember(member.Name, result.Error ?? string.Empty));
            return;
        }

        hired.Add(result.PersonaName);
        if (member.Spec.Manager)
        {
            string manager = result.PersonaName;
            _ = folders.UpdateTeamFile(target, current => current with { Manager = manager });
        }
    }

    private static DuplicateResult Refuse(string error) => new(false, error, [], []);

    private static string? WorkspaceProblem(WorkspaceSpec? spec, string? workspace)
    {
        if (spec is null || spec.Kind == WorkspaceKind.None)
        {
            return null;
        }

        if (workspace is null)
        {
            return spec.Required ? WorkspaceRequiredMessage : null;
        }

        return Directory.Exists(workspace) ? null : WorkspaceMissingMessage;
    }

    /// <summary>A source member with its resolved role.</summary>
    private sealed record SourceMember(string Name, string TypeId, RoleSpec Spec);
}

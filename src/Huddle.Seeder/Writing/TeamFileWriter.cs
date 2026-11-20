using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Writes each Team's <c>_team.md</c>, composed by the app's <see cref="TeamFile"/>, and a typed Team's <c>AGENTS.md</c>, read
/// through the app's <see cref="TeamTypeCatalog"/>. Seeder spec SEED-AV-5 and SEED-HOME-8.
/// </summary>
internal static class TeamFileWriter
{
    /// <summary>Writes every Team's files, live and archived, giving each a different palette colour.</summary>
    /// <param name="plan">The plan whose Teams are written.</param>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="roomIds">The Room ids by key, for the archived Teams' <c>archivedRooms</c>.</param>
    /// <exception cref="InvalidOperationException">A Team names a team type, or a type's process, that the app does not know.</exception>
    internal static void Write(SeedPlan plan, string dataDir, IReadOnlyDictionary<string, string> roomIds)
    {
        TeamTypeCatalog types = new(Options.Create(new TeamOptions { DataDir = dataDir }), NullLogger<TeamTypeCatalog>.Instance);

        // Archived Teams take the colours after the live ones, so adding one never repaints a live Team.
        IReadOnlyList<string> colours = AvatarWriter.Spread(plan.Teams.Count + plan.ArchivedTeams.Count);
        for (int index = 0; index < plan.ArchivedTeams.Count; index++)
        {
            WriteArchived(plan, plan.ArchivedTeams[index], colours[plan.Teams.Count + index], dataDir, roomIds);
        }

        for (int index = 0; index < plan.Teams.Count; index++)
        {
            SeedTeam team = plan.Teams[index];
            VersionedRef type = TypeOf(team);
            string folder = $"Teams/{team.Name}";
            ProcessSettings settings = ProcessSettings.Empty;
            if (team.TeamType is not null)
            {
                settings = ProcessSettings.Split(DefaultProcess(types, team, type), out string body);
                SeedIo.WriteText(SeedIo.Resolve(dataDir, $"{folder}/{TeamPaths.AgentsMdFileName}"), body);
            }

            SeedIo.WriteText(SeedIo.Resolve(dataDir, $"{folder}/{TeamPaths.TeamFileName}"), Compose(team, type, colours[index], settings, dataDir));

            EnsureScope(SeedIo.Resolve(dataDir, folder));
            foreach (string project in team.Projects)
            {
                EnsureScope(SeedIo.Resolve(dataDir, $"{folder}/{project}"));
            }

            // Archive project moves the whole Project folder, so it keeps its raw, memory and outputs folders.
            foreach (string project in team.ArchivedProjects)
            {
                EnsureScope(SeedIo.Resolve(dataDir, $"Archive/{team.Name}/{project}"));
            }
        }
    }

    /// <summary>
    /// Writes an archived Team as <c>ArchiveService</c> leaves it: <c>Archive/&lt;T&gt;/_team.md</c> holding the roster and the ids of
    /// the Team's Rooms (<c>archivedMembers</c>, <c>archivedRooms</c>), the Team and Project folders, and no manager.
    /// </summary>
    private static void WriteArchived(SeedPlan plan, SeedArchivedTeam team, string colour, string dataDir, IReadOnlyDictionary<string, string> roomIds)
    {
        string[] rooms = [.. plan.Rooms
            .Where(r => string.Equals(r.Team, team.Name, StringComparison.OrdinalIgnoreCase))
            .Select(r => roomIds[r.Key])];
        TeamFile file = new(new VersionedRef(BlankTeamType.Id, BlankTeamType.Manifest.Version), Manager: null, Workspace: null, Owns: null, Notes: string.Empty)
        {
            Onboarded = true,
            Avatar = new Avatar(Label: null, Image: null, Background: colour),
            ArchivedMembers = team.Members,
            ArchivedRooms = rooms,
        };

        SeedIo.WriteText(SeedIo.Resolve(dataDir, $"{team.Folder}/{TeamPaths.TeamFileName}"), file.Compose());
        EnsureScope(SeedIo.Resolve(dataDir, team.Folder));
        foreach (string project in team.Projects)
        {
            EnsureScope(SeedIo.Resolve(dataDir, $"{team.Folder}/{project}"));
        }
    }

    /// <summary>Creates a Team or Project folder with its <c>raw</c>, <c>memory</c> and <c>outputs</c> sub-folders (Spec §6.1).</summary>
    private static void EnsureScope(string fullPath)
    {
        Directory.CreateDirectory(fullPath);
        SecondBrainFolders.Ensure(fullPath);
    }

    /// <summary>
    /// The text of a Team's <c>_team.md</c>: its type (blank unless named), its manager, its "What it's for" line and its avatar
    /// colour, and the full path of its workspace when it has one (<see cref="WorkspaceWriter"/> creates it). It is always
    /// <c>onboarded: true</c>, so a seeded manager is never sent the onboarding prompt.
    /// </summary>
    private static string Compose(SeedTeam team, VersionedRef type, string colour, ProcessSettings settings, string dataDir) =>
        new TeamFile(type, team.Manager, team.Workspace is null ? null : WorkspaceWriter.PathOf(dataDir, team.Workspace), team.Owns, Notes: string.Empty)
        {
            Process = settings,
            Onboarded = true,
            Avatar = new Avatar(Label: null, Image: null, Background: colour),
        }.Compose();

    private static VersionedRef TypeOf(SeedTeam team)
    {
        if (team.TeamType is null)
        {
            return new VersionedRef(BlankTeamType.Id, BlankTeamType.Manifest.Version);
        }

        return VersionedRef.TryParse(team.TeamType, out VersionedRef? type)
            ? type
            : throw new InvalidOperationException($"Team '{team.Name}' names team type '{team.TeamType}', which is not id@version.");
    }

    private static string DefaultProcess(TeamTypeCatalog types, SeedTeam team, VersionedRef type)
    {
        TeamTypeManifest manifest = types.Find(type.Id)
            ?? throw new InvalidOperationException($"Team '{team.Name}' names team type '{type.Id}', which the app does not know.");
        if (manifest.Processes.Count == 0)
        {
            throw new InvalidOperationException($"Team type '{type.Id}' has no process for Team '{team.Name}'.");
        }

        ProcessRef process = manifest.Processes.FirstOrDefault(static p => p.Default) ?? manifest.Processes[0];
        return types.ReadProcess(type.Id, process.Id)
            ?? throw new InvalidOperationException($"Team type '{type.Id}' could not read its process '{process.Id}'.");
    }
}

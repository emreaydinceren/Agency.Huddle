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
    /// <summary>Writes every Team's files, giving each a different palette colour.</summary>
    /// <param name="plan">The plan whose Teams are written.</param>
    /// <param name="dataDir">The data folder.</param>
    /// <exception cref="InvalidOperationException">A Team names a team type, or a type's process, that the app does not know.</exception>
    internal static void Write(SeedPlan plan, string dataDir)
    {
        TeamTypeCatalog types = new(Options.Create(new TeamOptions { DataDir = dataDir }), NullLogger<TeamTypeCatalog>.Instance);
        IReadOnlyList<string> colours = AvatarWriter.Spread(plan.Teams.Count);
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

            SeedIo.WriteText(SeedIo.Resolve(dataDir, $"{folder}/{TeamPaths.TeamFileName}"), Compose(team, type, colours[index], settings));

            EnsureScope(SeedIo.Resolve(dataDir, folder));
            foreach (string project in team.Projects)
            {
                EnsureScope(SeedIo.Resolve(dataDir, $"{folder}/{project}"));
            }
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
    /// colour. It is always <c>onboarded: true</c>, so a seeded manager is never sent the onboarding prompt.
    /// </summary>
    private static string Compose(SeedTeam team, VersionedRef type, string colour, ProcessSettings settings) =>
        new TeamFile(type, team.Manager, Workspace: null, team.Owns, Notes: string.Empty)
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

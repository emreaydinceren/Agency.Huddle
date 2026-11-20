using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Creates the workspace folder of each Team that names one: an empty git repository (the app reads a folder holding a <c>.git</c> entry as a
/// repository, <c>WorkspaceProbe.StateOf</c>) with the Team's files in it. It sits in <c>&lt;root&gt;/workspaces/&lt;folder&gt;</c>, beside the
/// data folder, so the seed root stays the only thing a run writes and no network or <c>git</c> program is needed. Seeder spec §9A (SEED-DEV-6).
/// </summary>
internal static class WorkspaceWriter
{
    private const string WorkspacesFolder = "workspaces";

    /// <summary>The full path of a Team's workspace folder.</summary>
    /// <param name="dataDir">The data folder; the workspaces folder is its sibling.</param>
    /// <param name="folder">The <see cref="SeedTeam.Workspace"/> folder name.</param>
    /// <returns>The absolute path the Team's <c>workspace:</c> holds.</returns>
    internal static string PathOf(string dataDir, string folder) =>
        Path.GetFullPath(Path.Combine(dataDir, "..", WorkspacesFolder, folder));

    /// <summary>Writes the workspace of every Team that has one.</summary>
    /// <param name="plan">The plan whose Teams are read.</param>
    /// <param name="dataDir">The data folder.</param>
    internal static void Write(SeedPlan plan, string dataDir)
    {
        foreach (SeedTeam team in plan.Teams.Where(static t => t.Workspace is not null))
        {
            string root = PathOf(dataDir, team.Workspace ?? string.Empty);
            WriteRepository(root);
            foreach (SeedFile file in team.WorkspaceFiles)
            {
                SeedIo.WriteText(SeedIo.Resolve(root, file.RelativePath), file.Content);
            }
        }
    }

    /// <summary>The smallest repository <c>git</c> itself accepts: a <c>HEAD</c> on <c>main</c>, a config and the empty object and ref folders.</summary>
    private static void WriteRepository(string root)
    {
        string git = Path.Combine(root, ".git");
        SeedIo.WriteText(Path.Combine(git, "HEAD"), "ref: refs/heads/main");
        SeedIo.WriteText(Path.Combine(git, "config"), "[core]\n\trepositoryformatversion = 0\n\tfilemode = false\n\tbare = false\n");
        SeedIo.WriteText(Path.Combine(git, "description"), "Unnamed repository; edit this file 'description' to name the repository.");
        foreach (string folder in new[] { "objects/info", "objects/pack", "refs/heads", "refs/tags" })
        {
            _ = Directory.CreateDirectory(SeedIo.Resolve(git, folder));
        }
    }
}

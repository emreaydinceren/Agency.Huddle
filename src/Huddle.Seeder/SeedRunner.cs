using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Seeder;

/// <summary>What the caller asked the seeder to build.</summary>
/// <param name="Scenario">The scenario to build.</param>
/// <param name="Root">The seed root; wiped first.</param>
/// <param name="Today">The run date every offset is measured from.</param>
internal sealed record SeedRequest(IScenario Scenario, string Root, DateOnly Today);

/// <summary>What was built.</summary>
/// <param name="Root">The seed root.</param>
/// <param name="DataDir">The data folder Huddle should be pointed at.</param>
/// <param name="Plan">The plan that was written.</param>
/// <param name="Ids">The ids the database assigned.</param>
internal sealed record SeedSummary(string Root, string DataDir, SeedPlan Plan, DatabaseResult Ids);

/// <summary>Wipes the seed root and builds a scenario into it. Huddle is never started; everything goes through files and Huddle's own public data API.</summary>
internal static class SeedRunner
{
    private const string LayoutMarker = ".layout-migrated";

    /// <summary>Wipes <see cref="SeedRequest.Root"/> and writes the scenario into a fresh one.</summary>
    /// <param name="request">What to build and where.</param>
    /// <param name="protectedFolders">Folders the wipe must never touch.</param>
    /// <param name="ct">Cancels the database writes.</param>
    /// <returns>Where the data went and what was created.</returns>
    /// <exception cref="InvalidOperationException">The root is refused by <see cref="SeedRootGuard"/>, or is still in use.</exception>
    internal static async Task<SeedSummary> RunAsync(SeedRequest request, IReadOnlyList<ProtectedFolder> protectedFolders, CancellationToken ct)
    {
        SeedRootGuard.Wipe(request.Root, protectedFolders);

        string root = Path.GetFullPath(request.Root);
        string dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);
        SeedIo.WriteText(Path.Combine(root, SeedRootGuard.MarkerFileName), "This folder was created by huddle-seed and may be wiped by it.");

        SeedPlan plan = request.Scenario.Build(request.Today);

        // Without this marker Huddle's one-time layout migration moves every non-Task note under Teams/ into Teammates/_unsorted/.
        SeedIo.WriteText(SeedIo.Resolve(dataDir, "Teammates/" + LayoutMarker), string.Empty);

        DatabaseResult ids = await DatabaseSeeder.SeedAsync(plan, dataDir, ct);

        WriteFolders(plan, dataDir);
        WriteTeammates(plan, dataDir);
        WriteTasks(plan, dataDir, ids);
        WriteFiles(plan, dataDir);
        SeedIo.WriteText(Path.Combine(dataDir, "views.json"), plan.ViewsJson);
        SeedIo.WriteText(Path.Combine(dataDir, "avatars.json"), plan.AvatarsJson);
        SeedIo.WriteText(Path.Combine(root, "manifest.md"), ManifestWriter.Render(plan, ids, root));

        return new SeedSummary(root, dataDir, plan, ids);
    }

    private static void WriteFolders(SeedPlan plan, string dataDir)
    {
        foreach (SeedTeam team in plan.Teams)
        {
            Directory.CreateDirectory(SeedIo.Resolve(dataDir, $"Teams/{team.Name}"));
            foreach (string project in team.Projects)
            {
                Directory.CreateDirectory(SeedIo.Resolve(dataDir, $"Teams/{team.Name}/{project}"));
            }
        }
    }

    private static void WriteTeammates(SeedPlan plan, string dataDir)
    {
        foreach (SeedTeammate teammate in plan.Teammates)
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, $"Teammates/{teammate.Name}/{teammate.Name}.md"), TeammateFileWriter.RenderTeammate(teammate));
        }

        foreach (SeedSkill skill in plan.Skills)
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, $"Skills/{skill.Name}/SKILL.md"), TeammateFileWriter.RenderSkill(skill));
        }
    }

    private static void WriteTasks(SeedPlan plan, string dataDir, DatabaseResult ids)
    {
        foreach (SeedTask task in plan.Tasks)
        {
            string? origin = task.OriginRoom is null ? null : ids.RoomIds[task.OriginRoom];
            string path = SeedIo.Resolve(dataDir, TaskFileWriter.RelativePath(task));
            SeedIo.WriteText(path, TaskFileWriter.Render(task, plan.Today, origin));

            // Huddle compares the file's modified time with the last Change log entry at start-up; a newer file is
            // recorded as "edited outside Huddle" and loses the backdated Updated date.
            File.SetLastWriteTimeUtc(path, TaskFileWriter.BuildLog(task, plan.Today)[^1].At.UtcDateTime);
        }
    }

    private static void WriteFiles(SeedPlan plan, string dataDir)
    {
        foreach (SeedFile file in plan.Files)
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, file.RelativePath), file.Content);
        }
    }
}

using Agency.Huddle.App.Avatars;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Seeder;

/// <summary>Wipes the seed root and builds a scenario into it. Huddle is never started; everything goes through files and Huddle's own public data API. Seeder spec §5, the write order; Appendix A.2 (Seed a new kind of thing).</summary>
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

        // Without this marker Huddle's one-time layout migration moves every non-Task note under Teams/ into Agents/_unsorted/.
        SeedIo.WriteText(SeedIo.Resolve(dataDir, "Agents/" + LayoutMarker), string.Empty);

        DatabaseResult ids = await DatabaseSeeder.SeedAsync(plan, dataDir, ct).ConfigureAwait(false);
        ids = ids with { JobRoomIds = await JobSeeder.SeedAsync(plan, dataDir, ids, TimeProvider.System, ct).ConfigureAwait(false) };
        FeedSeeder.Write(plan, dataDir, ids, TimeProvider.System);

        WriteFolders(plan, dataDir, ids);
        WriteAgents(plan, dataDir);
        JobSeeder.WriteFiles(plan, dataDir);
        WriteTasks(plan, dataDir, ids);
        WriteFiles(plan, dataDir);
        WriteBinaryFiles(plan, dataDir, ids);
        WriteSprints(plan, dataDir);
        DocHistoryWriter.Write(plan, dataDir);
        SeedIo.WriteText(Path.Combine(dataDir, "views.json"), plan.ViewsJson);
        await AvatarWriter.WriteAsync(plan, dataDir, AvatarLibraryLocator.BundledDir, ct).ConfigureAwait(false);
        SeedIo.WriteText(Path.Combine(root, "manifest.md"), ManifestWriter.Render(plan, ids, root));

        return new SeedSummary(root, dataDir, plan, ids);
    }

    private static void WriteFolders(SeedPlan plan, string dataDir, DatabaseResult ids)
    {
        TeamFileWriter.Write(plan, dataDir, ids.RoomIds);
        foreach (SeedTeam team in plan.Teams)
        {
            foreach (string project in team.Projects)
            {
                Directory.CreateDirectory(SeedIo.Resolve(dataDir, $"Teams/{team.Name}/{project}"));
            }
        }
    }

    private static void WriteAgents(SeedPlan plan, string dataDir)
    {
        foreach (SeedAgent agent in plan.Agents)
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, $"Agents/{agent.Name}/{agent.Name}.md"), AgentFileWriter.RenderAgent(agent));
        }

        foreach (SeedSkill skill in plan.Skills)
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, $"Skills/{skill.Name}/SKILL.md"), AgentFileWriter.RenderSkill(skill));
        }
    }

    private static void WriteTasks(SeedPlan plan, string dataDir, DatabaseResult ids)
    {
        foreach (SeedTask task in plan.Tasks)
        {
            string? origin = task.OriginRoom is null ? null : ids.RoomIds[task.OriginRoom];
            string path = SeedIo.Resolve(dataDir, TaskFileWriter.RelativePath(task));
            SeedIo.WriteText(path, TaskFileWriter.Render(task, plan.Today, origin, dataDir));

            // Huddle compares the file's modified time with the last timeline entry (of any kind) at start-up; a
            // newer file is recorded as "edited outside Huddle" and loses the backdated Updated date.
            File.SetLastWriteTimeUtc(path, TaskFileWriter.BuildTimeline(task, plan.Today)[^1].At.UtcDateTime);
        }
    }

    private static void WriteBinaryFiles(SeedPlan plan, string dataDir, DatabaseResult ids)
    {
        foreach (SeedBinaryFile file in plan.BinaryFiles)
        {
            SeedIo.WriteBytes(SeedIo.Resolve(dataDir, file.RelativePath), file.Content);
        }

        // Where the app's upload flow would have saved each file: the folder depends on the Room's Scope and its id.
        foreach (SeedUpload upload in plan.Uploads)
        {
            SeedRoom room = plan.Rooms.First(r => string.Equals(r.Key, upload.RoomKey, StringComparison.Ordinal));
            SeedIo.WriteBytes(Path.Combine(MessageReferences.UploadFolder(dataDir, room, ids.RoomIds[room.Key]), upload.FileName), upload.Content);
        }

        // A Task's uploads live in a folder keyed by its id, so closing or re-filing the Task never moves them.
        foreach (SeedTaskUpload upload in plan.TaskUploads)
        {
            SeedIo.WriteBytes(SeedIo.Resolve(dataDir, upload.RelativePath), upload.Content);
        }
    }

    private static void WriteSprints(SeedPlan plan, string dataDir)
    {
        if (plan.Sprints.Count == 0)
        {
            return;
        }

        string text = "# Sprints\n\n" + string.Join('\n', plan.Sprints.Select(s => $"- {s.Id} | {s.Name}"));
        SeedIo.WriteText(Path.Combine(dataDir, "sprints.md"), text);
    }

    private static void WriteFiles(SeedPlan plan, string dataDir)
    {
        foreach (SeedFile file in plan.Files)
        {
            SeedIo.WriteText(SeedIo.Resolve(dataDir, file.RelativePath), file.Content);
        }
    }
}

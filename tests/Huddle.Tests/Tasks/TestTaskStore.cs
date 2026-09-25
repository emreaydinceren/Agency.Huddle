using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Shared fixture helpers for <see cref="TaskStore"/> tests: constructing a real
/// <see cref="PersonaStore"/> and <see cref="TaskStore"/> over the same <see cref="TempDataDir"/>,
/// and writing a valid Task file to disk. Moved out of <c>TaskStoreTests</c> (Task 5.4.t) so
/// <c>TaskStoreWatcherTests</c> can share them without copying.
/// </summary>
internal static class TestTaskStore
{
    /// <summary>Returns the Tasks scan root under a temporary data directory.</summary>
    /// <param name="dir">The temporary data directory a Task store under test scans.</param>
    public static string Root(TempDataDir dir) => Path.Combine(dir.Path, "Teams");

    /// <summary>Returns a Task file's path, relative to <see cref="Root"/>, in today's on-disk layout.</summary>
    /// <param name="team">The owning Team's label.</param>
    /// <param name="project">The owning Project's label, or <see langword="null"/> for a Team-level Task.</param>
    /// <param name="closed">Whether the Task lives under the closed folder.</param>
    /// <param name="fileName">The Task file's name, including extension.</param>
    public static string RelativePath(string team, string? project, bool closed, string fileName)
    {
        List<string> segments = [team];
        if (project is not null)
        {
            segments.Add(project);
        }

        segments.Add("_tasks");

        if (closed)
        {
            segments.Add("_closed");
        }

        segments.Add(fileName);
        return Path.Combine([.. segments]);
    }

    /// <summary>Writes a valid Task file's composed text under <paramref name="root"/>, and backdates it to its last Change log entry's time (or a fixed past time with none) so a later reconciliation pass never rewrites a fixture.</summary>
    /// <param name="root">The Tasks scan root.</param>
    /// <param name="relativePath">The file's path, relative to <paramref name="root"/>.</param>
    /// <param name="task">The Task to compose and write.</param>
    public static string WriteTask(string root, string relativePath, TaskItem task)
    {
        string path = Path.Combine(root, relativePath);
        string? directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, TaskFileFormat.Compose(task));
        DateTimeOffset lastWrite = task.ChangeLog.Count > 0 ? task.ChangeLog[^1].At : new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        return path;
    }

    /// <summary>Constructs a real <see cref="PersonaStore"/> over the same <see cref="TempDataDir"/> a <see cref="TaskStore"/> under test also reads from.</summary>
    /// <param name="dir">The temporary data directory shared with the Task store under test.</param>
    public static PersonaStore CreatePersonaStore(TempDataDir dir) =>
        new(new TeammatePaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), NullLogger<PersonaStore>.Instance);

    /// <summary>Constructs the <see cref="TaskStore"/> under test with default options and a real <see cref="TimeProvider"/>.</summary>
    /// <param name="dir">The temporary data directory to scan.</param>
    /// <param name="personas">The Persona store the Task store checks Team labels against.</param>
    public static TaskStore CreateTaskStore(TempDataDir dir, PersonaStore personas) =>
        new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
}

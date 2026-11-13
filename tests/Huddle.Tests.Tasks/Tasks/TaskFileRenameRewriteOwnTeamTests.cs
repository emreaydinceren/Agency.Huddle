using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins that the Task-file rewrite never scans the renamed Team's own folder: its Tasks moved with it, so even a body that
/// cites <c>Teams/Marketing/raw/x.png</c> stays byte for byte and gains no change-log entry.
/// </summary>
public sealed class TaskFileRenameRewriteOwnTeamTests
{
    private static readonly TeamRename Rename = new(1, TeamRenameKind.Team, "Marketing", "Marketing", "Growth", TeamRenameState.Pending, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>An own Task citing the old Team's path is untouched, has no change-log line and does not list Growth in TeamsTouched.</summary>
    [Fact]
    public async Task RunAsync_OwnTaskCitesOldTeamPath_UntouchedAndNotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        const string id = "GROW-0001";
        TaskItem task = TestTasks.Make(id: id, location: new("Growth", null, false), description: "See `/data/Teams/Marketing/raw/x.png`.", path: string.Empty);
        string path = TestTaskStore.WriteTask(TestTaskStore.Root(dir), Path.Combine("Growth", "_tasks", id + ".md"), task);
        byte[] before = await File.ReadAllBytesAsync(path, ct);
        TaskFileRenameRewrite rewrite = new(dir.Options());

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        Assert.DoesNotContain("Growth", touched, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
        Assert.DoesNotContain("Team renamed", await File.ReadAllTextAsync(path, ct), StringComparison.Ordinal);
    }
}

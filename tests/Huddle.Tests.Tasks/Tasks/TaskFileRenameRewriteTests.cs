using System.Globalization;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins the Task-file rewrite of a Team rename (Spec R5, section 4 rows Requests and Attachments): another Team's Task
/// that names the old Team in <c>from_team</c> or an attachment path is rewritten on disk and gains one change-log entry.
/// </summary>
public sealed class TaskFileRenameRewriteTests
{
    private static readonly TeamRename Rename = new(1, TeamRenameKind.Team, "Marketing", "Marketing", "Growth", TeamRenameState.Pending, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>Writes a Task of <paramref name="team"/> and returns its file path.</summary>
    /// <param name="dir">The data directory.</param>
    /// <param name="team">The Team that holds the Task.</param>
    /// <param name="number">The Task number.</param>
    /// <param name="fromTeam">The <c>from_team</c> value, or null for an ordinary Task.</param>
    /// <param name="description">The Task description.</param>
    private static string Write(TempDataDir dir, string team, int number, string? fromTeam, string description = "")
    {
        string prefix = team.ToUpperInvariant()[..4];
        string id = string.Create(CultureInfo.InvariantCulture, $"{prefix}-{number:0000}");
        IReadOnlyList<KeyValuePair<string, string>> unknown = fromTeam is null
            ? []
            : [new("request", "bug"), new("from_team", fromTeam), new("requester", "Joe"), new("triage", ""), new("decline_reason", ""), new("confirmed_by", "")];
        TaskItem task = TestTasks.Make(id: id, location: new(team, null, false), description: description, unknownFields: unknown, path: "");
        return TestTaskStore.WriteTask(TestTaskStore.Root(dir), Path.Combine(team, "_tasks", id + ".md"), task);
    }

    /// <summary>Builds the rewrite over the data directory.</summary>
    /// <param name="dir">The data directory.</param>
    private static TaskFileRenameRewrite Create(TempDataDir dir) => new(dir.Options());

    /// <summary>R5: a Request Research holds from Marketing now says Growth, gains one change-log entry, and only Research is touched.</summary>
    [Fact]
    public async Task RunAsync_RequestFromRenamedTeam_RewritesFromTeamAndLogsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Write(dir, "Research", 1, "Marketing");
        TaskFileRenameRewrite rewrite = Create(dir);

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        string text = await File.ReadAllTextAsync(path, ct);
        Assert.Equal(["Research"], touched);
        Assert.Contains("from_team: Growth", text, StringComparison.Ordinal);
        Assert.DoesNotContain("from_team: Marketing", text, StringComparison.Ordinal);
        Assert.Single(text.Split('\n'), line => line.TrimEnd('\r').EndsWith("| Huddle | Team renamed from Marketing to Growth", StringComparison.Ordinal));
    }

    /// <summary>A Request from a different Team is left byte for byte, and its Team is not listed.</summary>
    [Fact]
    public async Task RunAsync_RequestFromOtherTeam_Untouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Write(dir, "Research", 1, "Design");
        byte[] before = await File.ReadAllBytesAsync(path, ct);
        TaskFileRenameRewrite rewrite = Create(dir);

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        Assert.Empty(touched);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
    }

    /// <summary>An attachment path under Teams/Marketing/ is rewritten; Teams/Marketing2/ is not.</summary>
    /// <param name="root">The path up to the Teams folder, without a trailing separator.</param>
    /// <param name="separator">The separator the stored path uses.</param>
    [Theory]
    [InlineData("C:\\Data", '\\')]
    [InlineData("/data", '/')]
    public async Task RunAsync_AttachmentPaths_RewritesOnlyTheWholeTeamSegment(string root, char separator)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string Join(string team, string file) => string.Join(separator, root, "Teams", team, "Docs", file);
        string description = $"See these.\n\n`{Join("Marketing", "plan.md")}`\n`{Join("Marketing2", "other.md")}`";
        string path = Write(dir, "Research", 1, null, description);
        TaskFileRenameRewrite rewrite = Create(dir);

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        string text = await File.ReadAllTextAsync(path, ct);
        Assert.Equal(["Research"], touched);
        Assert.Contains($"`{Join("Growth", "plan.md")}`", text, StringComparison.Ordinal);
        Assert.Contains($"`{Join("Marketing2", "other.md")}`", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"Teams{separator}Marketing{separator}", text, StringComparison.Ordinal);
        Assert.Contains("| Team renamed from Marketing to Growth", text, StringComparison.Ordinal);
    }

    /// <summary>The renamed Team's own Task (moved with the folder) is untouched and gets no change-log line.</summary>
    [Fact]
    public async Task RunAsync_RenamedTeamsOwnTask_UntouchedAndNotListed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Write(dir, "Growth", 1, null, "Plain description.");
        byte[] before = await File.ReadAllBytesAsync(path, ct);
        TaskFileRenameRewrite rewrite = Create(dir);

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        Assert.Empty(touched);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
    }

    /// <summary>A second run changes nothing and appends no second change-log entry.</summary>
    [Fact]
    public async Task RunAsync_RunTwice_SecondRunChangesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Write(dir, "Research", 1, "Marketing", "`C:\\Data\\Teams\\Marketing\\Docs\\plan.md`");
        TaskFileRenameRewrite rewrite = Create(dir);
        await rewrite.RunAsync(Rename, ct);
        byte[] afterFirst = await File.ReadAllBytesAsync(path, ct);

        IReadOnlyList<string> touchedAgain = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        Assert.Empty(touchedAgain);
        Assert.Equal(afterFirst, await File.ReadAllBytesAsync(path, ct));
    }

    /// <summary>After the pause is lifted the Task store sees the rewritten Task as already known: no outside edit.</summary>
    [Fact]
    public async Task RunAsync_UnderPause_RaisesNoOutsideEditAfterLift()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        _ = Write(dir, "Research", 1, "Marketing");
        using PersonaStore personas = dir.CreatePersonaStore();
        using TaskStore store = TestTaskStore.CreateTaskStore(dir, personas);
        List<OutsideEdit> edits = [];
        store.OutsideEditDetected += edits.Add;
        TaskFileRenameRewrite rewrite = Create(dir);

        using (store.PauseWrites("Research"))
        {
            await rewrite.RunAsync(Rename, ct);
        }

        store.RebuildFromWatcher();
        Assert.Empty(edits);
    }
}

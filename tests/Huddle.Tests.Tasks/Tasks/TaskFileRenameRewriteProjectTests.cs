using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins the Project form of the Task-file rewrite (Team Rename tr11 item 5): another Team's Task with an attachment path under
/// <c>Teams/&lt;Team&gt;/&lt;OldProject&gt;/</c> is rewritten once and gains one change-log entry; a longer Project name, the same
/// Project name under another Team and <c>from_team</c> are left alone.
/// </summary>
public sealed class TaskFileRenameRewriteProjectTests
{
    private static readonly TeamRename Rename = new(1, TeamRenameKind.Project, "Growth", "Launch", "Debut", TeamRenameState.Pending, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

    private static string Write(TempDataDir dir, string description, string? fromTeam = null)
    {
        IReadOnlyList<KeyValuePair<string, string>> unknown = fromTeam is null
            ? []
            : [new("request", "bug"), new("from_team", fromTeam), new("requester", "Joe"), new("triage", ""), new("decline_reason", ""), new("confirmed_by", "")];
        TaskItem task = TestTasks.Make(id: "RESE-0001", location: new("Research", null, false), description: description, unknownFields: unknown, path: "");
        return TestTaskStore.WriteTask(TestTaskStore.Root(dir), Path.Combine("Research", "_tasks", "RESE-0001.md"), task);
    }

    /// <summary>The path under Teams/Growth/Launch/ is rewritten, the sibling, the other Team's Launch and the Team-level path stay, with one change-log line.</summary>
    /// <param name="root">The path up to the Teams folder, without a trailing separator.</param>
    /// <param name="separator">The separator the stored path uses.</param>
    [Theory]
    [InlineData("C:\\Data", '\\')]
    [InlineData("/data", '/')]
    public async Task RunAsync_ProjectAttachmentPaths_RewritesOnlyTheWholeProjectSegment(string root, char separator)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string Join(params string[] parts) => string.Join(separator, [root, "Teams", .. parts]);
        string description = $"`{Join("Growth", "Launch", "plan.md")}`\n`{Join("Growth", "Launch2", "a.md")}`\n`{Join("Ops", "Launch", "b.md")}`\n`{Join("Growth", "c.md")}`";
        string path = Write(dir, description);
        TaskFileRenameRewrite rewrite = new(dir.Options());

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        string text = await File.ReadAllTextAsync(path, ct);
        Assert.Equal(["Research"], touched);
        Assert.Contains($"`{Join("Growth", "Debut", "plan.md")}`", text, StringComparison.Ordinal);
        Assert.Contains($"`{Join("Growth", "Launch2", "a.md")}`", text, StringComparison.Ordinal);
        Assert.Contains($"`{Join("Ops", "Launch", "b.md")}`", text, StringComparison.Ordinal);
        Assert.Contains($"`{Join("Growth", "c.md")}`", text, StringComparison.Ordinal);
        Assert.Single(text.Split('\n'), static line => line.TrimEnd('\r').EndsWith("| Huddle | event | Project renamed from Launch to Debut", StringComparison.Ordinal));
    }

    /// <summary>A Request whose <c>from_team</c> is the Project's Team is not a Project reference: nothing is rewritten and no Team is listed.</summary>
    [Fact]
    public async Task RunAsync_FromTeamNamesTheProjectsTeam_Untouched()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        string path = Write(dir, "Plain.", "Growth");
        byte[] before = await File.ReadAllBytesAsync(path, ct);
        TaskFileRenameRewrite rewrite = new(dir.Options());

        IReadOnlyList<string> touched = rewrite.TeamsTouched(Rename);
        await rewrite.RunAsync(Rename, ct);

        Assert.Empty(touched);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, ct));
    }
}

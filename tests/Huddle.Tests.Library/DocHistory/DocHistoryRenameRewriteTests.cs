using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>
/// <see cref="DocHistoryRenameRewrite"/> (Team Rename Spec 4, Library root and History): the doc-history of <c>teams/&lt;Old&gt;</c>,
/// <c>archive/&lt;Old&gt;</c> and the workspace root, whose id <c>workspace-{slug}</c> is computed from the Team name, moves to the new
/// names. A case-only rename keeps the workspace slug, so that history stays; a second run changes nothing.
/// </summary>
public sealed class DocHistoryRenameRewriteTests : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    private static TeamRename Rename(string oldName, string newName) => new(1, TeamRenameKind.Team, oldName, oldName, newName, TeamRenameState.Pending, Stamp);

    private string History(params string[] segments) => Path.Combine([this.dir.Path, "doc-history", .. segments]);

    private DocHistoryRenameRewrite NewRewrite() => new(new DocHistoryStore(this.dir.Options(), NullLogger<DocHistoryStore>.Instance));

    private void Seed(params string[] segments)
    {
        string file = this.History(segments);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file) ?? this.dir.Path);
        File.WriteAllText(file, "{}\n");
    }

    /// <summary>The Team's, the archived Team's and the workspace root's histories move to the new names, with their files.</summary>
    [Fact]
    public async Task RunAsync_MovesTeamArchiveAndWorkspaceHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Seed("teams", "Growth", "plan.md.jsonl");
        this.Seed("teams", "Growth", "sub", "notes.md.jsonl");
        this.Seed("archive", "Growth", "old.md.jsonl");
        this.Seed("workspace-growth", "src", "a.cs.jsonl");
        this.Seed("teams", "Growth2", "other.md.jsonl");

        await this.NewRewrite().RunAsync(Rename("Growth", "Revenue"), ct);

        Assert.True(File.Exists(this.History("teams", "Revenue", "plan.md.jsonl")));
        Assert.True(File.Exists(this.History("teams", "Revenue", "sub", "notes.md.jsonl")));
        Assert.True(File.Exists(this.History("archive", "Revenue", "old.md.jsonl")));
        Assert.True(File.Exists(this.History("workspace-revenue", "src", "a.cs.jsonl")));
        Assert.False(Directory.Exists(this.History("teams", "Growth")));
        Assert.False(Directory.Exists(this.History("archive", "Growth")));
        Assert.False(Directory.Exists(this.History("workspace-growth")));
        Assert.True(File.Exists(this.History("teams", "Growth2", "other.md.jsonl")));
    }

    /// <summary>A second run finds nothing under the old names and leaves the moved history where the first run put it.</summary>
    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Seed("teams", "Growth", "plan.md.jsonl");
        this.Seed("workspace-growth", "a.md.jsonl");
        DocHistoryRenameRewrite rewrite = this.NewRewrite();

        await rewrite.RunAsync(Rename("Growth", "Revenue"), ct);
        await rewrite.RunAsync(Rename("Growth", "Revenue"), ct);

        Assert.True(File.Exists(this.History("teams", "Revenue", "plan.md.jsonl")));
        Assert.True(File.Exists(this.History("workspace-revenue", "a.md.jsonl")));
        Assert.Empty(rewrite.TeamsTouched(Rename("Growth", "Revenue")));
    }

    /// <summary>A case-only rename gives the same workspace slug, so the workspace history is not moved or lost; the Team folder takes the new case.</summary>
    [Fact]
    public async Task RunAsync_CaseOnlyRename_KeepsWorkspaceHistory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.Seed("workspace-growth", "a.md.jsonl");
        this.Seed("teams", "Growth", "plan.md.jsonl");

        await this.NewRewrite().RunAsync(Rename("Growth", "growth"), ct);

        Assert.True(File.Exists(this.History("workspace-growth", "a.md.jsonl")));
        Assert.Equal(["growth"], Directory.GetDirectories(this.History("teams")).Select(Path.GetFileName));
        Assert.True(File.Exists(this.History("teams", "growth", "plan.md.jsonl")));
    }
}

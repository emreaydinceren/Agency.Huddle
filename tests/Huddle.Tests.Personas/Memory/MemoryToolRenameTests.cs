using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Memory;

/// <summary>Pins the old-name redirect of the <c>memory</c> tool (Team Rename R9, R10): list and read follow a renamed Team, a write is refused.</summary>
public sealed class MemoryToolRenameTests
{
    private const string Note = "Team 'Marketing' was renamed to 'Growth'";

    private const string Description = "How the Growth Team ships a release, with enough words to pass the description check.";

    /// <summary>Growth (manager Ada, member Leo) was Marketing; one file sits in its memory folder.</summary>
    /// <param name="harness">The harness whose catalog and journal are filled.</param>
    private static void Seed(MemoryToolHarness harness)
    {
        harness.Catalog.Teams = [new TeamSummary("Growth", [], ["Leo"], true) { Manager = "Ada" }];
        harness.Renames.Complete(harness.Renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));
        MemoryToolHarness.Put(harness.TeamMemoryDir("Growth"), "ship.md", $"---\ndescription: {Description}\n---\n\nTuesdays.\n");
    }

    /// <summary>A list of folder <c>Marketing</c> is served from Growth and starts with the rename note.</summary>
    [Fact]
    public async Task List_OldTeamName_IsServedFromTheNewFolderWithNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string listed = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("list", ("folder", "Marketing")), ct);

        Assert.StartsWith(Note, listed, StringComparison.Ordinal);
        Assert.Contains($"ship.md - {Description}", listed, StringComparison.Ordinal);
    }

    /// <summary>A read of folder <c>Marketing/</c> is served from Growth and starts with the rename note.</summary>
    [Fact]
    public async Task Read_OldTeamName_IsServedFromTheNewFolderWithNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string read = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("read", ("folder", "Marketing"), ("file", "ship.md")), ct);

        Assert.StartsWith(Note, read, StringComparison.Ordinal);
        Assert.Contains("Tuesdays.", read, StringComparison.Ordinal);
    }

    /// <summary>A write to folder <c>Marketing</c> is refused with the exact sentence and writes nothing, even for the manager.</summary>
    [Fact]
    public async Task Write_OldTeamName_IsRefusedAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string result = await harness.RunAsAsync(
            "Ada",
            MemoryToolHarness.Args("write", ("folder", "Marketing"), ("file", "new.md"), ("description", Description), ("text", "Body.")),
            ct);

        Assert.Equal("Team 'Marketing' was renamed to 'Growth'. Use 'Growth'.", result);
        Assert.False(File.Exists(Path.Combine(harness.TeamMemoryDir("Growth"), "new.md")));
        Assert.False(Directory.Exists(Path.Combine(harness.TeamsRoot, "Marketing")));
    }
}

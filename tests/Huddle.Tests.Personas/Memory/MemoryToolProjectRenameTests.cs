using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Memory;

/// <summary>Pins the old-name redirect of the <c>memory</c> tool for a renamed Project (Team Rename R12): list and read follow it, a write is refused.</summary>
public sealed class MemoryToolProjectRenameTests
{
    private const string Note = "Project 'Growth/Launch' was renamed to 'Growth/Launch-2027'";

    private const string Description = "How the Launch project ships, with enough words to pass the description check.";

    /// <summary>Growth (manager Ada, member Leo) has the Project Launch-2027, once Launch; one file sits in its memory folder.</summary>
    /// <param name="harness">The harness whose catalog and journal are filled.</param>
    private static void Seed(MemoryToolHarness harness)
    {
        harness.Catalog.Teams = [new TeamSummary("Growth", ["Launch-2027"], ["Leo"], true) { Manager = "Ada" }];
        harness.Renames.Complete(harness.Renames.Begin(TeamRenameKind.Project, "Growth", "Launch", "Launch-2027"));
        MemoryToolHarness.Put(harness.TeamMemoryDir("Growth", "Launch-2027"), "ship.md", $"---\ndescription: {Description}\n---\n\nTuesdays.\n");
    }

    /// <summary>A read of folder <c>Growth/Launch</c> is served from the renamed Project and starts with the Project note.</summary>
    [Fact]
    public async Task Read_OldProjectName_IsServedFromTheNewFolderWithNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string read = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("read", ("folder", "Growth/Launch"), ("file", "ship.md")), ct);

        Assert.StartsWith(Note, read, StringComparison.Ordinal);
        Assert.Contains("Tuesdays.", read, StringComparison.Ordinal);
    }

    /// <summary>A write to folder <c>Growth/Launch</c> is refused with the exact sentence and writes nothing.</summary>
    [Fact]
    public async Task Write_OldProjectName_IsRefusedAndWritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string result = await harness.RunAsAsync(
            "Ada",
            MemoryToolHarness.Args("write", ("folder", "Growth/Launch"), ("file", "new.md"), ("description", Description), ("text", "Body.")),
            ct);

        Assert.Equal("Project 'Growth/Launch' was renamed to 'Growth/Launch-2027'. Use 'Growth/Launch-2027'.", result);
        Assert.False(File.Exists(Path.Combine(harness.TeamMemoryDir("Growth", "Launch-2027"), "new.md")));
        Assert.False(Directory.Exists(Path.Combine(harness.TeamsRoot, "Growth", "Launch")));
    }
}

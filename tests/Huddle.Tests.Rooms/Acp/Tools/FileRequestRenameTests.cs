using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Pins the old-name refusal of <see cref="FileRequestTool"/> (Team Rename R10): a Request to a renamed Team's old name is refused with the new name.</summary>
public sealed class FileRequestRenameTests
{
    private const string Body = "## Steps to reproduce\nOpen the page.\n\n## Expected and actual\nIt should load; it does not.";

    private static FakeTeamCatalog Catalog() => new()
    {
        Teams =
        [
            new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova", TeamType = new VersionedRef("development", "1") },
            new TeamSummary("Design", [], ["Dana"], true) { Manager = "Dana", TeamType = new VersionedRef("design", "2") },
        ],
    };

    private static JsonObject Arguments(string toTeam) => new()
    {
        ["toTeam"] = toTeam,
        ["kind"] = "bug",
        ["title"] = "Page does not load",
        ["body"] = Body,
    };

    private static async Task<(TaskToolHarness Harness, FileRequestTool Tool)> BuildAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        harness.Personas.Add(new PersonaIdentity("Dana", "Dana", "Dana", ["Design"]), "You are Dana.");
        TeamRenameStore renames = new(harness.Options);
        renames.Complete(renames.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Design"));
        FileRequestTool tool = new(
            Catalog(),
            harness.Service,
            harness.Store,
            new FakeRequestNotifier(),
            harness.Directory,
            harness.NovaId ?? throw new InvalidOperationException("Harness not seeded."),
            new FakePromptSource(),
            renames);
        return (harness, tool);
    }

    /// <summary>A Request to the old name of a renamed Team is refused with the new name and files nothing.</summary>
    [Fact]
    public async Task FileRequest_OldTeamName_IsRefusedWithNewName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskToolHarness harness, FileRequestTool tool) = await BuildAsync(ct);
        using TaskToolHarness owned = harness;

        string result = await tool.InvokeAsync(Arguments("Marketing"), ct);

        Assert.Equal("Team 'Marketing' was renamed to 'Design'. Use 'Design'.", result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>A name that was never journalled keeps today's error.</summary>
    [Fact]
    public async Task FileRequest_NeverJournalledUnknownTeam_KeepsTodaysError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskToolHarness harness, FileRequestTool tool) = await BuildAsync(ct);
        using TaskToolHarness owned = harness;

        string result = await tool.InvokeAsync(Arguments("Ghost"), ct);

        Assert.Equal("There is no Team named 'Ghost'; call list_teams.", result);
    }
}

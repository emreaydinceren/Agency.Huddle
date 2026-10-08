using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Everything the retro proposal tests (Task 76) share: the Task harness, the Platform Team folder on disk with a
/// <c>Process.md</c> and a <c>_team.md</c>, a room with Nova (the proposer) and the real store and service.
/// </summary>
internal sealed class RetroProposalFixture : IDisposable
{
    /// <summary>The text of <c>Process.md</c> the fixture starts with.</summary>
    public const string ProcessText = "# Process\n\nReview before done.\nOne PR per Task.\n";

    /// <summary>The text of <c>_team.md</c> the fixture starts with.</summary>
    public const string TeamText = "Team notes: keep it small.\n";

    private RetroProposalFixture(TaskToolHarness harness, string roomId, string otherRoomId)
    {
        this.Harness = harness;
        this.RoomId = roomId;
        this.OtherRoomId = otherRoomId;
        this.TeamFolder = Path.Combine(harness.Options.Value.DataDir, "platform-folder");
        _ = Directory.CreateDirectory(this.TeamFolder);
        File.WriteAllText(Path.Combine(this.TeamFolder, TeamPaths.ProcessFileName), ProcessText);
        File.WriteAllText(Path.Combine(this.TeamFolder, TeamPaths.TeamFileName), TeamText);
        this.Catalog = new FakeTeamCatalog
        {
            Teams =
            [
                new TeamSummary("Platform", [], ["Nova", "Kai"], true) { Manager = "Nova" },
                new TeamSummary("Other", [], ["Kai"], true),
            ],
        };
        DiskTeamFolders folders = new(this.TeamFolder);
        this.Folders = folders;
        this.Proposals = new RetroProposalStore(folders);
        this.Retro = new RetroFileStore(folders);
        this.Service = new RetroProposalService(
            this.Proposals,
            this.Catalog,
            folders,
            harness.Personas,
            this.Retro,
            harness.Directory,
            harness.Chat,
            this.Clock,
            NullLogger<RetroProposalService>.Instance);
    }

    /// <summary>The Task tool harness with Nova and Kai.</summary>
    public TaskToolHarness Harness { get; }

    /// <summary>The Human's room with Nova, the proposer.</summary>
    public string RoomId { get; }

    /// <summary>The Human's room with Kai only.</summary>
    public string OtherRoomId { get; }

    /// <summary>The Platform Team folder.</summary>
    public string TeamFolder { get; }

    /// <summary>The Team catalog with Platform and Other.</summary>
    public FakeTeamCatalog Catalog { get; }

    /// <summary>The folders the stores use.</summary>
    public DiskTeamFolders Folders { get; }

    /// <summary>The clock the service reads.</summary>
    public ManualTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The proposal store.</summary>
    public RetroProposalStore Proposals { get; }

    /// <summary>The register store.</summary>
    public RetroFileStore Retro { get; }

    /// <summary>The service under test.</summary>
    public RetroProposalService Service { get; }

    /// <summary>The text of Platform's <c>Process.md</c> now.</summary>
    public string ProcessNow => File.ReadAllText(Path.Combine(this.TeamFolder, TeamPaths.ProcessFileName));

    /// <summary>The text of Platform's <c>_team.md</c> now.</summary>
    public string TeamNow => File.ReadAllText(Path.Combine(this.TeamFolder, TeamPaths.TeamFileName));

    /// <summary>Builds the fixture.</summary>
    /// <param name="ct">Cancels the seeding.</param>
    public static async Task<RetroProposalFixture> CreateAsync(CancellationToken ct)
    {
        TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Retro", [KnownIds.Human, harness.NovaId ?? ""], ct);
        Room other = await harness.Directory.CreateRoomAsync("Chat", [KnownIds.Human, harness.KaiId ?? ""], ct);
        return new RetroProposalFixture(harness, room.Id, other.Id);
    }

    /// <summary>Stores a proposal set from Nova for Platform and returns it.</summary>
    /// <param name="changes">The changes; ids are set to 1, 2, 3 in order.</param>
    public RetroProposalSet Propose(params RetroChange[] changes)
    {
        RetroProposalSet set = new(Guid.NewGuid().ToString("N"), "Platform", "Nova", changes);
        Assert.True(this.Proposals.Add(set));
        return set;
    }

    /// <summary>Makes a change.</summary>
    /// <param name="id">The change id.</param>
    /// <param name="targetFile">The file to edit.</param>
    /// <param name="find">The text to find.</param>
    /// <param name="replace">The replacement.</param>
    public static RetroChange Change(string id, string targetFile, string find, string replace) =>
        new(id, "Title " + id, targetFile, find, replace);

    /// <inheritdoc />
    public void Dispose() => this.Harness.Dispose();
}

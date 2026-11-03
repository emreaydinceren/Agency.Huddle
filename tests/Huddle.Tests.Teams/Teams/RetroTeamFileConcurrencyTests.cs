using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins that an accepted <c>_team.md</c> retro change is applied to the file as it is at accept time, not as it was at proposal time.</summary>
public sealed class RetroTeamFileConcurrencyTests
{
    /// <summary>A key changed after the proposal was made survives an accepted change to another part of the file.</summary>
    [Fact]
    public async Task AcceptAsync_TeamFileChangedSinceProposal_KeepsTheNewKey()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "_team.md", "keep it small", "keep it tiny"));
        Assert.True(fixture.Folders.UpdateTeamFile("Platform", static current => current with { Manager = "Kai" }));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Applied, outcome.Kind);
        Assert.Equal("Kai", fixture.Folders.File?.Manager);
        Assert.Contains("keep it tiny", fixture.TeamNow, StringComparison.Ordinal);
        Assert.Contains("Kai", fixture.TeamNow, StringComparison.Ordinal);
    }
}

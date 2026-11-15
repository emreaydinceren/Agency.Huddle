using System.Text.Json.Nodes;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins that a retro change can never set the Needs You level or its spend cap (ADR-0087): <c>propose_retro_changes</c> refuses such a proposal and
/// logs it, and an Accept refuses one that is already stored, even one that names no key but would change a value.
/// </summary>
public sealed class RetroNeedsYouLevelTests
{
    private const string Refusal = "Only the Human sets the Needs You level and its spend cap; propose_retro_changes cannot change them.";

    private static JsonObject Arguments(string file, string find, string replace) => new()
    {
        ["team"] = "Platform",
        ["proposals"] = new JsonArray
        {
            new JsonObject { ["title"] = "Level", ["targetFile"] = file, ["find"] = find, ["replace"] = replace },
        },
    };

    /// <summary>A proposal whose find or replace text names either key of <c>_team.md</c> is refused with the settled text, stores nothing and is logged.</summary>
    /// <param name="find">The text to find.</param>
    /// <param name="replace">The replacement.</param>
    [Theory]
    [InlineData("Team notes: keep it small.", "needsYouLevel: 'run-inside-team'")]
    [InlineData("Team notes: keep it small.", "SPENDCAP: '999'")]
    [InlineData("needsYouLevel: 'ask-me'", "x")]
    [InlineData("teamType: 'web@1.2.0'", "teamType: 'web@1.2.0'\nspendCap: '1'")]
    public async Task Tool_ProposalNamingAKey_IsRefusedAndLogged(string find, string replace)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        string result = await fixture.Tool("Nova").InvokeAsync(Arguments("_team.md", find, replace), ct);

        Assert.Equal(Refusal, result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
        Assert.Equal(
            $"Refused a retro proposal from caller {fixture.Ids["Nova"]} for Team Platform: it changes the Needs You level or spend cap of _team.md.",
            Assert.Single(fixture.ToolLog.AtExactly(LogLevel.Warning)).Message);
    }

    /// <summary>A proposal for another file, or for <c>_team.md</c> without either key, is stored as before.</summary>
    /// <param name="file">The target file.</param>
    /// <param name="find">The text to find.</param>
    /// <param name="replace">The replacement.</param>
    [Theory]
    [InlineData("_team.md", "keep it small", "keep it tiny")]
    [InlineData("AGENTS.md", "One PR per Task.", "One PR per Task, squash merged.")]
    public async Task Tool_ProposalNotNamingAKey_IsStored(string file, string find, string replace)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        string result = await fixture.Tool("Nova").InvokeAsync(Arguments(file, find, replace), ct);

        Assert.Equal("Proposal sent to the Human.", result);
        _ = Assert.Single(fixture.Proposals.Pending("Platform"));
        Assert.Empty(fixture.ToolLog.AtExactly(LogLevel.Warning));
    }

    /// <summary>A stored change that names a key is refused at Accept: nothing is written, the register is untouched and the refusal is logged.</summary>
    [Fact]
    public async Task AcceptAsync_ChangeNamingAKey_IsRefusedAndLogged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "_team.md", "keep it small", "keep it small\nneedsYouLevel: 'run-inside-team'"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal(Refusal, outcome.Text);
        Assert.Equal(RetroProposalFixture.TeamText, fixture.TeamNow);
        Assert.Equal(
            "Refused to apply retro change 1 of Platform to _team.md: it changes the Needs You level or spend cap.",
            Assert.Single(fixture.ServiceLog.AtExactly(LogLevel.Warning)).Message);
    }

    /// <summary>A change that never names a key but replaces only a value so the level would change is refused at Accept too, and the file is left as it was.</summary>
    [Fact]
    public async Task AcceptAsync_ChangeThatWouldChangeTheLevelWithoutNamingIt_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        string withLevel = "---\nteamType: 'web@1.2.0'\nneedsYouLevel: 'ask-me'\n---\nTeam notes: keep it small.\n";
        File.WriteAllText(Path.Combine(fixture.TeamFolder, TeamPaths.TeamFileName), withLevel);
        Assert.True(TeamFile.TryParse(withLevel, out TeamFile? current));
        fixture.Folders.File = current;
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "_team.md", "'ask-me'", "'run-inside-team'"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal(Refusal, outcome.Text);
        Assert.Equal(withLevel, fixture.TeamNow);
        Assert.Single(fixture.ServiceLog.AtExactly(LogLevel.Warning));
    }
}

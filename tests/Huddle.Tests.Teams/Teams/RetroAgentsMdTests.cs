using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins SB-R08's allowed write (Spec §6.2): an accepted retro change replaces exact text in the Team's <c>AGENTS.md</c>, the
/// name <c>Process.md</c> is retired as a target, and <c>_team.md</c> still applies.
/// </summary>
public sealed class RetroAgentsMdTests
{
    /// <summary>Builds the tool arguments for one change.</summary>
    /// <param name="file">The target file.</param>
    private static JsonObject Arguments(string file) => new()
    {
        ["team"] = "Platform",
        ["proposals"] = new JsonArray
        {
            new JsonObject { ["title"] = "Squash", ["targetFile"] = file, ["find"] = "One PR per Task.", ["replace"] = "One PR per Task, squash merged." },
        },
    };

    /// <summary>A change naming <c>AGENTS.md</c> with matching old text is written to the Team's AGENTS.md on accept.</summary>
    [Fact]
    public async Task AcceptAsync_AgentsMdTarget_AppliesToAgentsMd()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "One PR per Task.", "One PR per Task, squash merged."));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        // The fixture's AGENTS.md is "# Process\n\nReview before done.\nOne PR per Task.\n"; the one match of "One PR per Task." gains ", squash merged".
        Assert.Equal(RetroChangeOutcomeKind.Applied, outcome.Kind);
        Assert.Equal("# Process\n\nReview before done.\nOne PR per Task, squash merged.\n", fixture.ProcessNow);
        Assert.Equal("Applied 'Title 1' to AGENTS.md. @Nova", outcome.Text);
    }

    /// <summary>Old text that is not in AGENTS.md is refused and the file is byte-identical.</summary>
    [Fact]
    public async Task AcceptAsync_AgentsMdOldTextMissing_IsRefusedAndFileUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        string path = Path.Combine(fixture.TeamFolder, TeamPaths.AgentsMdFileName);
        byte[] before = File.ReadAllBytes(path);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "AGENTS.md", "Nothing like this", "x"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        // "Nothing like this" occurs 0 times in the fixture text, so Apply returns the no-match refusal naming the file.
        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("The text to replace is not in AGENTS.md; nothing changed.", outcome.Text);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>A change naming <c>_team.md</c> still applies to the Team file.</summary>
    [Fact]
    public async Task AcceptAsync_TeamFileTarget_StillApplies()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "_team.md", "keep it small", "keep it tiny"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        // The rewrite also drops the retired "process:" line (see RetroProposalTests.AcceptAsync_TeamFile_IsEdited); AGENTS.md is untouched.
        Assert.Equal(RetroChangeOutcomeKind.Applied, outcome.Kind);
        Assert.Equal("---\nteamType: 'web@1.2.0'\n---\nTeam notes: keep it tiny.\n", fixture.TeamNow);
        Assert.Equal(RetroProposalFixture.ProcessText, fixture.ProcessNow);
    }

    /// <summary>The retired target name <c>Process.md</c> is refused at accept and no file changes.</summary>
    [Fact]
    public async Task AcceptAsync_ProcessMdTarget_IsRefusedAsRetired()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "One PR per Task.", "x"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        // Process.md is neither AGENTS.md, _team.md nor a member's file, so nothing can read it and Apply refuses.
        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("The change could not be applied to Process.md.", outcome.Text);
        Assert.Equal(RetroProposalFixture.ProcessText, fixture.ProcessNow);
        Assert.Equal(RetroProposalFixture.TeamText, fixture.TeamNow);
    }

    /// <summary>The tool accepts <c>AGENTS.md</c> as a target and stores the set.</summary>
    [Fact]
    public async Task Tool_AgentsMdTarget_IsAccepted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("AGENTS.md"), ct);

        // IsAllowed must accept the name; the stored change keeps the target verbatim.
        Assert.Equal("Proposal sent to the Human.", result);
        RetroProposalSet set = Assert.Single(fixture.Proposals.Pending("Platform"));
        Assert.Equal(["AGENTS.md"], set.Changes.Select(static each => each.TargetFile));
    }

    /// <summary>The tool refuses the retired <c>Process.md</c> target and stores nothing.</summary>
    [Fact]
    public async Task Tool_ProcessMdTarget_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("Process.md"), ct);

        // Process.md is not in the allow-list any more, so the proposal is not stored.
        Assert.Equal("targetFile must be AGENTS.md, _team.md or the file of an Agent (Name.md).", result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }
}

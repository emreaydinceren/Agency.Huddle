using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Tests for the retro proposal service and tool (Task 76, RT-5): the diff is computed by Huddle from the current file, Accept
/// applies the replacement once and only once, a find that does not match exactly once is refused, a path outside the allow-list is
/// refused at the tool, Reject writes nothing and four proposals are refused.
/// </summary>
public sealed class RetroProposalTests
{
    private const string DiffOfSquash =
        "--- Process.md\n+++ Process.md\n@@\n Review before done.\n-One PR per Task.\n+One PR per Task, squash merged.";

    /// <summary>Builds the tool arguments.</summary>
    /// <param name="team">The Team name.</param>
    /// <param name="changes">The proposals.</param>
    private static JsonObject Arguments(string team, params (string Title, string File, string Find, string Replace)[] changes)
    {
        JsonArray array = [];
        foreach ((string title, string file, string find, string replace) in changes)
        {
            array.Add(new JsonObject { ["title"] = title, ["targetFile"] = file, ["find"] = find, ["replace"] = replace });
        }

        return new JsonObject { ["team"] = team, ["proposals"] = array };
    }

    /// <summary>One valid proposal against <paramref name="file"/>.</summary>
    /// <param name="file">The target file.</param>
    private static (string Title, string File, string Find, string Replace) Edit(string file = "Process.md") =>
        ("Squash", file, "One PR per Task.", "One PR per Task, squash merged.");

    /// <summary>The diff is computed from the file's current text and the change's find and replace.</summary>
    [Fact]
    public async Task Preview_ComputesDiffFromCurrentText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "One PR per Task.", "One PR per Task, squash merged."));

        string diff = fixture.Service.Preview(set, set.Changes[0]);

        Assert.Equal(DiffOfSquash, diff);
    }

    /// <summary>A find that no longer matches shows the no-match note instead of a diff.</summary>
    [Fact]
    public async Task Preview_FindNotInFile_ShowsNoMatchNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "Nothing like this", "x"));

        string diff = fixture.Service.Preview(set, set.Changes[0]);

        Assert.Equal("The text to replace is not in the file exactly once.", diff);
    }

    /// <summary>Accept applies the replacement, records one Done row in the register, and a second Accept does nothing.</summary>
    [Fact]
    public async Task AcceptAsync_AppliesOnceAndRecordsDoneRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "One PR per Task.", "One PR per Task, squash merged."));

        RetroChangeOutcome first = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);
        RetroChangeOutcome second = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Applied, first.Kind);
        Assert.Equal("Applied 'Title 1' to Process.md. @Nova", first.Text);
        Assert.Equal(RetroChangeOutcomeKind.Gone, second.Kind);
        Assert.Equal("# Process\n\nReview before done.\nOne PR per Task, squash merged.\n", fixture.ProcessNow);
        IReadOnlyList<RegisterRow> rows = fixture.Retro.ReadRegister("Platform");
        RegisterRow row = Assert.Single(rows);
        Assert.Equal(
            new RegisterRow("R-001", "Title 1", "Edited Process.md", "Nova", RetroStatus.Done, "Accepted 2026-10-08; verify at the next retro."),
            row);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>Two Accepts racing for the same change apply it exactly once.</summary>
    [Fact]
    public async Task AcceptAsync_Concurrent_AppliesExactlyOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "Review before done.", "Review twice."));

        RetroChangeOutcome[] outcomes = await Task.WhenAll(
            fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct),
            fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct));

        Assert.Equal(1, outcomes.Count(static each => each.Kind == RetroChangeOutcomeKind.Applied));
        Assert.Equal("# Process\n\nReview twice.\nOne PR per Task.\n", fixture.ProcessNow);
        Assert.Single(fixture.Retro.ReadRegister("Platform"));
    }

    /// <summary>A find that is not in the file is refused, nothing is written and the change stays pending.</summary>
    [Fact]
    public async Task AcceptAsync_FindMatchesZeroTimes_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "Not there", "x"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("The text to replace is not in Process.md; nothing changed.", outcome.Text);
        Assert.Equal(RetroProposalFixture.ProcessText, fixture.ProcessNow);
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
        Assert.Single(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A find that occurs twice is refused and nothing is written.</summary>
    [Fact]
    public async Task AcceptAsync_FindMatchesTwice_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "e", "E"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("The text to replace occurs more than once in Process.md; nothing changed.", outcome.Text);
        Assert.Equal(RetroProposalFixture.ProcessText, fixture.ProcessNow);
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
    }

    /// <summary>The Team file is a valid target.</summary>
    [Fact]
    public async Task AcceptAsync_TeamFile_IsEdited()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "_team.md", "keep it small", "keep it tiny"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Applied, outcome.Kind);
        Assert.Equal("---\nteamType: 'web@1.2.0'\nprocess: 'Process.md'\n---\nTeam notes: keep it tiny.\n", fixture.TeamNow);
    }

    /// <summary>A Team member's file is edited through the Persona store.</summary>
    [Fact]
    public async Task AcceptAsync_MemberFile_UpdatesPersona()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Kai.md", "You are Kai.", "You are Kai, and you review twice."));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Persona kai = fixture.Harness.Personas.Get("Kai") ?? throw new InvalidOperationException("Kai vanished.");
        Assert.Equal(RetroChangeOutcomeKind.Applied, outcome.Kind);
        Assert.Contains("You are Kai, and you review twice.", kai.Text, StringComparison.Ordinal); // contains-ok: persona body text
        Assert.Equal("Edited Kai.md", Assert.Single(fixture.Retro.ReadRegister("Platform")).Action);
    }

    /// <summary>When the Persona rules refuse the new text, the Persona is unchanged and the change stays pending.</summary>
    [Fact]
    public async Task AcceptAsync_PersonaRulesRefuse_LeavesPersonaUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        Persona before = fixture.Harness.Personas.Get("Kai") ?? throw new InvalidOperationException("Kai vanished.");
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Kai.md", before.Text, string.Empty));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Persona after = fixture.Harness.Personas.Get("Kai") ?? throw new InvalidOperationException("Kai vanished.");
        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("The change could not be applied to Kai.md.", outcome.Text);
        Assert.Equal(before.Text, after.Text);
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
        Assert.Single(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>An edit that leaves <c>_team.md</c> unparseable is refused before anything is written, and the change stays pending.</summary>
    [Fact]
    public async Task AcceptAsync_TeamFileEditNotParseable_IsRefusedAndLeavesFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "_team.md", "teamType: 'web@1.2.0'", "teamType: 'not a ref'"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("The edit would leave _team.md unreadable; nothing changed.", outcome.Text);
        Assert.Equal(RetroProposalFixture.TeamText, fixture.TeamNow);
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
        Assert.Single(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A Teammate-file edit that changes <c>name:</c> is refused: a retro change cannot rename a Teammate.</summary>
    [Fact]
    public async Task AcceptAsync_MemberFileRename_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        Persona before = fixture.Harness.Personas.Get("Kai") ?? throw new InvalidOperationException("Kai vanished.");
        string nameLine = before.Text.Split('\n').Select(static line => line.TrimEnd('\r')).First(static line => line.StartsWith("name:", StringComparison.Ordinal));
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Kai.md", nameLine, "name: Kay"));

        RetroChangeOutcome outcome = await fixture.Service.AcceptAsync(fixture.RoomId, set.Id, "1", ct);

        Persona after = fixture.Harness.Personas.Get("Kai") ?? throw new InvalidOperationException("Kai vanished.");
        Assert.Equal(RetroChangeOutcomeKind.Refused, outcome.Kind);
        Assert.Equal("A retro change cannot rename a Teammate; Kai.md was not changed.", outcome.Text);
        Assert.Equal(before.Text, after.Text);
        Assert.Null(fixture.Harness.Personas.Get("Kay"));
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
    }

    /// <summary>A stored proposal file whose id differs from its file name, or whose Team differs from its folder, is not loaded.</summary>
    [Fact]
    public async Task Pending_FileWithWrongIdOrTeam_IsIgnored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        string directory = Path.Combine(fixture.TeamFolder, "_proposals", "retro");
        _ = Directory.CreateDirectory(directory);
        RetroChange[] changes = [RetroProposalFixture.Change("1", "Process.md", "Review before done.", "Review twice.")];
        File.WriteAllText(Path.Combine(directory, "good.json"), System.Text.Json.JsonSerializer.Serialize(new RetroProposalSet("good", "Platform", "Nova", changes)));
        File.WriteAllText(Path.Combine(directory, "renamed.json"), System.Text.Json.JsonSerializer.Serialize(new RetroProposalSet("other-id", "Platform", "Nova", changes)));
        File.WriteAllText(Path.Combine(directory, "foreign.json"), System.Text.Json.JsonSerializer.Serialize(new RetroProposalSet("foreign", "Other", "Nova", changes)));
        RetroProposalStore store = new(fixture.Folders);

        IReadOnlyList<RetroProposalSet> pending = store.Pending("Platform");

        Assert.Equal(["good"], pending.Select(static each => each.Id));
        Assert.Null(store.Get("other-id"));
        Assert.Null(store.Get("foreign"));
    }

    /// <summary>Reject writes nothing to the files or the register and removes the change.</summary>
    [Fact]
    public async Task RejectAsync_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "One PR per Task.", "x"));

        RetroChangeOutcome outcome = await fixture.Service.RejectAsync(fixture.RoomId, set.Id, "1", "Too strict.", ct);

        Assert.Equal(RetroChangeOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal("Rejected 'Title 1': Too strict. @Nova", outcome.Text);
        Assert.Equal(RetroProposalFixture.ProcessText, fixture.ProcessNow);
        Assert.Empty(fixture.Retro.ReadRegister("Platform"));
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A reject without a reason says so.</summary>
    [Fact]
    public async Task RejectAsync_NoReason_SaysNoReasonGiven()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "One PR per Task.", "x"));

        RetroChangeOutcome outcome = await fixture.Service.RejectAsync(fixture.RoomId, set.Id, "1", "  ", ct);

        Assert.Equal("Rejected 'Title 1': no reason given. @Nova", outcome.Text);
    }

    /// <summary>Only rooms the proposer is in see the set.</summary>
    [Fact]
    public async Task PendingAsync_OnlyInProposersRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        RetroProposalSet set = fixture.Propose(RetroProposalFixture.Change("1", "Process.md", "a", "b"));

        IReadOnlyList<RetroProposalSet> here = await fixture.Service.PendingAsync(fixture.RoomId, ct);
        IReadOnlyList<RetroProposalSet> there = await fixture.Service.PendingAsync(fixture.OtherRoomId, ct);

        Assert.Equal([set.Id], here.Select(static each => each.Id));
        Assert.Empty(there);
    }

    /// <summary>The tool stores a set from the arguments and tells the agent it was sent.</summary>
    [Fact]
    public async Task Tool_StoresSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("Platform", Edit(), Edit("_team.md"), Edit("Kai.md")), ct);

        Assert.Equal("Proposal sent to the Human.", result);
        RetroProposalSet set = Assert.Single(fixture.Proposals.Pending("Platform"));
        Assert.Equal("Nova", set.Proposer);
        Assert.Equal(["Process.md", "_team.md", "Kai.md"], set.Changes.Select(static each => each.TargetFile));
        Assert.Equal(["1", "2", "3"], set.Changes.Select(static each => each.Id));
    }

    /// <summary>The Team's Retro agent may propose, as the manager does.</summary>
    [Fact]
    public async Task Tool_RetroAgent_StoresSet()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        string result = await fixture.Tool("Rae").InvokeAsync(Arguments("Platform", Edit()), ct);

        Assert.Equal("Proposal sent to the Human.", result);
        RetroProposalSet set = Assert.Single(fixture.Proposals.Pending("Platform"));
        Assert.Equal("Rae", set.Proposer);
    }

    /// <summary>A member holding the Skill who is neither the manager nor the Retro agent is refused and nothing is stored.</summary>
    [Fact]
    public async Task Tool_MemberWithSkillButNoRole_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        string result = await fixture.Tool("Sam").InvokeAsync(Arguments("Platform", Edit()), ct);

        Assert.Equal("Only the Team's manager or its Retro agent may propose retro changes.", result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A member without the retrospective Skill is refused and nothing is stored.</summary>
    [Fact]
    public async Task Tool_MemberWithoutSkill_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        string result = await fixture.Tool("Kai").InvokeAsync(Arguments("Platform", Edit()), ct);

        Assert.Equal("You do not hold the retrospective Skill.", result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A Retro agent of another Team is refused as a non-member and nothing is stored.</summary>
    [Fact]
    public async Task Tool_NonMember_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);

        string result = await fixture.Tool("Ola").InvokeAsync(Arguments("Platform", Edit()), ct);

        Assert.Equal("You are not a member of Team Platform.", result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>Four proposals are refused and nothing is stored.</summary>
    [Fact]
    public async Task Tool_FourProposals_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("Platform", Edit(), Edit(), Edit(), Edit()), ct);

        Assert.Equal("Propose at most 3 changes.", result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>No proposals at all is refused.</summary>
    [Fact]
    public async Task Tool_NoProposals_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("Platform"), ct);

        Assert.Equal("Propose at least one change.", result);
    }

    /// <summary>A path outside the allow-list is refused at the tool, whatever its spelling.</summary>
    [Theory]
    [InlineData("../Process.md")]
    [InlineData("Other.md")]
    [InlineData("logs/tool-calls/x.md")]
    [InlineData("Teams/Platform/Process.md")]
    [InlineData("process.md")]
    public async Task Tool_PathOutsideAllowList_Refused(string file)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("Platform", Edit(), Edit(file)), ct);

        Assert.Equal("targetFile must be Process.md, _team.md or the file of a Team member (Name.md).", result);
        Assert.Empty(fixture.Proposals.Pending("Platform"));
    }

    /// <summary>A proposal missing its title or find text is refused.</summary>
    [Fact]
    public async Task Tool_MissingFields_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string result = await tool.InvokeAsync(Arguments("Platform", ("", "Process.md", "a", "b")), ct);

        Assert.Equal("Each proposal needs a title, a targetFile and a find text.", result);
    }

    /// <summary>A caller who is not a member of the named Team is refused, and an unknown Team is named.</summary>
    [Fact]
    public async Task Tool_NotAMemberOrNoTeam_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        string notMember = await tool.InvokeAsync(Arguments("Other", Edit()), ct);
        string noTeam = await tool.InvokeAsync(Arguments("Ghost", Edit()), ct);

        Assert.Equal("You are not a member of Team Other.", notMember);
        Assert.Equal("There is no Team named Ghost.", noTeam);
    }

    /// <summary>The tool's name and description are pinned.</summary>
    [Fact]
    public async Task Tool_NameAndDescription()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using RetroProposalFixture fixture = await RetroProposalFixture.CreateAsync(ct);
        ProposeRetroChangesTool tool = fixture.Tool("Nova");

        Assert.Equal("propose_retro_changes", tool.Name);
        Assert.Equal(
            "Proposes up to 3 changes to the Human after a retro. Each change names a targetFile (Process.md, _team.md or a Team member's Name.md), " +
            "the exact text to find (it must occur once in that file) and the text to replace it with. Huddle shows each as a diff; " +
            "nothing is edited until the Human accepts it, and then Huddle makes the edit, not you.",
            tool.Description);
    }
}

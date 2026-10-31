using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.TeamMemory;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>Pins the <c>propose_team_memory</c> tool's refusals and its one success (Team Memory Helper spec §6.1).</summary>
public sealed class ProposeTeamMemoryToolTests
{
    /// <summary>A Team the caller is not in is refused, naming the Teams it can propose to (U-7); nothing is queued.</summary>
    [Fact]
    public async Task NotAMember_IsRefusedWithTheTeamsItCanUse()
    {
        using TeamMemoryHarness harness = new();

        string reply = await harness.ProposeAsync("Leo", "Marketing", "Launch posts go out on Mondays.");

        Assert.Equal("You are not a member of Marketing. You can propose to: Research.", reply);
        harness.PassTheWait();
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Equal(0, harness.Process.Calls);
    }

    /// <summary>A Team that does not exist is refused the same way.</summary>
    [Fact]
    public async Task UnknownTeam_IsRefused()
    {
        using TeamMemoryHarness harness = new();

        Assert.Equal("You are not a member of Nowhere. You can propose to: Research.", await harness.ProposeAsync("Leo", "Nowhere", "A fact."));
    }

    /// <summary>The manager and the Chief of Staff may propose; the Chief of Staff to any Team (U-9).</summary>
    /// <param name="persona">The caller.</param>
    /// <param name="team">The Team.</param>
    [Theory]
    [InlineData("Ada", "Research")]
    [InlineData("Chief", "Marketing")]
    public async Task ManagerAndChiefOfStaff_MayPropose(string persona, string team)
    {
        using TeamMemoryHarness harness = new();

        Assert.Equal($"Proposed to {team}'s Team Memory.", await harness.ProposeAsync(persona, team, "A fact the Team keeps."));
    }

    /// <summary>A blank fact is refused.</summary>
    [Fact]
    public async Task BlankFact_IsRefused()
    {
        using TeamMemoryHarness harness = new();

        Assert.Equal("Write the fact to propose: one to a few sentences.", await harness.ProposeAsync("Leo", "Research", "   "));
    }

    /// <summary>A fact or a why over its cap is refused with its length and the cap.</summary>
    /// <param name="factLength">The fact's length.</param>
    /// <param name="whyLength">The why's length.</param>
    /// <param name="expected">The refusal.</param>
    [Theory]
    [InlineData(1001, 10, "The fact is 1001 characters; the most is 1000. Propose one fact at a time.")]
    [InlineData(10, 301, "The why is 301 characters; the most is 300. Propose one fact at a time.")]
    public async Task OverTheCap_IsRefused(int factLength, int whyLength, string expected)
    {
        using TeamMemoryHarness harness = new();

        Assert.Equal(expected, await harness.ProposeAsync("Leo", "Research", new string('f', factLength), new string('w', whyLength)));
    }

    /// <summary>A Helper the Human turned off, or Adapters switched off, refuses proposals and says what to do instead.</summary>
    /// <param name="adaptersOff">Whether to switch Adapters off rather than the Helper.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HelperOff_IsRefused(bool adaptersOff)
    {
        using TeamMemoryHarness harness = new(team => team.Acp.Enabled = !adaptersOff);
        if (!adaptersOff)
        {
            harness.SettingsStore.Save(TeamMemoryHelper.HelperId, new HelperSettingsRow(false, null, null, null, 30));
        }

        Assert.Equal(
            "Team Memory proposals are switched off. Note the fact in your own Memory, or tell the manager.",
            await harness.ProposeAsync("Leo", "Research", "A fact."));
    }

    /// <summary>A Helper paused at its daily cap refuses proposals (U-8).</summary>
    [Fact]
    public async Task HelperPaused_IsRefused()
    {
        using TeamMemoryHarness harness = new();
        harness.SettingsStore.Save(TeamMemoryHelper.HelperId, new HelperSettingsRow(true, null, null, null, 1));
        Assert.True(harness.Budget.TryReserve(TeamMemoryHelper.HelperId, 1));

        Assert.Equal(
            "The Team Memory Helper is paused for today. Note the fact in your own Memory, or tell the manager.",
            await harness.ProposeAsync("Leo", "Research", "A fact."));
    }

    /// <summary>A valid call queues exactly one judgement, of the trimmed fact and why, by the caller, under the catalog's spelling of the Team.</summary>
    [Fact]
    public async Task ValidCall_QueuesExactlyOneJudgement()
    {
        using TeamMemoryHarness harness = new();
        harness.Process.Text = """{"action": "none", "reason": "already recorded"}""";

        string reply = await harness.ProposeAsync("Miriam", "RESEARCH", "  Consent can be verbal.  ", "  Room 'Interview plan'  ");
        harness.PassTheWait();
        await harness.RecordedAsync();
        await Task.Delay(150, TestContext.Current.CancellationToken);

        Assert.Equal("Proposed to Research's Team Memory.", reply);
        Assert.Equal(1, harness.Process.Calls);
        string prompt = harness.Process.Requests[0].Prompt;
        Assert.StartsWith("Team Research. Folders you may write: Research, Research/Atlas\n", prompt, StringComparison.Ordinal); // contains-ok: model prompt text; its first line is the assertion
        Assert.EndsWith("Proposal from Miriam:\n<fact>\nConsent can be verbal.\n</fact>\n<why>\nRoom 'Interview plan'\n</why>", prompt, StringComparison.Ordinal); // contains-ok: model prompt text; its closing proposal part is the assertion
    }

    /// <summary>The tool's name is recognised bare and behind an Adapter's prefix.</summary>
    [Fact]
    public void IsToolName_MatchesBareAndPrefixed()
    {
        Assert.True(App.Acp.Tools.ProposeTeamMemoryTool.IsToolName("propose_team_memory"));
        Assert.True(App.Acp.Tools.ProposeTeamMemoryTool.IsToolName("mcp__team__propose_team_memory"));
        Assert.False(App.Acp.Tools.ProposeTeamMemoryTool.IsToolName("propose_team_memory_x"));
    }
}

using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins <see cref="GoldfishDoneTool"/> (goldfish spec 5.3, corrections-slice3 step 2): it refuses unless the caller's (Agent, Room)
/// is possessed, and otherwise ends the possession with the result, the findings and the outcome.
/// </summary>
public sealed class GoldfishDoneToolTests
{
    private const string NotPossessed = "You are not in goldfish mode, so there is nothing to finish here. Do not call goldfish_done again.";

    private static JsonObject Arguments(string result, string[]? findings = null, string? outcome = null)
    {
        JsonObject arguments = new() { ["result"] = result };
        if (findings is not null)
        {
            arguments["findings"] = new JsonArray([.. findings.Select(static finding => (JsonNode?)finding)]);
        }

        if (outcome is not null)
        {
            arguments["outcome"] = outcome;
        }

        return arguments;
    }

    /// <summary>The tool is named <c>goldfish_done</c>; the schema requires <c>result</c> and carries the 4000, 30 and 500 limits and the two outcomes.</summary>
    [Fact]
    public async Task NameAndSchema_CarryTheLimitsAndOutcomes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        GoldfishDoneTool tool = rig.DoneTool();

        Assert.Equal("goldfish_done", tool.Name);
        Assert.Equal(["result"], Assert.IsType<JsonArray>(tool.InputSchema["required"]).Select(static node => node?.GetValue<string>()));
        JsonObject properties = Assert.IsType<JsonObject>(tool.InputSchema["properties"]);
        Assert.Equal(4000, properties["result"]?["maxLength"]?.GetValue<int>());
        Assert.Equal(30, properties["findings"]?["maxItems"]?.GetValue<int>());
        Assert.Equal(500, properties["findings"]?["items"]?["maxLength"]?.GetValue<int>());
        Assert.Equal(["finished", "stuck"], Assert.IsType<JsonArray>(properties["outcome"]?["enum"]).Select(static node => node?.GetValue<string>()));
    }

    /// <summary>The description says what <c>result</c> holds, that findings are listed apart, that <c>stuck</c> is for when it cannot go on, and that the Turn ends.</summary>
    [Fact]
    public async Task Description_IsThePrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);

        Assert.Equal(
            "Leave goldfish mode and hand your result back. In result, say what you did, the questions you asked and the answers you got, and the path of any file you wrote; "
            + "put each finding in findings. Use the outcome stuck only when you cannot go on, even with assumptions. Your Turn ends after this call.",
            rig.DoneTool().Description);
    }

    /// <summary>Called by an Agent with no possession, the tool refuses with its final text and ends nothing.</summary>
    [Fact]
    public async Task InvokeAsync_NotPossessed_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();

        string result = await rig.DoneTool().InvokeAsync(Arguments("All done."), ct);

        Assert.Equal(NotPossessed, result);
        Assert.Empty(rig.Link.Ends);
        Assert.Empty(await rig.HuddleLinesAsync(ct));
    }

    /// <summary>A Starting possession is the warm calling Turn's, not the goldfish's: the tool refuses and the row stays open.</summary>
    [Fact]
    public async Task InvokeAsync_OnlyStarting_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession(PossessionState.Starting);
        rig.NovaTurnInRoom();

        string result = await rig.DoneTool().InvokeAsync(Arguments("All done."), ct);

        Assert.Equal(NotPossessed, result);
        Assert.Equal(PossessionState.Starting, rig.Supervisor.FindOpen(rig.NovaId)?.State);
    }

    /// <summary>A possession in Pricing does not let a Turn of the same Agent in another Room end it.</summary>
    [Fact]
    public async Task InvokeAsync_PossessedInAnotherRoom_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        Room other = await rig.Harness.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, rig.NovaId], ct);
        rig.Activity.Begin(rig.NovaId, other.Id);

        string result = await rig.DoneTool().InvokeAsync(Arguments("All done."), ct);

        Assert.Equal(NotPossessed, result);
        Assert.Empty(rig.Link.Ends);
    }

    /// <summary>A finished possession ends Finished: the result Message has the brief on top, the text and the findings as a list, and the tool says the body goes back.</summary>
    [Fact]
    public async Task InvokeAsync_Possessed_PostsTheResultWithBriefAndFindings()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        rig.NovaTurnInRoom();

        string result = await rig.DoneTool().InvokeAsync(Arguments("The total is 42.", ["Rounding drifts", "Tax is missing"]), ct);

        Assert.Equal("Done. Your body goes back to Nova.", result);
        ChatMessage posted = Assert.Single(await rig.HuddleLinesAsync(ct), static message => message.Text.StartsWith("Brief: ", StringComparison.Ordinal));
        Assert.Equal("Brief: Look into it\n\nThe total is 42.\n\n- Rounding drifts\n- Tax is missing", posted.Text);
        Assert.Equal(("p1", GoldfishExit.Finished), (rig.Link.Ends.Single().PossessionId, rig.Link.Ends.Single().Exit));
        Assert.Equal(PossessionState.Ended, rig.Store.Get("p1")?.State);
    }

    /// <summary>The outcome <c>stuck</c> sets the exit Stuck and the result Message is posted as a partial result.</summary>
    [Fact]
    public async Task InvokeAsync_Stuck_EndsStuckWithAPartialResult()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        rig.NovaTurnInRoom();

        _ = await rig.DoneTool().InvokeAsync(Arguments("Got halfway.", outcome: "stuck"), ct);

        Assert.Equal(GoldfishExit.Stuck, rig.Link.Ends.Single().Exit);
        ChatMessage posted = Assert.Single(await rig.HuddleLinesAsync(ct), static message => message.Text.StartsWith("Brief: ", StringComparison.Ordinal));
        Assert.Equal("Brief: Look into it\n\nPartial result:\nGot halfway.", posted.Text);
    }
}

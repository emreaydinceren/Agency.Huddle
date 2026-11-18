using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins <see cref="GoldfishTool"/> (goldfish spec 5.1, corrections-slice3 step 2): the refusals in their order, the success
/// text, the possession row, the start row, the Begin sent to the runner and the comment on the Task in hand.
/// </summary>
public sealed class GoldfishToolTests
{
    private const string Brief = "  Read src/a.cs and check @Kai's math\n  twice  ";

    private const string Description =
        "Go into goldfish mode: a fresh copy of you, with none of this Room's history and none of your Memory, does one piece of work while you step aside. "
        + "The brief is the only thing the goldfish will know, and it is posted in the Room word for word. "
        + "Say what to do and what to read; do not say what you think the answer is, what the author meant or what was agreed in chat. "
        + "The goldfish leaves with goldfish_done, and anyone can call it back by writing your name and \"comeback\" in the Room. "
        + "This call ends your Turn: end it with one short line and the result arrives as a new Turn.";

    private static JsonObject Arguments(string? instructions) =>
        instructions is null ? [] : new JsonObject { ["instructions"] = instructions };

    /// <summary>The tool is named <c>goldfish</c>, requires <c>instructions</c> capped at 1000 characters and takes no Room.</summary>
    [Fact]
    public async Task NameAndSchema_RequireInstructionsAndTakeNoRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        GoldfishTool tool = rig.Tool();

        Assert.Equal("goldfish", tool.Name);
        JsonArray required = Assert.IsType<JsonArray>(tool.InputSchema["required"]);
        Assert.Equal(["instructions"], required.Select(static node => node?.GetValue<string>()));
        JsonObject properties = Assert.IsType<JsonObject>(tool.InputSchema["properties"]);
        Assert.Equal(["instructions"], properties.Select(static pair => pair.Key));
        Assert.Equal(1000, properties["instructions"]?["maxLength"]?.GetValue<int>());
    }

    /// <summary>The description is the Prompt, in the spec's order: what the mode is, the brief posted word for word, what to say, the way out, the Turn ends.</summary>
    [Fact]
    public async Task Description_IsThePromptInSpecOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);

        Assert.Equal(Description, rig.Tool().Description);
        Assert.DoesNotContain("mcp__team__", rig.Tool().Description, StringComparison.Ordinal);
    }

    /// <summary>A good call returns the started text naming the Room and the silent token, at once.</summary>
    [Fact]
    public async Task InvokeAsync_Accepted_ReturnsTheStartedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();

        string result = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Assert.Equal(
            "Goldfish started in Pricing. Your brief is posted there. End this Turn now with one short line, or NO_REPLY.\n"
            + "You will get the result as a new Turn when the goldfish is done. Until then you cannot post in Pricing.",
            result);
    }

    /// <summary>A good call writes one Starting possession for Nova in the calling Turn's Room, with the brief byte for byte.</summary>
    [Fact]
    public async Task InvokeAsync_Accepted_WritesAStartingRow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();

        _ = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Possession row = Assert.IsType<Possession>(rig.Supervisor.FindOpen(rig.NovaId));
        Assert.Equal((rig.NovaId, rig.Room.Id, null, Brief, PossessionState.Starting), (row.AgentId, row.RoomId, row.TaskId, row.Brief, row.State));
    }

    /// <summary>The start row is Huddle's Message with the brief word for word, and the brief's own Mention of Kai owes Kai no Turn.</summary>
    [Fact]
    public async Task InvokeAsync_BriefMentioningKai_PostsTheStartRowAndOwesNobody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.Queue.Subscribe(rig.KaiId);
        rig.NovaTurnInRoom();

        _ = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        ChatMessage line = Assert.Single(await rig.HuddleLinesAsync(ct));
        Assert.Equal("Nova went into goldfish mode. They don't remember this Room.\n\n" + Brief, line.Text);
        Assert.Empty(TurnQueueRows.Read(rig.Harness.Options.Value.DataDir));
    }

    /// <summary>The runner is sent <c>GoldfishBegin</c> once, with the possession id, the Room and the brief unchanged.</summary>
    [Fact]
    public async Task InvokeAsync_Accepted_SendsGoldfishBegin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();

        _ = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Possession row = Assert.IsType<Possession>(rig.Supervisor.FindOpen(rig.NovaId));
        (string agentId, GoldfishBegin begin) = Assert.Single(rig.Link.Begins);
        Assert.Equal(rig.NovaId, agentId);
        Assert.Equal((row.Id, rig.Room.Id, Brief, null), (begin.PossessionId, begin.RoomId, begin.Brief, begin.TaskId));
    }

    /// <summary>With a Task in hand the Task gets the brief as a comment from Huddle, and the Task id travels in the possession and in Begin.</summary>
    [Fact]
    public async Task InvokeAsync_TaskInHand_CommentsTheBriefAndCarriesTheTaskId()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        TaskItem task = rig.Harness.CreateTask("Fix login");
        rig.Activity.RecordWake(rig.NovaId, rig.Room.Id, task.Id);
        rig.NovaTurnInRoom();

        _ = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        TaskItem stored = rig.Harness.Store.Get(task.Id) ?? throw new InvalidOperationException("Task vanished.");
        TimelineEntry comment = Assert.Single(stored.Comments);
        Assert.Equal((TaskActors.Huddle.Name, ("Goldfish brief for Nova: " + Brief).Trim()), (comment.Author, comment.Text));
        Possession row = Assert.IsType<Possession>(rig.Supervisor.FindOpen(rig.NovaId));
        Assert.Equal(task.Id.ToString(), row.TaskId);
        Assert.Equal(task.Id.ToString(), Assert.Single(rig.Link.Begins).Begin.TaskId);
    }

    /// <summary>A Task that cannot take the comment is logged and skipped: the goldfish still starts.</summary>
    [Fact]
    public async Task InvokeAsync_CommentFails_StillStarts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        _ = TaskId.TryParse("PLAT-9999", out TaskId missing);
        rig.Activity.RecordWake(rig.NovaId, rig.Room.Id, missing);
        rig.NovaTurnInRoom();

        string result = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Assert.StartsWith("Goldfish started in Pricing.", result, StringComparison.Ordinal); // contains-ok: only the start of the started text matters here; the full text is pinned in InvokeAsync_Accepted_ReturnsTheStartedText
        Assert.Equal(PossessionState.Starting, rig.Supervisor.FindOpen(rig.NovaId)?.State);
    }

    /// <summary>An empty or blank brief is refused first, even with no Turn running, and nothing is started.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public async Task InvokeAsync_BlankBrief_IsRefusedBeforeAnythingElse(string? instructions)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);

        string result = await rig.Tool().InvokeAsync(Arguments(instructions), ct);

        Assert.Equal("Write the brief: what to do and what to read. Nothing was started.", result);
        Assert.Null(rig.Supervisor.FindOpen(rig.NovaId));
        Assert.Empty(rig.Link.Begins);
    }

    /// <summary>A brief of 1001 characters is refused with the count and the limit; one of exactly 1000 is accepted.</summary>
    [Fact]
    public async Task InvokeAsync_BriefOverTheLimit_IsRefusedAndTheLimitItselfIsFine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();

        string tooLong = await rig.Tool().InvokeAsync(Arguments(new string('x', 1001)), ct);
        Assert.Null(rig.Supervisor.FindOpen(rig.NovaId));
        string atLimit = await rig.Tool().InvokeAsync(Arguments(new string('x', 1000)), ct);

        Assert.Equal("The brief is 1001 characters; the limit is 1000. Say less: what to do and which files to read. Nothing was started.", tooLong);
        Assert.StartsWith("Goldfish started in Pricing.", atLimit, StringComparison.Ordinal); // contains-ok: only the start of the started text matters here; the full text is pinned in InvokeAsync_Accepted_ReturnsTheStartedText
    }

    /// <summary>With more than one Turn allowed at once the tool refuses, read live, before it looks for the Turn.</summary>
    [Fact]
    public async Task InvokeAsync_MaxConcurrentTurnsAboveOne_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        GoldfishTool tool = rig.Tool();
        rig.Harness.Options.Value.Acp.MaxConcurrentTurns = 2;

        string result = await tool.InvokeAsync(Arguments(Brief), ct);

        Assert.Equal("Goldfish mode needs one Turn at a time, and this Huddle runs up to 2 at once. It is not available here; do not retry.", result);
        Assert.Null(rig.Supervisor.FindOpen(rig.NovaId));
    }

    /// <summary>Called with no running Turn of the caller, the tool says to call it from the Turn where the work is.</summary>
    [Fact]
    public async Task InvokeAsync_NoTurn_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);

        string result = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Assert.Equal("Call goldfish from the Turn where the work is. Nothing was started.", result);
        Assert.Null(rig.Supervisor.FindOpen(rig.NovaId));
    }

    /// <summary>A second call in the same Turn is refused as already in goldfish mode, naming the Room, and leaves the first row alone.</summary>
    [Fact]
    public async Task InvokeAsync_SecondCallInTheSameTurn_IsRefusedNamingTheRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.NovaTurnInRoom();
        _ = await rig.Tool().InvokeAsync(Arguments(Brief), ct);
        string firstId = Assert.IsType<Possession>(rig.Supervisor.FindOpen(rig.NovaId)).Id;

        string second = await rig.Tool().InvokeAsync(Arguments("Another brief"), ct);

        Assert.Equal("You are already in goldfish mode in Pricing. One at a time. Nothing was started.", second);
        Assert.Equal(firstId, rig.Supervisor.FindOpen(rig.NovaId)?.Id);
        Assert.Single(rig.Link.Begins);
    }

    /// <summary>Called from another Room while a possession is open in Pricing, the refusal names Pricing, the Room of the open one.</summary>
    [Fact]
    public async Task InvokeAsync_OpenInAnotherRoom_NamesThatRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        Room other = await rig.Harness.Directory.CreateRoomAsync("Elsewhere", [KnownIds.Human, rig.NovaId], ct);
        rig.Activity.Begin(rig.NovaId, other.Id);

        string result = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Assert.Equal("You are already in goldfish mode in Pricing. One at a time. Nothing was started.", result);
    }

    /// <summary>Called by the goldfish itself, in the Room that is possessed, the tool refuses with the no-nesting text.</summary>
    [Fact]
    public async Task InvokeAsync_CallerIsPossessed_IsRefusedAsNested()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using GoldfishToolRig rig = await GoldfishToolRig.CreateAsync(ct);
        rig.OpenPossession();
        rig.NovaTurnInRoom();

        string result = await rig.Tool().InvokeAsync(Arguments(Brief), ct);

        Assert.Equal("You are in goldfish mode. Finish with goldfish_done first. Nothing was started.", result);
        Assert.Empty(rig.Link.Begins);
    }
}

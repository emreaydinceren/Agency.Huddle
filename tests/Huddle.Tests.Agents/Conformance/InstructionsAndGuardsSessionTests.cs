using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Pins the host wiring of Instructions and the file guards through a real session against the scripted ACP peer: that
/// <c>DotAcpPersonaHost</c> reads the three <c>Instructions.md</c> files into the appended system prompt, and that the permission
/// handler it gives the session refuses a member's edit of Team Memory and an Agent's edit of an Instructions file while allowing reads.
/// </summary>
public sealed class InstructionsAndGuardsSessionTests
{
    private static readonly string[] BothOptions = ["allow", "reject"];

    /// <summary>Starts a session for <c>nova</c>, a member (or the manager) of Team Business, over a data folder the test has already filled.</summary>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="manager">The Team's manager as written in <c>_team.md</c>.</param>
    /// <param name="ct">Cancels the start.</param>
    private static async Task<MockAdapterFixture> StartAsync(TempDataDir dataDir, string manager, CancellationToken ct)
    {
        string business = Path.Combine(dataDir.Path, "Teams", "Business");
        Directory.CreateDirectory(business);
        File.WriteAllText(Path.Combine(business, "_team.md"), $"---\nteamType: 'development@1.0.0'\nmanager: '{manager}'\n---\n");

        string text = "---\nName: Nova\nTitle: Nova\nAlias: Nova\nTeams: [Business]\n---\nYou are Nova.";
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:DataDir"] = dataDir.Path };

        return await MockAdapterFixture.StartAsync(new Persona("nova", text), config, cancellationToken: ct);
    }

    /// <summary>Asks the host's permission handler about one tool call, through the wire, and returns the option it chose.</summary>
    /// <param name="fixture">The started fixture.</param>
    /// <param name="kind">The ACP tool kind.</param>
    /// <param name="path">The path argument.</param>
    /// <param name="ct">Cancels the wait.</param>
    private static async Task<string?> DecideAsync(MockAdapterFixture fixture, string kind, string path, CancellationToken ct)
    {
        _ = await fixture.SessionNewAsync(ct);

        JsonArray options = [];
        foreach (string id in BothOptions)
        {
            options.Add(new JsonObject { ["optionId"] = id, ["name"] = id, ["kind"] = id == "allow" ? "allow_once" : "reject_once" });
        }

        JsonObject parameters = new()
        {
            ["sessionId"] = "sess-1",
            ["toolCall"] = new JsonObject
            {
                ["toolCallId"] = "call-1",
                ["title"] = "Tool",
                ["kind"] = kind,
                ["status"] = "pending",
                ["rawInput"] = new JsonObject { ["file_path"] = path },
            },
            ["options"] = options,
        };

        JsonObject result = await fixture.Agent.SendRequestAsync("session/request_permission", parameters);
        return (string?)(result["outcome"] as JsonObject)?["optionId"];
    }

    /// <summary>The three levels of Instructions reach the system prompt in order, general to specific, under one heading.</summary>
    [Fact]
    public async Task SessionStart_ReadsTheThreeInstructionsFilesInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Teams", "Business"));
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Agents", "nova", "work"));
        File.WriteAllText(Path.Combine(dataDir.Path, "Teams", "Instructions.md"), "Huddle rule.");
        File.WriteAllText(Path.Combine(dataDir.Path, "Teams", "Business", "Instructions.md"), "Business rule.");
        File.WriteAllText(Path.Combine(dataDir.Path, "Agents", "nova", "work", "Instructions.md"), "Own rule.");

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "boss", ct);
        string prompt = (await fixture.AppendedSystemPromptAsync(ct)).ReplaceLineEndings("\n");

        int start = prompt.IndexOf("## Instructions", StringComparison.Ordinal);
        int end = prompt.IndexOf("Own rule.", StringComparison.Ordinal) + "Own rule.".Length;
        Assert.True(start >= 0 && end > start, "The prompt has no Instructions part.");
        Assert.Equal(
            "## Instructions\nThe Human wrote these standing instructions for you. Follow them in every Room. Where they\n"
            + "conflict, the more specific level wins: your own over your Team's, your Team's over Huddle's.\n"
            + "### Huddle\nHuddle rule.\n\n### Team Business\nBusiness rule.\n\n### You\nOwn rule.",
            prompt[start..end]);
        Assert.True(prompt.IndexOf("You are Nova.", StringComparison.Ordinal) < start, "Instructions follow the Persona text.");
    }

    /// <summary>With no Instructions files the prompt has no Instructions part.</summary>
    [Fact]
    public async Task SessionStart_WithNoInstructionsFiles_HasNoInstructionsPart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "boss", ct);
        string prompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.DoesNotContain("## Instructions", prompt, StringComparison.Ordinal);
    }

    /// <summary>A member's edit of Team Memory is rejected by the session's permission handler, and its read of the same file is allowed.</summary>
    [Fact]
    public async Task Permission_MemberEditOfTeamMemory_IsRejected_AndReadAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string file = Path.Combine(dataDir.Path, "Teams", "Business", "memory", "x.md");

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "boss", ct);
        string? edit = await DecideAsync(fixture, "edit", file, ct);
        string? read = await DecideAsync(fixture, "read", file, ct);

        Assert.Equal("reject", edit);
        Assert.Equal("allow", read);
    }

    /// <summary>The manager's own edit of its Team's Memory is allowed.</summary>
    [Fact]
    public async Task Permission_ManagerEditOfTeamMemory_IsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string file = Path.Combine(dataDir.Path, "Teams", "Business", "memory", "x.md");

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "nova", ct);
        string? edit = await DecideAsync(fixture, "edit", file, ct);

        Assert.Equal("allow", edit);
    }

    /// <summary>An Agent's edit of an Instructions file is rejected at the Teams root and in its own Work Dir; a read is allowed.</summary>
    [Fact]
    public async Task Permission_EditOfAnInstructionsFile_IsRejected_AndReadAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string huddle = Path.Combine(dataDir.Path, "Teams", "Instructions.md");
        string own = Path.Combine(dataDir.Path, "Agents", "nova", "work", "Instructions.md");

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "boss", ct);

        Assert.Equal("reject", await DecideAsync(fixture, "edit", huddle, ct));
        Assert.Equal("reject", await DecideAsync(fixture, "edit", own, ct));
        Assert.Equal("allow", await DecideAsync(fixture, "read", huddle, ct));
    }
}

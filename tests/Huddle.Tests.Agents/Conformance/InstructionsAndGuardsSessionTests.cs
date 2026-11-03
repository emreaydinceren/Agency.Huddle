using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Pins the host wiring of Instructions and the file guards through a real session against the scripted ACP peer: that
/// <c>DotAcpPersonaHost</c> reads the three <c>AGENTS.md</c> levels into the appended system prompt, and that the permission
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

    /// <summary>
    /// The three AGENTS.md levels reach the system prompt in order, general to specific, after the Persona's text and
    /// behind the levels sentence; an <c>Instructions.md</c> in the Agent's Work Dir no longer does.
    /// </summary>
    [Fact]
    public async Task SessionStart_ReadsTheThreeAgentsMdFilesInOrder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string business = Path.Combine(dataDir.Path, "Teams", "Business");
        Directory.CreateDirectory(Path.Combine(business, "Alpha"));
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Agents", "nova", "work"));
        File.WriteAllText(Path.Combine(dataDir.Path, "Teams", "AGENTS.md"), "Huddle rule.");
        File.WriteAllText(Path.Combine(business, "AGENTS.md"), "Business rule.");
        File.WriteAllText(Path.Combine(business, "Alpha", "AGENTS.md"), "Alpha rule.");
        File.WriteAllText(Path.Combine(dataDir.Path, "Agents", "nova", "work", "Instructions.md"), "ZEBRA");

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "boss", ct);
        string prompt = (await fixture.AppendedSystemPromptAsync(ct)).ReplaceLineEndings("\n");

        int persona = prompt.IndexOf("You are Nova.", StringComparison.Ordinal);
        int levels = prompt.IndexOf("Each level below adds to the one before it.", StringComparison.Ordinal);
        int huddle = prompt.IndexOf("## Huddle-wide (", StringComparison.Ordinal);
        int team = prompt.IndexOf("## Team Business (", StringComparison.Ordinal);
        int project = prompt.IndexOf("## Project Business/Alpha (", StringComparison.Ordinal);
        Assert.True(persona >= 0 && persona < levels, "The levels sentence follows the Persona text.");
        Assert.True(levels < huddle && huddle < team && team < project, "Levels sentence, then Huddle-wide, Team, Project.");
        Assert.Contains(")\nHuddle rule.\n\n## Team Business (", prompt, StringComparison.Ordinal);
        Assert.Contains(")\nBusiness rule.\n\n## Project Business/Alpha (", prompt, StringComparison.Ordinal);
        Assert.Contains(")\nAlpha rule.", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("ZEBRA", prompt, StringComparison.Ordinal);
    }

    /// <summary>With no AGENTS.md files the prompt has no AGENTS.md part.</summary>
    [Fact]
    public async Task SessionStart_WithNoAgentsMdFiles_HasNoAgentsMdPart()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();

        await using MockAdapterFixture fixture = await StartAsync(dataDir, "boss", ct);
        string prompt = await fixture.AppendedSystemPromptAsync(ct);

        Assert.DoesNotContain("## Huddle-wide", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Each level below adds to the one before it.", prompt, StringComparison.Ordinal);
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

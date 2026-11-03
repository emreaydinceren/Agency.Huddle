using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Pins that the permission handler <c>DotAcpPersonaHost</c> gives a real session carries the Prompts-root guard (Spec §6.6, SB-R09): an edit of
/// <c>{DataDir}/Prompts/x.md</c> is rejected over the wire, while an edit of <c>Teams/Home/notes.md</c> is allowed. The refusal text T6 is
/// pinned in <c>PromptsRootGuardTests</c>; the reply carries no reason, so this test sees the decision only.
/// </summary>
public sealed class PromptsRootGuardSessionTests
{
    private static readonly string[] BothOptions = ["allow", "reject"];

    /// <summary>An edit under the Prompts folder is rejected by the session's handler and one in a Team folder is allowed.</summary>
    [Fact]
    public async Task Permission_EditInThePromptsFolder_IsRejected_AndTeamNotesAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string home = Path.Combine(dataDir.Path, "Teams", "Home");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, "_team.md"), "---\nteamType: 'development@1.0.0'\nmanager: 'nova'\n---\n");
        string text = "---\nName: Nova\nTitle: Nova\nAlias: Nova\nTeams: [Home]\n---\nYou are Nova.";
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:DataDir"] = dataDir.Path };

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", text), config, cancellationToken: ct);
        _ = await fixture.SessionNewAsync(ct);
        string? prompt = await DecideAsync(fixture, Path.Combine(dataDir.Path, "Prompts", "x.md"));
        string? notes = await DecideAsync(fixture, Path.Combine(home, "notes.md"));

        Assert.Equal("reject", prompt);
        Assert.Equal("allow", notes);
    }

    /// <summary>Asks the session's permission handler, through the wire, about an edit of one path and returns the option it chose.</summary>
    /// <param name="fixture">The started fixture, with a session open.</param>
    /// <param name="path">The path argument.</param>
    /// <returns>The chosen option id.</returns>
    private static async Task<string?> DecideAsync(MockAdapterFixture fixture, string path)
    {
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
                ["kind"] = "edit",
                ["status"] = "pending",
                ["rawInput"] = new JsonObject { ["file_path"] = path },
            },
            ["options"] = options,
        };

        JsonObject result = await fixture.Agent.SendRequestAsync("session/request_permission", parameters);
        return (string?)(result["outcome"] as JsonObject)?["optionId"];
    }
}

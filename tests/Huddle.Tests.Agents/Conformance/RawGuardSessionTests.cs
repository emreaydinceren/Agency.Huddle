using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Pins that the permission handler <c>DotAcpPersonaHost</c> gives a real session carries the Raw guard (Spec §8.6, SB-R05): a delete
/// under a Team's <c>raw</c> folder is rejected over the wire, while a delete under its <c>outputs</c> folder is allowed.
/// </summary>
public sealed class RawGuardSessionTests
{
    private static readonly string[] BothOptions = ["allow", "reject"];

    /// <summary>A delete under <c>Teams/Business/raw</c> is rejected by the session's handler and one under <c>outputs</c> is allowed.</summary>
    [Fact]
    public async Task Permission_DeleteUnderTeamRaw_IsRejected_AndOutputsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string business = Path.Combine(dataDir.Path, "Teams", "Business");
        Directory.CreateDirectory(business);
        File.WriteAllText(Path.Combine(business, "_team.md"), "---\nteamType: 'development@1.0.0'\nmanager: 'nova'\n---\n");
        string text = "---\nName: Nova\nTitle: Nova\nAlias: Nova\nTeams: [Business]\n---\nYou are Nova.";
        Dictionary<string, string?> config = new(StringComparer.Ordinal) { ["Team:DataDir"] = dataDir.Path };

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", text), config, cancellationToken: ct);
        _ = await fixture.SessionNewAsync(ct);
        string? raw = await DecideAsync(fixture, Path.Combine(business, "raw", "r1", "a.pdf"));
        string? outputs = await DecideAsync(fixture, Path.Combine(business, "outputs", "old.md"));

        Assert.Equal("reject", raw);
        Assert.Equal("allow", outputs);
    }

    /// <summary>Asks the session's permission handler, through the wire, about a delete of one path and returns the option it chose.</summary>
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
                ["kind"] = "delete",
                ["status"] = "pending",
                ["rawInput"] = new JsonObject { ["file_path"] = path },
            },
            ["options"] = options,
        };

        JsonObject result = await fixture.Agent.SendRequestAsync("session/request_permission", parameters);
        return (string?)(result["outcome"] as JsonObject)?["optionId"];
    }
}

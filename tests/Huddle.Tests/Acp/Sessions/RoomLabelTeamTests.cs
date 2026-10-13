using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins the Room label a Turn opens with when the Room is scoped to a Team (Team chats section 6.6):
/// the root label stays exactly as it always read, and the scoped one names the Team.
/// </summary>
public sealed class RoomLabelTeamTests
{
    /// <summary>A root Room's label is byte-identical to the literal the prompt produced before Team chats.</summary>
    [Fact]
    public void BuildPrompt_RootRoom_LabelIsTheOldLiteral()
    {
        WorkItem item = new("abc123", "Nova", "You", "hi", []);

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("[Room: Nova (id: abc123)] You: hi", prompt);
    }

    /// <summary>A scoped Room's label carries the Team after the id.</summary>
    [Fact]
    public void BuildPrompt_ScopedRoom_LabelNamesTheTeam()
    {
        WorkItem item = new("abc123", "Nova", "You", "hi", [], Team: "Marketing");

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("[Room: Nova (id: abc123, Team: Marketing)] You: hi", prompt);
    }

    /// <summary>The Greeting names the Team too, because it renders the same label.</summary>
    [Fact]
    public void BuildPrompt_ScopedGreeting_LabelNamesTheTeam()
    {
        WorkItem item = new("abc123", "Nova", string.Empty, string.Empty, [], WorkItemKind.Greeting, Team: "Marketing");

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("[Room: Nova (id: abc123, Team: Marketing)]", prompt.Split('\n')[0]);
    }

    /// <summary>A Room label hand-edited without the placeholder still renders, with no Team on it.</summary>
    [Fact]
    public void BuildPrompt_LabelOverrideWithoutTeamSuffix_OmitsTheTeam()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("turn.roomLabel", "[Room: {{roomName}} (id: {{roomId}})]");
        WorkItem item = new("abc123", "Nova", "You", "hi", [], Team: "Marketing");

        string prompt = RoomSession.BuildPrompt(item, prompts);

        Assert.Equal("[Room: Nova (id: abc123)] You: hi", prompt);
    }

    /// <summary>The shipped label has the suffix placeholder, optional so an older edit stays valid.</summary>
    [Fact]
    public void RoomLabelPrompt_DeclaresTeamSuffixAsOptional()
    {
        PromptDefinition definition = PromptCatalog.Get("turn.roomLabel");

        Assert.Contains("{{teamSuffix}}", definition.Placeholders);
        Assert.DoesNotContain("{{teamSuffix}}", definition.RequiredPlaceholders);
    }

    /// <summary>The Rooms help tells an Agent that a Room under a Team belongs to it and its files live in the Team folder.</summary>
    [Fact]
    public void GetHelpRooms_SaysATeamRoomBelongsToItsTeam()
    {
        string[] lines = PromptCatalog.Get("getHelp.rooms").Default.Split('\n', StringSplitOptions.TrimEntries);

        Assert.Contains("A Room listed under a Team belongs to that Team, and files shared in it are in that Team's folder.", lines);
    }
}

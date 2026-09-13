using System.Globalization;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Contracts;

public sealed class ProtocolJsonTests
{
    [Fact]
    public void Serialize_UsesCamelCase_AndStringEnums()
    {
        var post = new PostMessage("room-1", "msg-1", "hi");
        var postJson = ProtocolJson.Serialize(post);
        Assert.Contains("\"roomId\"", postJson, StringComparison.Ordinal);

        var member = new MemberInfo("agent-1", "echo", UserKind.Agent);
        var welcome = new Welcome("agent-1", "echo", new List<RoomInfo> { new("room-1", "echo", new List<MemberInfo> { member }) });
        var welcomeJson = ProtocolJson.Serialize(welcome);

        Assert.Contains("\"kind\":\"agent\"", welcomeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Kind\"", welcomeJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_TypePropertyNotFirst_Succeeds()
    {
        var version = ProtocolVersion.Current.ToString(CultureInfo.InvariantCulture);
        var result = ProtocolJson.Deserialize("{\"name\":\"x\",\"type\":\"hello\",\"version\":" + version + "}");

        var hello = Assert.IsType<Hello>(result);
        Assert.Equal("x", hello.Name);
    }

    [Fact]
    public void Serialize_OmitsNulls()
    {
        var json = ProtocolJson.Serialize(new Hello("x", null));

        Assert.DoesNotContain("description", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MessagePosted_RoundTrips_Members()
    {
        var chatMessage = new ChatMessage("m1", DateTimeOffset.UtcNow, "human", "You", "hi @echo");
        var mentioned = new MemberInfo("agent-1", "echo", UserKind.Agent);
        var human = new MemberInfo("human", "You", UserKind.Human);
        var posted = new MessagePosted(
            "room-1", "echo", chatMessage, true, new List<MemberInfo> { mentioned }, new List<MemberInfo> { human, mentioned },
            AgentMessagesSinceHuman: 3, Budget: 40);

        var json = ProtocolJson.Serialize(posted);
        var result = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(json));

        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.Id == "human" && m.Kind == UserKind.Human);
        Assert.Contains(result.Members, m => m.Id == "agent-1" && m.Kind == UserKind.Agent);
        Assert.Equal(3, result.AgentMessagesSinceHuman);
        Assert.Equal(40, result.Budget);
    }

    // The reason a new field on a server-to-client record costs no ProtocolVersion bump: a client
    // built before it still parses the line and ignores what it does not know. traps.md records the
    // opposite case - renaming a property silently changes the protocol with no signal at all.
    [Fact]
    public void MessagePosted_WithUnknownProperties_StillDeserialises()
    {
        var json = """
            {"type":"messagePosted","version":2,"roomId":"room-1","roomName":"echo",
             "message":{"id":"m1","timestamp":"2026-09-12T00:00:00+00:00","senderId":"human","senderName":"You","text":"hi"},
             "mentioned":false,"mentions":[],"members":[],
             "agentMessagesSinceHuman":2,"budget":40,"somethingFromALaterVersion":"ignored"}
            """.ReplaceLineEndings(string.Empty);

        var result = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(json));

        Assert.Equal("room-1", result.RoomId);
        Assert.Equal(2, result.AgentMessagesSinceHuman);
        Assert.Equal(40, result.Budget);
    }
}
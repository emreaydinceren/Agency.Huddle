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
            "room-1", "echo", chatMessage, true, new List<MemberInfo> { mentioned }, new List<MemberInfo> { human, mentioned });

        var json = ProtocolJson.Serialize(posted);
        var result = Assert.IsType<MessagePosted>(ProtocolJson.Deserialize(json));

        Assert.Equal(2, result.Members.Count);
        Assert.Contains(result.Members, m => m.Id == "human" && m.Kind == UserKind.Human);
        Assert.Contains(result.Members, m => m.Id == "agent-1" && m.Kind == UserKind.Agent);
    }
}
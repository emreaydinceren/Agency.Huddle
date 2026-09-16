using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Services;

/// <summary>Tests for <see cref="RoomNaming.Derive"/>, the one rule for a Room's auto-derived name.</summary>
public sealed class RoomNamingTests
{
    /// <summary>The Human never appears in a derived name - only Agents do.</summary>
    [Fact]
    public void Derive_ExcludesTheHuman()
    {
        var human = new User(KnownIds.Human, "You", UserKind.Human, null);
        var agent = new User("agent-1", "Echo", UserKind.Agent, null);

        var name = RoomNaming.Derive([human, agent]);

        Assert.Equal("Echo", name);
    }

    /// <summary>Multiple Agents are joined with a comma and a single space.</summary>
    [Fact]
    public void Derive_MultipleAgents_JoinsWithCommaSpace()
    {
        var alpha = new User("agent-1", "Nova", UserKind.Agent, null);
        var beta = new User("agent-2", "Jarvis", UserKind.Agent, null);

        var name = RoomNaming.Derive([alpha, beta]);

        Assert.Equal("Nova, Jarvis", name);
    }

    /// <summary>
    /// A single-Agent Room derives exactly that Agent's Name - proof that substituting
    /// <c>RoomNaming.Derive([agent])</c> for the old direct <c>agent.Name</c> literal is inert.
    /// </summary>
    [Fact]
    public void Derive_SingleAgent_EqualsThatAgentsName()
    {
        var agent = new User("agent-1", "Echo", UserKind.Agent, null);

        var name = RoomNaming.Derive([agent]);

        Assert.Equal(agent.Name, name);
    }

    /// <summary>A member set with no Agent at all derives the empty string.</summary>
    [Fact]
    public void Derive_NoAgents_ReturnsEmptyString()
    {
        var human = new User(KnownIds.Human, "You", UserKind.Human, null);

        var name = RoomNaming.Derive([human]);

        Assert.Equal(string.Empty, name);
    }
}

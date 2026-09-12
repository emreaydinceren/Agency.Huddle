namespace Agency.Huddle.Acp.Tests.Tools;

using System;
using Agency.Huddle.Console.Tools;
using Xunit;

/// <summary>
/// Exercises <see cref="ChatRoomRegistry"/> in isolation, without going through the tool server.
/// </summary>
public sealed class ChatRoomRegistryTests
{
    [Fact]
    public void Construction_SeedsBananasRoom()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();

        Assert.Contains("bananas", registry.List(), StringComparer.Ordinal);
    }

    [Fact]
    public void Create_AddsRoom_ListContainsBothItAndBananas()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();

        registry.Create("q4-planning");

        Assert.Contains("q4-planning", registry.List(), StringComparer.Ordinal);
        Assert.Contains("bananas", registry.List(), StringComparer.Ordinal);
    }

    [Fact]
    public void Create_DuplicateName_Throws()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();
        registry.Create("q4-planning");

        Assert.Throws<InvalidOperationException>(() => registry.Create("q4-planning"));
    }

    [Fact]
    public void Create_DuplicateNameDifferentCase_Throws()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();
        registry.Create("q4-planning");

        Assert.Throws<InvalidOperationException>(() => registry.Create("Q4-PLANNING"));
    }

    [Fact]
    public void Create_ExistingSeededRoomDifferentCase_Throws()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Create("BANANAS"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhitespaceName_Throws(string? name)
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();

        Assert.Throws<ArgumentException>(() => registry.Create(name!));
    }

    [Fact]
    public void Create_WhitespaceName_DoesNotChangeList()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();

        try
        {
            registry.Create("   ");
        }
        catch (ArgumentException)
        {
            // Expected; we only care that the room count did not change.
        }

        Assert.Single(registry.List());
    }

    [Fact]
    public void List_ReturnsExactlyOneRoomOnFreshRegistry()
    {
        ChatRoomRegistry registry = new ChatRoomRegistry();

        Assert.Single(registry.List(), name => string.Equals(name, "bananas", StringComparison.Ordinal));
    }
}

using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pure tests of <see cref="RoomScopeLabel"/>: which Rooms are Team one-to-ones and how their scope reads.</summary>
public sealed class RoomScopeLabelTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly User Human = new("h", "You", UserKind.Human, null);
    private static readonly User Rhea = new("a1", "Rhea", UserKind.Agent, null);
    private static readonly User Other = new("a2", "Omar", UserKind.Agent, null);

    private static FakeTeamCatalog Live() =>
        new() { Teams = [new TeamSummary("Development Team", ["Website"], ["rhea"], true)] };

    /// <summary>A root one-to-one has no scope and keeps its plain name.</summary>
    [Fact]
    public void Display_RootOneToOne_IsThePlainName()
    {
        Room room = new("r", "Rhea", Start);

        Assert.False(RoomScopeLabel.IsScopedOneToOne(room, [Human, Rhea]));
        Assert.Null(RoomScopeLabel.Scope(room));
        Assert.Equal("Rhea", RoomScopeLabel.Display(room, [Human, Rhea], Live()));
    }

    /// <summary>A Team one-to-one reads "Name · Team".</summary>
    [Fact]
    public void Display_TeamOneToOne_AppendsTheTeam()
    {
        Room room = new("r", "Rhea", Start) { Team = "Development Team" };

        Assert.True(RoomScopeLabel.IsScopedOneToOne(room, [Human, Rhea]));
        Assert.Equal("Rhea · Development Team", RoomScopeLabel.Display(room, [Human, Rhea], Live()));
    }

    /// <summary>A Project one-to-one reads "Name · Team / Project".</summary>
    [Fact]
    public void Display_ProjectOneToOne_AppendsTeamAndProject()
    {
        Room room = new("r", "Rhea", Start) { Team = "Development Team", Project = "Website" };

        Assert.Equal("Development Team / Website", RoomScopeLabel.Scope(room));
        Assert.Equal("Rhea · Development Team / Website", RoomScopeLabel.Display(room, [Human, Rhea], Live()));
    }

    /// <summary>A one-to-one of a Team that has left the catalog is not labelled: it keeps its plain name.</summary>
    [Fact]
    public void Display_VanishedTeam_IsThePlainName()
    {
        Room room = new("r", "Rhea", Start) { Team = "Gone Team" };

        Assert.Null(RoomScopeLabel.ScopeFor(room, [Human, Rhea], Live()));
        Assert.Equal("Rhea", RoomScopeLabel.Display(room, [Human, Rhea], Live()));
    }

    /// <summary>A manager-only Team's Team Room holds one Agent but is the Team Room, so it keeps its name.</summary>
    [Fact]
    public void IsScopedOneToOne_TeamRoom_IsFalse()
    {
        Room room = new("r", "Development Team", Start) { Team = "Development Team", IsTeamRoom = true };

        Assert.False(RoomScopeLabel.IsScopedOneToOne(room, [Human, Rhea]));
        Assert.Equal("Development Team", RoomScopeLabel.Display(room, [Human, Rhea], Live()));
    }

    /// <summary>A scoped Room with two Agents is a group Room, not a one-to-one.</summary>
    [Fact]
    public void IsScopedOneToOne_TwoAgents_IsFalse()
    {
        Room room = new("r", "Standup", Start) { Team = "Development Team" };

        Assert.False(RoomScopeLabel.IsScopedOneToOne(room, [Human, Rhea, Other]));
        Assert.Equal("Standup", RoomScopeLabel.Display(room, [Human, Rhea, Other], Live()));
    }
}

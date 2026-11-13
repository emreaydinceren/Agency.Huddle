using Agency.Huddle.App.Components.Shared.Dialogs;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="TeamRenameConfirmation"/> (Team Rename Spec §7, corrections-tr12 item 5): the title and message of a Team's and
/// a Project's confirmation, and its five points, with member counts 0, 1 and many, mid-Turn names 0, 1, 3 and 5, and is/are
/// agreement. A point's text carries its own leading space, so the dialog joins lead and text with nothing between.
/// </summary>
public sealed class TeamRenameConfirmationTests
{
    private const string Marketing = "Marketing";
    private const string Restart = " Each one keeps a summary of recent Messages but loses its working context.";

    /// <summary>A Team's title is the question naming both names.</summary>
    [Fact]
    public void Title_Team_NamesBothNames()
    {
        Assert.Equal("Rename Marketing to Growth?", TeamRenameConfirmation.Title(TeamRenameKind.Team, Marketing, "Growth"));
    }

    /// <summary>A Project's title says "project" before the old name.</summary>
    [Fact]
    public void Title_Project_SaysProject()
    {
        Assert.Equal("Rename project Launch to Launch-2027?", TeamRenameConfirmation.Title(TeamRenameKind.Project, "Launch", "Launch-2027"));
    }

    /// <summary>The message names the Team's folder for a Team and the Project's folder for a Project.</summary>
    [Fact]
    public void Message_TeamAndProject_NameTheirFolder()
    {
        Assert.Equal(
            "Huddle moves the Team's folder and updates its Rooms, Tasks, Requests and saved Views. Some things can go wrong or stay behind:",
            TeamRenameConfirmation.Message(TeamRenameKind.Team));
        Assert.Equal(
            "Huddle moves the Project's folder and updates its Rooms, Tasks, Requests and saved Views. Some things can go wrong or stay behind:",
            TeamRenameConfirmation.Message(TeamRenameKind.Project));
    }

    /// <summary>A Team with several members has five points; the first says how many restart.</summary>
    [Fact]
    public void Points_TeamWithThreeMembers_FivePointsFirstSaysThree()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 3, []);

        Assert.Equal(5, points.Count);
        Assert.Equal(new ConfirmPoint("3 members restart and start new conversations.", Restart), points[0]);
    }

    /// <summary>One member reads in the singular.</summary>
    [Fact]
    public void Points_TeamWithOneMember_IsSingular()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 1, []);

        Assert.Equal(new ConfirmPoint("1 member restarts and starts a new conversation.", Restart), points[0]);
    }

    /// <summary>A Team with no members has no restart point, so four remain and the first is the "told on their next Turn" one.</summary>
    [Fact]
    public void Points_TeamWithNoMembers_OmitsTheRestartPoint()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 0, []);

        Assert.Equal(4, points.Count);
        Assert.Equal("Agents are told on their next Turn, not now.", points[0].Lead);
    }

    /// <summary>One member mid-Turn is named with "is" and "loses".</summary>
    [Fact]
    public void Points_OneMidTurn_NamesHimWithIsAndLoses()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 3, ["Ada"]);

        Assert.Equal(Restart + " Ada is in the middle of a Turn and loses that Turn's work.", points[0].Text);
    }

    /// <summary>Two members mid-Turn are joined with "and" and take "are" and "lose".</summary>
    [Fact]
    public void Points_TwoMidTurn_JoinedWithAnd()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 3, ["Ada", "Bo"]);

        Assert.Equal(Restart + " Ada and Bo are in the middle of a Turn and lose that Turn's work.", points[0].Text);
    }

    /// <summary>Three members mid-Turn are all named: "A, B and C".</summary>
    [Fact]
    public void Points_ThreeMidTurn_AllNamed()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 5, ["Ada", "Bo", "Cy"]);

        Assert.Equal(Restart + " Ada, Bo and Cy are in the middle of a Turn and lose that Turn's work.", points[0].Text);
    }

    /// <summary>Five members mid-Turn: three are named and the rest are counted, "and 2 others".</summary>
    [Fact]
    public void Points_FiveMidTurn_ThreeNamedAndTwoOthers()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 6, ["Ada", "Bo", "Cy", "Di", "Ed"]);

        Assert.Equal(Restart + " Ada, Bo, Cy and 2 others are in the middle of a Turn and lose that Turn's work.", points[0].Text);
    }

    /// <summary>Four members mid-Turn leave one other, in the singular, and the verb still agrees with several people.</summary>
    [Fact]
    public void Points_FourMidTurn_OneOther()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 6, ["Ada", "Bo", "Cy", "Di"]);

        Assert.Equal(Restart + " Ada, Bo, Cy and 1 other are in the middle of a Turn and lose that Turn's work.", points[0].Text);
    }

    /// <summary>The Team's remaining points: told next Turn, own notes, links outside Huddle and not undone in one step.</summary>
    [Fact]
    public void Points_Team_TheFourSharedPointsNameTheTeam()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Team, Marketing, 3, []);

        Assert.Equal(
            new ConfirmPoint(
                "Agents are told on their next Turn, not now.",
                " Until then an Agent may use the old name. Huddle catches most of that, but an Agent that writes a file to the old folder brings it back as an empty Team."),
            points[1]);
        Assert.Equal(new ConfirmPoint("Agents' own notes keep the old name", " until each Agent updates them."), points[2]);
        Assert.Equal(new ConfirmPoint("Links outside Huddle break.", " Bookmarks, scripts or other programs that use Teams/Marketing need updating."), points[3]);
        Assert.Equal(
            new ConfirmPoint("This can't be undone in one step,", " and Marketing stays reserved. Renaming back is another rename, with the same effects."),
            points[4]);
    }

    /// <summary>A Project restarts no Agent, so its first point says so whatever the member count or Turns; the link path has the Team too.</summary>
    [Fact]
    public void Points_Project_NoAgentRestartsAndPathNamesTheTeam()
    {
        IReadOnlyList<ConfirmPoint> points = TeamRenameConfirmation.Points(TeamRenameKind.Project, "Launch", 3, ["Ada"], "Growth");

        Assert.Equal(5, points.Count);
        Assert.Equal(new ConfirmPoint("No Agent restarts.", " Agents in its Rooms keep their conversations."), points[0]);
        Assert.Equal(" Until then an Agent may use the old name. Huddle catches most of that, but an Agent that writes a file to the old folder brings it back as an empty Project.", points[1].Text);
        Assert.Equal(" Bookmarks, scripts or other programs that use Teams/Growth/Launch need updating.", points[3].Text);
        Assert.Equal(" and Launch stays reserved. Renaming back is another rename, with the same effects.", points[4].Text);
    }
}

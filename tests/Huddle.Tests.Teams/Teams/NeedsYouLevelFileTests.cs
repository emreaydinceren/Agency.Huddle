using System.Globalization;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins what is left of the <c>needsYouLevel</c> and <c>spendCap</c> lines in <see cref="TeamFile"/> and <see cref="ProjectFile"/> (nothing: ADR-0088 keeps the values in <c>team.db</c>) and the text helpers the store still uses.</summary>
public sealed class NeedsYouLevelFileTests
{
    private const string TeamText = "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'\nspendCap: '5.50'\nowner: 'Ada'\n---\nNotes\n";

    /// <summary>A stray level and cap line in a Team file is an unknown line: kept verbatim in file order, composed back unchanged and meaning nothing.</summary>
    [Fact]
    public void TeamFile_StrayLevelAndCapLines_RoundTripAsUnknownLines()
    {
        Assert.True(TeamFile.TryParse(TeamText, out TeamFile? file));

        Assert.Equal(["needsYouLevel: 'run-inside-team'", "spendCap: '5.50'", "owner: 'Ada'"], file.UnknownLines);
        Assert.Equal(TeamText, file.Compose());
    }

    /// <summary>A stray level and cap line in a Project file is an unknown line, kept verbatim next to the avatar and the notes.</summary>
    [Fact]
    public void ProjectFile_StrayLevelAndCapLines_RoundTripAsUnknownLines()
    {
        const string Text = "---\navatarLabel: 'LA'\nneedsYouLevel: 'ask-me'\nspendCap: '2'\nowner: 'Ada'\n---\nNotes\n";

        ProjectFile file = ProjectFile.Parse(Text);

        Assert.Equal("LA", file.Avatar.Label);
        Assert.Equal(["needsYouLevel: 'ask-me'", "spendCap: '2'", "owner: 'Ada'"], file.UnknownLines);
        Assert.Equal(Text, file.Compose());
    }

    /// <summary>An unknown level or a cap that is zero, negative, comma-grouped or not a number reads as unset.</summary>
    /// <param name="level">The stored level text.</param>
    /// <param name="cap">The stored cap text.</param>
    [Theory]
    [InlineData("sometimes", "0")]
    [InlineData("", "-3")]
    [InlineData("ASK-ME-ALWAYS", "5,00")]
    [InlineData("run inside team", "lots")]
    public void Text_InvalidValues_ReadAsUnset(string level, string cap)
    {
        Assert.Null(NeedsYouLevelText.ParseOrNull(level));
        Assert.Null(NeedsYouLevelText.ParseCapOrNull(cap));
    }

    /// <summary>The cap is written in the invariant culture, with a point, whatever the current culture is.</summary>
    [Fact]
    public void Text_Cap_IsWrittenInTheInvariantCulture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("12.5", NeedsYouLevelText.FormatCap(12.5m));
            Assert.Equal(12.5m, NeedsYouLevelText.ParseCapOrNull("12.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>Every level has a wire name that reads back, a display name and, for a scope, the word a sentence uses.</summary>
    [Fact]
    public void LevelText_WireAndDisplayNames_AreTheDocumentedOnes()
    {
        Assert.Equal("ask-me", NeedsYouLevel.AskMe.ToWire());
        Assert.Equal("run-inside-team", NeedsYouLevel.RunInsideTheTeam.ToWire());
        Assert.Equal("Ask me", NeedsYouLevel.AskMe.ToDisplay());
        Assert.Equal("Run inside the Team", NeedsYouLevel.RunInsideTheTeam.ToDisplay());
        Assert.Equal("global", NeedsYouScope.Global.ToDisplay());
        Assert.Equal("Team", NeedsYouScope.Team.ToDisplay());
        Assert.Equal("project", NeedsYouScope.Project.ToDisplay());
        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, NeedsYouLevelText.ParseOrNull(" RUN-INSIDE-TEAM "));
    }
}

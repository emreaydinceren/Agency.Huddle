using System.Globalization;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins the <c>needsYouLevel</c> and <c>spendCap</c> keys of <see cref="TeamFile"/> and <see cref="ProjectFile"/> (ADR-0088).</summary>
public sealed class NeedsYouLevelFileTests
{
    private const string TeamText = "---\nteamType: 'development@1.0.0'\nneedsYouLevel: 'run-inside-team'\nspendCap: '5.50'\nowner: 'Ada'\n---\nNotes\n";

    /// <summary>A Team file with both keys parses them and composes back to the same text, keeping the unknown line and the notes.</summary>
    [Fact]
    public void TeamFile_WithLevelAndCap_RoundTrips()
    {
        Assert.True(TeamFile.TryParse(TeamText, out TeamFile? file));

        Assert.Equal(NeedsYouLevel.RunInsideTheTeam, file.NeedsYouLevel);
        Assert.Equal(5.50m, file.SpendCap);
        Assert.Equal(["owner: 'Ada'"], file.UnknownLines);
        Assert.Equal(TeamText, file.Compose());
    }

    /// <summary>A Team file with neither key reads both as unset and writes no line for them.</summary>
    [Fact]
    public void TeamFile_WithoutKeys_ReadsUnsetAndWritesNothing()
    {
        const string Text = "---\nteamType: 'development@1.0.0'\n---\n";
        Assert.True(TeamFile.TryParse(Text, out TeamFile? file));

        Assert.Null(file.NeedsYouLevel);
        Assert.Null(file.SpendCap);
        Assert.Equal(Text, file.Compose());
    }

    /// <summary>An unknown level or a cap that is zero, negative, comma-grouped or not a number reads as unset, and a rewrite drops the line.</summary>
    /// <param name="level">The stored level text.</param>
    /// <param name="cap">The stored cap text.</param>
    [Theory]
    [InlineData("sometimes", "0")]
    [InlineData("", "-3")]
    [InlineData("ASK-ME-ALWAYS", "5,00")]
    [InlineData("run inside team", "lots")]
    public void TeamFile_InvalidValues_ReadAsUnset(string level, string cap)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(cap);
        Assert.True(TeamFile.TryParse($"---\nteamType: 'development@1.0.0'\nneedsYouLevel: '{level}'\nspendCap: '{cap}'\n---\n", out TeamFile? file));

        Assert.Null(file.NeedsYouLevel);
        Assert.Null(file.SpendCap);
        Assert.DoesNotContain("needsYouLevel", file.Compose(), StringComparison.Ordinal);
        Assert.DoesNotContain("spendCap", file.Compose(), StringComparison.Ordinal);
    }

    /// <summary>The wire name is read ignoring case, and the key is found whatever its case, so a hand edit is not lost.</summary>
    [Fact]
    public void TeamFile_WireNameAndKey_AreCaseInsensitive()
    {
        Assert.True(TeamFile.TryParse("---\nteamType: 'development@1.0.0'\nNEEDSYOULEVEL: 'Ask-Me'\n---\n", out TeamFile? file));

        Assert.Equal(NeedsYouLevel.AskMe, file.NeedsYouLevel);
    }

    /// <summary>The cap is written in the invariant culture, with a point, whatever the current culture is.</summary>
    [Fact]
    public void TeamFile_Cap_IsWrittenInTheInvariantCulture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            TeamFile file = new(VersionedRef.None, null, null, null, string.Empty) { NeedsYouLevel = NeedsYouLevel.RunInsideTheTeam, SpendCap = 12.5m };

            Assert.Equal("---\nneedsYouLevel: 'run-inside-team'\nspendCap: '12.5'\n---\n", file.Compose());
            Assert.True(TeamFile.TryParse(file.Compose(), out TeamFile? parsed));
            Assert.Equal(12.5m, parsed.SpendCap);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>A Project file with both keys parses them, keeps the avatar, the unknown line and the notes, and composes back to the same text.</summary>
    [Fact]
    public void ProjectFile_WithLevelAndCap_RoundTrips()
    {
        const string Text = "---\navatarLabel: 'LA'\nneedsYouLevel: 'ask-me'\nspendCap: '2'\nowner: 'Ada'\n---\nNotes\n";

        ProjectFile file = ProjectFile.Parse(Text);

        Assert.Equal(NeedsYouLevel.AskMe, file.NeedsYouLevel);
        Assert.Equal(2m, file.SpendCap);
        Assert.Equal("LA", file.Avatar.Label);
        Assert.Equal(["owner: 'Ada'"], file.UnknownLines);
        Assert.Equal(Text, file.Compose());
    }

    /// <summary>A Project file with an unknown level or a bad cap reads both as unset, and a file with no frontmatter reads as unset too.</summary>
    [Fact]
    public void ProjectFile_InvalidOrMissing_ReadAsUnset()
    {
        ProjectFile invalid = ProjectFile.Parse("---\nneedsYouLevel: 'bogus'\nspendCap: '0'\n---\n");
        ProjectFile none = ProjectFile.Parse("needsYouLevel: 'run-inside-team'\n");

        Assert.Null(invalid.NeedsYouLevel);
        Assert.Null(invalid.SpendCap);
        Assert.Null(none.NeedsYouLevel);
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

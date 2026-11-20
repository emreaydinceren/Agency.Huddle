using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>The <c>oneInProgressPerProject</c> setting (ADR-0103): how the effective rule resolves, and how both files read and write the key.</summary>
public sealed class OneInProgressRuleTests
{
    /// <summary>A Project's <c>on</c> beats the Team's <c>off</c>, and its <c>off</c> beats the Team's <c>on</c>, and the Project is named as the setter.</summary>
    [Fact]
    public void Resolve_ProjectValue_BeatsTheTeam()
    {
        Assert.Equal(new ResolvedRule(true, RuleSource.Project), OneInProgressRule.Resolve(RuleSetting.Off, RuleSetting.On, true));
        Assert.Equal(new ResolvedRule(false, RuleSource.Project), OneInProgressRule.Resolve(RuleSetting.On, RuleSetting.Off, true));
    }

    /// <summary>An absent Project value inherits the Team's, and an absent Team value is off.</summary>
    [Fact]
    public void Resolve_AbsentProject_InheritsTheTeam()
    {
        Assert.Equal(new ResolvedRule(true, RuleSource.Team), OneInProgressRule.Resolve(RuleSetting.On, RuleSetting.Absent, true));
        Assert.Equal(new ResolvedRule(false, RuleSource.Team), OneInProgressRule.Resolve(RuleSetting.Off, RuleSetting.Absent, true));
        Assert.Equal(new ResolvedRule(false, RuleSource.Team), OneInProgressRule.Resolve(RuleSetting.Absent, RuleSetting.Absent, true));
    }

    /// <summary>A Task with no Project uses the Team's value, whatever a Project file says.</summary>
    [Fact]
    public void Resolve_TaskWithNoProject_UsesTheTeam()
    {
        Assert.Equal(new ResolvedRule(true, RuleSource.Team), OneInProgressRule.Resolve(RuleSetting.On, RuleSetting.Off, false));
    }

    /// <summary>An unreadable value counts as absent, in both files, and the key is then not written back.</summary>
    [Fact]
    public void Parse_UnreadableValue_CountsAsAbsent()
    {
        Assert.Equal(RuleSetting.Absent, OneInProgressRule.Parse("maybe"));
        Assert.Equal(RuleSetting.Absent, OneInProgressRule.Parse(null));
        Assert.Equal(RuleSetting.Absent, ProjectFile.Parse("---\noneInProgressPerProject: 'maybe'\n---\n").OneInProgressPerProject);
        Assert.True(TeamFile.TryParse("---\nteamType: 'web@1.2.0'\noneInProgressPerProject: 'true'\n---\n", out TeamFile? team));
        Assert.Equal(RuleSetting.Absent, team.OneInProgressPerProject);
        Assert.Equal("---\nteamType: 'web@1.2.0'\n---\n", team.Compose());
    }

    /// <summary>A Team file round-trips the key as written, keeping a hand-edited line next to it.</summary>
    [Fact]
    public void TeamFile_RoundTripsTheKey_AndKeepsHandEditedLines()
    {
        const string Text = "---\nteamType: 'web@1.2.0'\noneInProgressPerProject: 'on'\nowner: 'Ada'\n---\nNotes\n";

        Assert.True(TeamFile.TryParse(Text, out TeamFile? file));

        Assert.Equal(RuleSetting.On, file.OneInProgressPerProject);
        Assert.Equal(["owner: 'Ada'"], file.UnknownLines);
        Assert.Equal(Text, file.Compose());
        Assert.Equal("---\nteamType: 'web@1.2.0'\noneInProgressPerProject: 'off'\nowner: 'Ada'\n---\nNotes\n", (file with { OneInProgressPerProject = RuleSetting.Off }).Compose());
    }

    /// <summary>A Team file with the key absent never writes it.</summary>
    [Fact]
    public void TeamFile_AbsentKey_IsNeverWritten()
    {
        Assert.True(TeamFile.TryParse("---\nteamType: 'web@1.2.0'\n---\n", out TeamFile? file));

        Assert.Equal(RuleSetting.Absent, file.OneInProgressPerProject);
        Assert.DoesNotContain(OneInProgressRule.Key, file.Compose(), StringComparison.Ordinal);
    }

    /// <summary>A Project file round-trips the key, in each of its two values, keeping a hand-edited line and the notes.</summary>
    /// <param name="value">The stored value.</param>
    [Theory]
    [InlineData("on")]
    [InlineData("off")]
    public void ProjectFile_RoundTripsTheKey_AndKeepsHandEditedLines(string value)
    {
        RuleSetting expected = OneInProgressRule.Parse(value);
        string text = $"---\navatarLabel: 'LA'\noneInProgressPerProject: '{value}'\nowner: 'Ada'\n---\nNotes\n";

        ProjectFile file = ProjectFile.Parse(text);

        Assert.Equal(expected, file.OneInProgressPerProject);
        Assert.Equal(["owner: 'Ada'"], file.UnknownLines);
        Assert.Equal(text, file.Compose());
    }

    /// <summary>A Project with no value composes no key: with nothing else set it is an empty frontmatter, so no file need be written.</summary>
    [Fact]
    public void ProjectFile_AbsentKey_IsNeverWritten()
    {
        Assert.Equal("---\n---\n", ProjectFile.Empty.Compose());
        Assert.Equal(ProjectFile.Empty, ProjectFile.Empty with { OneInProgressPerProject = RuleSetting.Absent });
    }
}

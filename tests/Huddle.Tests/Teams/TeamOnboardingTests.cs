using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins the onboarding marker in <c>_team.md</c> and the <c>## Onboarding</c> section of a process (Task 58, PR-5).</summary>
public sealed class TeamOnboardingTests
{
    /// <summary>The steps are the lines of the Onboarding section, up to the next heading.</summary>
    [Fact]
    public void OnboardingSteps_SectionPresent_ReturnsItsLines()
    {
        string text = "# Process\n\n## Gates\n\n- x\n\n## Onboarding\n\n- Read the workspace.\n- Run the tests.\n\n## Escalation\n\n- Ask.\n";

        string? steps = ProcessDocument.OnboardingSteps(text);

        Assert.Equal("- Read the workspace.\n- Run the tests.", steps);
    }

    /// <summary>A process without an Onboarding section, or with an empty one, has no steps.</summary>
    [Theory]
    [InlineData("# Process\n\n## Gates\n\n- x\n")]
    [InlineData("# Process\n\n## Onboarding\n\n## Escalation\n")]
    public void OnboardingSteps_NoSteps_ReturnsNull(string text)
    {
        Assert.Null(ProcessDocument.OnboardingSteps(text));
    }

    /// <summary>The marker is written as an <c>onboarded: true</c> line and read back.</summary>
    [Fact]
    public void TeamFile_Onboarded_RoundTrips()
    {
        TeamFile file = new(new VersionedRef("web", "1.0.0"), "Lee", null, null, TeamPaths.ProcessFileName, string.Empty) { Onboarded = true };

        string text = file.Compose();

        Assert.True(TeamFile.TryParse(text, out TeamFile? parsed));
        Assert.Equal("---\nteamType: 'web@1.0.0'\nmanager: 'Lee'\nprocess: 'Process.md'\nonboarded: true\n---\n", text);
        Assert.True(parsed.Onboarded);
    }

    /// <summary>A file with no marker reads as not onboarded and writes no marker line.</summary>
    [Fact]
    public void TeamFile_NoMarker_NotOnboarded()
    {
        TeamFile file = new(new VersionedRef("web", "1.0.0"), null, null, null, TeamPaths.ProcessFileName, string.Empty);

        Assert.DoesNotContain("onboarded", file.Compose());
        Assert.False(file.Onboarded);
    }
}

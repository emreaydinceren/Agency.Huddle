using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Spec §7.2: <c>_team.md</c> carries the process settings and the archived members, and a missing <c>teamType</c> parses.</summary>
public sealed class TeamFileSettingsTests
{
    private const string FullText =
        "---\n"
        + "teamType: 'lightweight-flow@1.0.0'\n"
        + "manager: 'Ada'\n"
        + "workspace: 'C:\\Work\\Flow'\n"
        + "owns: 'Ships the flow'\n"
        + "onboarded: true\n"
        + "avatarLabel: 'FL'\n"
        + "avatarImage: 'flow.png'\n"
        + "avatarBackground: '#112233'\n"
        + "gates: ['qa-evidence-before-done', 'checks-before-merge']\n"
        + "wip: 4\n"
        + "retro: { every: 10, unit: 'done-tasks' }\n"
        + "digest: { every: 1, unit: 'days' }\n"
        + "checkCommand: 'dotnet test'\n"
        + "template: 'lightweight-flow@1.0.0'\n"
        + "archivedMembers: ['Bob', 'Cy']\n"
        + "---\n"
        + "# Notes\n";

    private static TeamFile Parse(string text)
    {
        Assert.True(TeamFile.TryParse(text, out TeamFile? file));
        return file;
    }

    /// <summary>Every §7.2 key parses into its property.</summary>
    [Fact]
    public void TryParse_EveryKey_HoldsEachValue()
    {
        TeamFile file = Parse(FullText);

        Assert.Equal(new VersionedRef("lightweight-flow", "1.0.0"), file.TeamType);
        Assert.Equal("Ada", file.Manager);
        Assert.Equal("C:\\Work\\Flow", file.Workspace);
        Assert.Equal("Ships the flow", file.Owns);
        Assert.True(file.Onboarded);
        Assert.Equal("FL", file.Avatar.Label);
        Assert.Equal("flow.png", file.Avatar.Image);
        Assert.Equal("#112233", file.Avatar.Background);
        Assert.Equal("# Notes\n", file.Notes);
        Assert.Empty(file.UnknownLines);
    }

    /// <summary>Gates, wip, retro, digest, checkCommand and template land on <c>Process</c>.</summary>
    [Fact]
    public void TryParse_ProcessKeys_HoldValuesOnProcess()
    {
        TeamFile file = Parse(FullText);

        Assert.Equal([GateId.QaEvidenceBeforeDone, GateId.ChecksBeforeMerge], file.Process.Gates);
        Assert.Equal(4, file.Process.Wip);
        Assert.Equal("{ every: 10, unit: 'done-tasks' }", file.Process.Retro);
        Assert.Equal("{ every: 1, unit: 'days' }", file.Process.Digest);
        Assert.Equal("dotnet test", file.Process.CheckCommand);
        Assert.Equal("lightweight-flow@1.0.0", file.Process.Template);
    }

    /// <summary>The archived members parse in file order.</summary>
    [Fact]
    public void TryParse_ArchivedMembers_HoldsNamesInOrder()
    {
        TeamFile file = Parse(FullText);

        Assert.Equal(["Bob", "Cy"], file.ArchivedMembers);
    }

    /// <summary>A file with none of the process keys has empty settings and no archived members.</summary>
    [Fact]
    public void TryParse_NoProcessKeys_ProcessIsEmpty()
    {
        TeamFile file = Parse("---\nteamType: 'a@1'\n---\n");

        Assert.Empty(file.Process.Gates);
        Assert.Null(file.Process.Wip);
        Assert.Null(file.Process.Retro);
        Assert.Null(file.Process.Digest);
        Assert.Null(file.Process.CheckCommand);
        Assert.Null(file.Process.Template);
        Assert.Empty(file.ArchivedMembers);
    }

    /// <summary>Compose then TryParse gives the same values, and a second Compose gives the same text.</summary>
    [Fact]
    public void Compose_ThenTryParse_RoundTripsEveryValue()
    {
        TeamFile first = Parse(FullText);

        string composed = first.Compose();
        TeamFile second = Parse(composed);

        Assert.Equal(first.TeamType, second.TeamType);
        Assert.Equal(first.Manager, second.Manager);
        Assert.Equal(first.Workspace, second.Workspace);
        Assert.Equal(first.Owns, second.Owns);
        Assert.Equal(first.Onboarded, second.Onboarded);
        Assert.Equal(first.Avatar, second.Avatar);
        Assert.Equal(first.Process.Gates, second.Process.Gates);
        Assert.Equal(first.Process.Wip, second.Process.Wip);
        Assert.Equal(first.Process.Retro, second.Process.Retro);
        Assert.Equal(first.Process.Digest, second.Process.Digest);
        Assert.Equal(first.Process.CheckCommand, second.Process.CheckCommand);
        Assert.Equal(first.Process.Template, second.Process.Template);
        Assert.Equal(first.ArchivedMembers, second.ArchivedMembers);
        Assert.Equal(first.Notes, second.Notes);
        Assert.Equal(composed, second.Compose());
    }

    /// <summary>Compose writes each process key under its §7.2 name.</summary>
    [Fact]
    public void Compose_ProcessSettings_WritesEachKey()
    {
        string composed = Parse(FullText).Compose();

        Assert.Contains("\ngates: ['qa-evidence-before-done', 'checks-before-merge']\n", composed, StringComparison.Ordinal);
        Assert.Contains("\nwip: 4\n", composed, StringComparison.Ordinal);
        Assert.Contains("\nretro: { every: 10, unit: 'done-tasks' }\n", composed, StringComparison.Ordinal);
        Assert.Contains("\ndigest: { every: 1, unit: 'days' }\n", composed, StringComparison.Ordinal);
        Assert.Contains("\ncheckCommand: 'dotnet test'\n", composed, StringComparison.Ordinal);
        Assert.Contains("\ntemplate: 'lightweight-flow@1.0.0'\n", composed, StringComparison.Ordinal);
        Assert.Contains("\narchivedMembers: ['Bob', 'Cy']\n", composed, StringComparison.Ordinal);
    }

    /// <summary>Correction C6: a file without <c>teamType</c> parses to <see cref="VersionedRef.None"/>.</summary>
    [Fact]
    public void TryParse_NoTeamType_ParsesToNone()
    {
        TeamFile file = Parse("---\nmanager: 'Ada'\n---\nbody\n");

        Assert.Equal(VersionedRef.None, file.TeamType);
        Assert.Equal("Ada", file.Manager);
    }

    /// <summary>Correction C6: composing a Team with no type omits the key, and the result still parses.</summary>
    [Fact]
    public void Compose_TeamTypeNone_OmitsTheKey()
    {
        TeamFile file = Parse("---\nmanager: 'Ada'\n---\nbody\n");

        string composed = file.Compose();

        Assert.DoesNotContain("teamType", composed, StringComparison.Ordinal);
        Assert.Equal("---\nmanager: 'Ada'\n---\nbody\n", composed);
        Assert.Equal(VersionedRef.None, Parse(composed).TeamType);
    }

    /// <summary>An unknown key survives a parse and compose round trip.</summary>
    [Fact]
    public void Compose_UnknownKey_Survives()
    {
        TeamFile file = Parse("---\nteamType: 'a@1'\ncolour: 'teal'\ngates: ['failing-test-before-fix']\n---\n");

        string composed = file.Compose();

        Assert.Equal(["colour: 'teal'"], file.UnknownLines);
        Assert.Contains("\ncolour: 'teal'\n", composed, StringComparison.Ordinal);
        Assert.Equal(["colour: 'teal'"], Parse(composed).UnknownLines);
        Assert.Equal([GateId.FailingTestBeforeFix], Parse(composed).Process.Gates);
    }
}

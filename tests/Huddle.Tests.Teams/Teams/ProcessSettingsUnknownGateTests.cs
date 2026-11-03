using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for the unknown gate ids <see cref="ProcessSettings"/> keeps so a <c>_team.md</c> rewrite does not lose them.</summary>
public sealed class ProcessSettingsUnknownGateTests
{
    private const string TeamText = "---\nteamType: 'development@1.0.0'\ngates: ['checks-before-merge', 'deploy-on-friday']\n---\nNotes\n";

    /// <summary>Parsing keeps the known gate in Gates and the unknown id, exactly as written, in UnknownGates.</summary>
    [Fact]
    public void Parse_UnknownGate_IsKeptApart()
    {
        Assert.True(TeamFile.TryParse(TeamText, out TeamFile? file));

        Assert.Equal([GateId.ChecksBeforeMerge], file.Process.Gates);
        Assert.Equal(["deploy-on-friday"], file.Process.UnknownGates);
    }

    /// <summary>Changing the gates and composing the file again still writes the unknown id back.</summary>
    [Fact]
    public void Compose_AfterWithGates_PreservesUnknownGate()
    {
        Assert.True(TeamFile.TryParse(TeamText, out TeamFile? file));

        string composed = file.WithGates([GateId.QaEvidenceBeforeDone]).Compose();

        Assert.True(TeamFile.TryParse(composed, out TeamFile? reread));
        Assert.Equal([GateId.QaEvidenceBeforeDone], reread.Process.Gates);
        Assert.Equal(["deploy-on-friday"], reread.Process.UnknownGates);
    }

    /// <summary>A file with only unknown gates still writes its gates line, so turning every known gate off loses nothing.</summary>
    [Fact]
    public void Compose_NoKnownGatesLeft_StillWritesUnknownGate()
    {
        Assert.True(TeamFile.TryParse(TeamText, out TeamFile? file));

        string composed = file.WithGates([]).Compose();

        Assert.Contains("'deploy-on-friday'", composed, StringComparison.Ordinal);
        Assert.DoesNotContain("checks-before-merge", composed, StringComparison.Ordinal);
    }
}

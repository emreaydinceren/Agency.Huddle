using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="TeamFile.WithGates"/>.</summary>
public sealed class TeamFileWithGatesTests
{
    /// <summary>WithGates replaces only the gates and the composed text lists them.</summary>
    [Fact]
    public void WithGates_ReplacesGatesOnly()
    {
        Assert.True(TeamFile.TryParse("---\nteamType: 'a@1'\nmanager: 'Ada'\nwip: 3\ngates: ['failing-test-before-fix']\n---\nbody\n", out TeamFile? file));

        TeamFile changed = file.WithGates([GateId.ChecksBeforeMerge, GateId.QaEvidenceBeforeDone]);

        Assert.Equal([GateId.ChecksBeforeMerge, GateId.QaEvidenceBeforeDone], changed.Process.Gates);
        Assert.Equal(3, changed.Process.Wip);
        Assert.Equal("Ada", changed.Manager);
        Assert.Equal([GateId.FailingTestBeforeFix], file.Process.Gates);
        Assert.Equal(
            "---\nteamType: 'a@1'\nmanager: 'Ada'\ngates: ['checks-before-merge', 'qa-evidence-before-done']\nwip: 3\n---\nbody\n",
            changed.Compose());
    }
}

using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="TeamCheckBar"/>: coverage, vendors, processes and whether Start Team is enabled (HR-4, HR-8, HR-9, NF-6).</summary>
public sealed class TeamCheckBarTests
{
    private static readonly string[] Steps = ["Generate", "Challenge", "Verify"];

    private static TeamTypeManifest Type()
    {
        return new TeamTypeManifest(1, "development", "1.0.0", "Development", 1, new WorkspaceSpec(WorkspaceKind.None, "Workspace", false),
            Coverage: TeamCheckBarTests.Steps,
            Roles:
            [
                new RoleSpec("lead", "Lead", "c/lead.md", Manager: true, Required: true, Covers: ["Generate"]),
                new RoleSpec("developer", "Developer", "c/dev.md", Required: true, MaxPerTeam: 0, Covers: ["Generate"]),
                new RoleSpec("reviewer", "Reviewer", "c/rev.md", Covers: ["Challenge"]),
                new RoleSpec("qa", "QA", "c/qa.md", Covers: ["Verify"]),
            ]);
    }

    private static CardState Card(string roleId, string adapter = "claude", CardReadiness readiness = CardReadiness.Ready)
    {
        return new CardState(roleId, roleId + "-name", new ModelPick(adapter, null), [], [], readiness,
            readiness == CardReadiness.Ready ? [] : ["no adapter"]);
    }

    /// <summary>Coverage is true for a step only when a hired role covers it, so a Team without a QA lacks Verify.</summary>
    [Fact]
    public void Compute_WithAndWithoutQa_ReportsVerifyCoverage()
    {
        TeamCheck without = TeamCheckBar.Compute(TeamCheckBarTests.Type(), [TeamCheckBarTests.Card("lead"), TeamCheckBarTests.Card("developer"), TeamCheckBarTests.Card("reviewer")]);
        TeamCheck with = TeamCheckBar.Compute(TeamCheckBarTests.Type(), [TeamCheckBarTests.Card("lead"), TeamCheckBarTests.Card("developer"), TeamCheckBarTests.Card("qa")]);

        Assert.False(without.Coverage["Verify"]);
        Assert.True(without.Coverage["Challenge"]);
        Assert.True(with.Coverage["Verify"]);
        Assert.False(with.Coverage["Challenge"]);
        Assert.Equal(3, with.Coverage.Count);
    }

    /// <summary>Vendor count is the number of distinct adapters among hired cards; process count is the member count.</summary>
    [Fact]
    public void Compute_OneVersusTwoAdapters_CountsVendorsAndProcesses()
    {
        TeamCheck one = TeamCheckBar.Compute(TeamCheckBarTests.Type(), [TeamCheckBarTests.Card("lead"), TeamCheckBarTests.Card("developer")]);
        TeamCheck two = TeamCheckBar.Compute(TeamCheckBarTests.Type(), [TeamCheckBarTests.Card("lead"), TeamCheckBarTests.Card("developer", "codex"), TeamCheckBarTests.Card("developer", "codex")]);

        Assert.Equal(1, one.VendorCount);
        Assert.Equal(2, one.ProcessCount);
        Assert.Equal(2, two.VendorCount);
        Assert.Equal(3, two.ProcessCount);
    }

    /// <summary>A Team with the manager, every required role and Ready cards can start with no blockers.</summary>
    [Fact]
    public void Compute_AllReady_CanStart()
    {
        TeamCheck check = TeamCheckBar.Compute(TeamCheckBarTests.Type(), [TeamCheckBarTests.Card("lead"), TeamCheckBarTests.Card("developer")]);

        Assert.True(check.CanStart);
        Assert.Empty(check.Blockers);
    }

    /// <summary>A Needs-setup card blocks Start Team and the blocker names the member.</summary>
    [Fact]
    public void Compute_NeedsSetupCard_Blocks()
    {
        TeamCheck check = TeamCheckBar.Compute(TeamCheckBarTests.Type(),
            [TeamCheckBarTests.Card("lead"), TeamCheckBarTests.Card("developer", readiness: CardReadiness.NeedsSetup)]);

        Assert.False(check.CanStart);
        Assert.Equal(["developer-name needs setup: no adapter."], check.Blockers);
    }

    /// <summary>A missing required role blocks Start Team and is named by its title.</summary>
    [Fact]
    public void Compute_MissingRequiredRole_BlocksAndNamesIt()
    {
        TeamCheck check = TeamCheckBar.Compute(TeamCheckBarTests.Type(), [TeamCheckBarTests.Card("lead")]);

        Assert.False(check.CanStart);
        Assert.Equal(["Hire a Developer; the Team requires one."], check.Blockers);
    }

    /// <summary>A Team with no manager is blocked and the manager's title is named.</summary>
    [Fact]
    public void Compute_NoManager_BlocksAndNamesIt()
    {
        TeamTypeManifest type = TeamCheckBarTests.Type() with
        {
            Roles = [.. TeamCheckBarTests.Type().Roles.Select(static r => r.Id == "lead" ? r with { Required = false } : r)],
        };

        TeamCheck check = TeamCheckBar.Compute(type, [TeamCheckBarTests.Card("developer")]);

        Assert.False(check.CanStart);
        Assert.Equal(["Hire the manager (Lead)."], check.Blockers);
    }

    /// <summary>A role is full at its limit, not full under it, and never full when MaxPerTeam is 0.</summary>
    [Fact]
    public void IsRoleFull_LimitUnderAndUnlimited()
    {
        RoleSpec limited = new("a", "A", "c/a.md", MaxPerTeam: 2);
        RoleSpec unlimited = new("b", "B", "c/b.md", MaxPerTeam: 0);

        Assert.True(TeamCheckBar.IsRoleFull(limited, 2));
        Assert.True(TeamCheckBar.IsRoleFull(limited, 3));
        Assert.False(TeamCheckBar.IsRoleFull(limited, 1));
        Assert.False(TeamCheckBar.IsRoleFull(unlimited, 50));
    }
}

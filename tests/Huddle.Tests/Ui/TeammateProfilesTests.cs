using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="TeammateProfiles"/> turns a saved Teammate into the data a profile card draws: who is a
/// Team's manager, who is a member of it, and what state the status dot shows.
/// </summary>
public sealed class TeammateProfilesTests
{
    /// <summary>A name no Teammate has yields null, so raw pipe clients and demo agents get no popover.</summary>
    [Fact]
    public async Task BuildAsync_UnknownName_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);

        TeammateProfileView? view = await fixture.Profiles.BuildAsync("Nobody", null, ct);

        Assert.Null(view);
    }

    /// <summary>The card carries the Teammate's title, skills, the role's pool description and tier.</summary>
    [Fact]
    public async Task BuildAsync_Teammate_FillsCardFromPersonaAndRole()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead", withRole: true);

        TeammateProfileView? view = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.NotNull(view);
        Assert.Equal("Ada", view.Profile.Name);
        Assert.Equal("Lead", view.Profile.Title);
        Assert.Equal("Teaches.", view.Profile.Description);
        Assert.Equal(ModelTier.Balanced, view.Profile.Tier);
        Assert.Equal(["lint"], view.Profile.SkillNames);
        Assert.Empty(view.Profile.SetupReasons);
    }

    /// <summary>A Teammate with no role of its own has an empty description and no tier chip.</summary>
    [Fact]
    public async Task BuildAsync_NoRole_HasNoTierAndEmptyDescription()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Bo", "Helper");

        TeammateProfileView? view = await fixture.Profiles.BuildAsync("Bo", null, ct);

        Assert.NotNull(view);
        Assert.Null(view.Profile.Tier);
        Assert.Equal(string.Empty, view.Profile.Description);
    }

    /// <summary>The Manager flag is true only for the manager of the Team asked about.</summary>
    [Fact]
    public async Task BuildAsync_Manager_OnlyForThatTeamsManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead", "Garden");
        fixture.AddTeammate("Bo", "Dev", "Garden");
        fixture.AddTeam("Garden", "ada", "Ada", "Bo");

        TeammateProfileView? manager = await fixture.Profiles.BuildAsync("Ada", "Garden", ct);
        TeammateProfileView? other = await fixture.Profiles.BuildAsync("Bo", "Garden", ct);
        TeammateProfileView? noTeam = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.True(manager?.Manager);
        Assert.False(other?.Manager);
        Assert.False(noTeam?.Manager);
    }

    /// <summary>Membership is Member only for a Teammate in the Team asked about, and None otherwise.</summary>
    [Fact]
    public async Task BuildAsync_Membership_MemberOnlyWithATeamTheyAreIn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead", "Garden");
        fixture.AddTeammate("Cy", "Dev");
        fixture.AddTeam("Garden", null, "Ada");

        TeammateProfileView? member = await fixture.Profiles.BuildAsync("Ada", "Garden", ct);
        TeammateProfileView? outsider = await fixture.Profiles.BuildAsync("Cy", "Garden", ct);
        TeammateProfileView? noTeam = await fixture.Profiles.BuildAsync("Ada", null, ct);

        Assert.Equal(ProfileMembership.Member, member?.Membership);
        Assert.Equal(ProfileMembership.None, outsider?.Membership);
        Assert.Equal(ProfileMembership.None, noTeam?.Membership);
    }

    /// <summary>A Teammate that never connected has no agent id and the Offline state; one that is online shows Online.</summary>
    [Fact]
    public async Task BuildAsync_State_ResolvedFromGateway_AndAgentIdFromDirectory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProfileFixture fixture = new();
        await fixture.InitializeAsync(ct);
        fixture.AddTeammate("Ada", "Lead");
        fixture.AddTeammate("Bo", "Dev");
        User ada = await fixture.AddAgentAsync("Ada", ct);
        fixture.Gateway.SetOnline(ada.Id);

        TeammateProfileView? online = await fixture.Profiles.BuildAsync("Ada", null, ct);
        TeammateProfileView? never = await fixture.Profiles.BuildAsync("Bo", null, ct);

        Assert.Equal(PersonaState.Online, online?.Profile.State);
        Assert.Equal(ada.Id, online?.AgentId);
        Assert.Null(never?.AgentId);
        Assert.NotEqual(PersonaState.Online, never?.Profile.State);
    }
}

using Agency.Huddle.Tests.Factories;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.SkillPacks.Prompts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins SB-R08 (Spec §6.2 rule 4): the settings Huddle saves on its own (onboarding marker, avatar, manager,
/// gates) land in <c>_team.md</c> and never touch the Team's <c>AGENTS.md</c>. A real
/// <see cref="TeamFolderProvisioner"/> on a temporary data directory, byte comparison of the prose file.
/// The Process tab's switch-off is driven through the component in <c>AgentsMdUntouchedProcessTabTests</c>.
/// </summary>
public sealed class AgentsMdUntouchedTests
{
    private const string Team = "Web Team";

    private const string Prose = "# Web Team\r\n\r\n## Onboarding\r\n\r\n- Read the workspace.\r\n\r\n## Notes\r\n\r\nKeep it tidy.é\r\n";

    /// <summary>Completing onboarding writes <c>onboarded: true</c> to <c>_team.md</c> and leaves <c>AGENTS.md</c> byte-identical.</summary>
    [Fact]
    public async Task Onboarding_Completes_WritesTheMarkerNotTheProse()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Fixture f = new();
        FakeOnboardingPoster poster = new();
        TeamOnboarding onboarding = new(f.Folders, new RecordingPromptSource(), poster);

        bool posted = await onboarding.PostIfDueAsync(Team, "Nova", ct);

        Assert.True(posted);
        Assert.Equal("Nova", Assert.Single(poster.Posts).Manager);
        Assert.True(f.Folders.ReadTeamFile(Team)?.Onboarded);
        Assert.Equal(Prose, f.ReadProse());
    }

    /// <summary>Changing the avatar writes it to <c>_team.md</c> and leaves <c>AGENTS.md</c> byte-identical.</summary>
    [Fact]
    public void Avatar_Changes_WritesTheTeamFileNotTheProse()
    {
        using Fixture f = new();
        Avatar avatar = new(Label: "WT", Image: null, Background: "#336699");

        bool saved = f.Folders.UpdateTeamFile(Team, current => current with { Avatar = avatar });

        Assert.True(saved);
        Assert.Equal(avatar, f.Folders.ReadTeamFile(Team)?.Avatar);
        Assert.Equal(Prose, f.ReadProse());
    }

    /// <summary>Changing the manager writes it to <c>_team.md</c> and leaves <c>AGENTS.md</c> byte-identical.</summary>
    [Fact]
    public void Manager_Changes_WritesTheTeamFileNotTheProse()
    {
        using Fixture f = new();

        bool saved = f.Folders.UpdateTeamFile(Team, current => current with { Manager = "Ada" });

        Assert.True(saved);
        Assert.Equal("Ada", f.Folders.ReadTeamFile(Team)?.Manager);
        Assert.Equal(Prose, f.ReadProse());
    }

    /// <summary>Saving gates writes them to <c>_team.md</c> and leaves <c>AGENTS.md</c> byte-identical.</summary>
    [Fact]
    public void Gates_Saved_WritesTheTeamFileNotTheProse()
    {
        using Fixture f = new();

        bool saved = f.Folders.UpdateTeamFile(Team, current => current.WithGates([GateId.ChecksBeforeMerge]));

        Assert.True(saved);
        Assert.Equal([GateId.ChecksBeforeMerge], f.Folders.ReadTeamFile(Team)?.Process.Gates);
        Assert.Equal(Prose, f.ReadProse());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir dir = new();
        private readonly LibraryFileServiceFixture library;
        private readonly PersonaStore personas;
        private readonly string prosePath;

        public Fixture()
        {
            this.library = LibraryFileServiceFixture.Attach(this.dir.Path);
            this.personas = this.dir.CreatePersonaStore();
            this.Folders = new TeamFolderProvisioner(this.personas, this.library.RootStore, this.library.Resolver, NullLogger<TeamFolderProvisioner>.Instance, new FakeTeamCatalog());
            TeamFile file = new(new VersionedRef("web", "1.2.0"), "Nova", null, null, string.Empty);
            TeamCreateResult created = this.Folders.CreateTeam(Team, file);
            Assert.Equal(TeamCreateOutcome.Created, created.Outcome);
            this.prosePath = Path.Combine(Path.GetDirectoryName(created.Path) ?? string.Empty, TeamPaths.AgentsMdFileName);
            File.WriteAllText(this.prosePath, Prose);
        }

        public TeamFolderProvisioner Folders { get; }

        public string ReadProse() => File.ReadAllText(this.prosePath);

        public void Dispose()
        {
            this.personas.Dispose();
            this.library.Dispose();
            this.dir.Dispose();
        }
    }
}

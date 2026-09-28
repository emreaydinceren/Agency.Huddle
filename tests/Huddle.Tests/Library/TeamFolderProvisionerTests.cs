using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Pins Spec §6.2's trigger table for <see cref="TeamFolderProvisioner"/>: it creates a
/// <c>Teams/&lt;Label&gt;/</c> folder for every Persona <c>teams</c> label on start and on every
/// <see cref="PersonaStore.PersonasChanged"/>, never renames or deletes one, and exposes
/// <see cref="TeamFolderProvisioner.EnsureProject"/> for the Library Pane's "New Project" trigger
/// (corrections-B4 items 33-37).
/// </summary>
public sealed class TeamFolderProvisionerTests
{
    /// <summary>Starting the provisioner creates one folder per distinct Persona Team label.</summary>
    [Fact]
    public async Task Start_CreatesFolderForEveryLabel()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing", "Sales"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Sales")));
    }

    /// <summary>A Persona added after start (raising <see cref="PersonaStore.PersonasChanged"/> synchronously)
    /// gets its label's folder created without any watcher wait.</summary>
    [Fact]
    public async Task PersonasChanged_NewLabel_CreatesFolder()
    {
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Engineering"]), "body");

        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Engineering")));
    }

    /// <summary>Removing the only Persona carrying a label leaves that label's folder in place: the
    /// provisioner never renames or deletes (Spec §6.2).</summary>
    [Fact]
    public async Task LabelRemoved_FolderKept()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));

        fixture.Personas.Remove("Nova");

        Assert.DoesNotContain("Marketing", fixture.Personas.Teams);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
    }

    /// <summary>A label with a character invalid in a folder name (Spec §10 E-9) is skipped without
    /// throwing, and logs a warning; the ':' row runs on every OS (corrections-B4 item 37).</summary>
    [Fact]
    public async Task LabelWithInvalidFolderChars_Skipped_NoThrow()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Bad:Label"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Bad:Label")));
        Assert.Empty(Directory.GetDirectories(fixture.TeamsRoot));
    }

    /// <summary>Two labels differing only by case create exactly one folder (corrections-B4 item 37
    /// dedupe), asserted by folder count so the check holds on every OS.</summary>
    [Fact]
    public async Task TwoLabelsDifferByCase_OneFolder()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing", "marketing"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.Single(Directory.GetDirectories(fixture.TeamsRoot));
    }

    /// <summary>A folder that already exists under a different case is reused rather than duplicated
    /// (corrections-B4 item 37, the Linux case): the assertion is the folder count, which holds on
    /// every OS regardless of the file system's own case sensitivity.</summary>
    [Fact]
    public async Task ExistingCaseInsensitiveMatch_Reused_NoSecondFolder()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "marketing"));
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.Single(Directory.GetDirectories(fixture.TeamsRoot));
    }

    /// <summary>Creates the Project folder under an already-existing Team folder.</summary>
    [Fact]
    public async Task EnsureProject_Creates()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "Launch");

        Assert.NotNull(result.Value);
        Assert.Null(result.Error);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "Launch")));
    }

    /// <summary>An invalid Project name is refused with the §6.1 name-refusal text, and nothing is created.</summary>
    [Fact]
    public async Task EnsureProject_InvalidName_Refused()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "Bad:Name");

        Assert.Null(result.Value);
        Assert.Equal("A name can't contain :.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "Bad:Name")));
    }

    /// <summary>An underscore-prefixed Project name is refused as reserved, and nothing is created.</summary>
    [Fact]
    public async Task EnsureProject_UnderscorePrefix_Refused()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "_tasks");

        Assert.Null(result.Value);
        Assert.Equal("That folder is reserved.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "_tasks")));
    }

    /// <summary>Calling <see cref="TeamFolderProvisioner.EnsureProject"/> twice for the same name is
    /// idempotent: both calls succeed and only one folder exists.</summary>
    [Fact]
    public async Task EnsureProject_Twice_IsIdempotent()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> first = provisioner.EnsureProject(teamFolder!, "Launch");
        LibraryResult<LibraryPath> second = provisioner.EnsureProject(teamFolder!, "Launch");

        Assert.NotNull(first.Value);
        Assert.NotNull(second.Value);
        Assert.Single(Directory.GetDirectories(Path.Combine(fixture.TeamsRoot, "Marketing")));
    }

    /// <summary>A missing Team folder refuses <see cref="TeamFolderProvisioner.EnsureProject"/>: nothing
    /// is created (corrections-B4 item 36).</summary>
    [Fact]
    public async Task EnsureProject_MissingTeamFolder_Refused()
    {
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Ghost", out LibraryPath? teamFolder, out _));
        Assert.NotNull(teamFolder);
        Assert.False(Directory.Exists(teamFolder.FullPath));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder, "Launch");

        Assert.Null(result.Value);
        Assert.Equal("That Team folder doesn't exist yet.", result.Error);
        Assert.False(Directory.Exists(teamFolder.FullPath));
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Ghost", "Launch")));
    }

    /// <summary>A FILE already occupying the Project's name makes <see cref="Directory.CreateDirectory(string)"/>
    /// throw <see cref="IOException"/>: this is a UI call, so it refuses with the settled create-failure text
    /// instead of throwing, and leaves the file untouched with no folder created (7.2 fix card).</summary>
    [Fact]
    public async Task EnsureProject_FileWithProjectName_Refused()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        File.WriteAllText(Path.Combine(fixture.TeamsRoot, "Marketing", "Launch"), "not a folder");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "Launch");

        Assert.Null(result.Value);
        Assert.Equal("Couldn't create Launch.", result.Error);
        Assert.True(File.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "Launch")));
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "Launch")));
    }

    /// <summary>A tampered <see cref="LibraryPath.FullPath"/> is ignored: the folder is created at the
    /// RESOLVED location (root id plus relative path), never at the bogus one (R4 tampering row).</summary>
    [Fact]
    public async Task EnsureProject_TamperedPath_UsesResolvedPath()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));
        string bogusPath = Path.Combine(fixture.DataDir, "does-not-exist-anywhere");
        LibraryPath tampered = teamFolder! with { FullPath = bogusPath };

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(tampered, "Launch");

        Assert.NotNull(result.Value);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "Launch")));
        Assert.False(Directory.Exists(bogusPath));
    }

    /// <summary>An <see cref="IOException"/> raised while creating one label's folder (a same-named
    /// FILE already occupying that slot) is caught and logged; the Persona write still succeeds and
    /// every OTHER label's folder is still created (R3 self-review: every event callback catches).</summary>
    [Fact]
    public void PersonasChanged_IOExceptionForOneLabel_OtherLabelsStillCreated()
    {
        using ProvisionerFixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.TeamsRoot, "Blocked"), "not a folder");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        Persona persona = fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Blocked", "Marketing"]), "body");

        Assert.Equal("Nova", persona.Name);
        Assert.True(File.Exists(Path.Combine(fixture.TeamsRoot, "Blocked")));
        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Blocked")));
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
    }

    /// <summary>After <see cref="TeamFolderProvisioner.Dispose"/>, the provisioner no longer observes
    /// <see cref="PersonaStore.PersonasChanged"/>: a later Add creates no folder.</summary>
    [Fact]
    public void Dispose_Unsubscribes_LaterAddCreatesNothing()
    {
        using ProvisionerFixture fixture = new();
        TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        provisioner.Dispose();

        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Marketing"]), "body");

        Assert.False(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing")));
    }

    /// <summary>An isolated <see cref="PersonaStore"/>, <see cref="LibraryRootStore"/> and
    /// <see cref="LibraryPathResolver"/> over a fresh temp <c>DataDir</c>, with <c>Teams/</c> and
    /// <c>Teammates/</c> already created (<see cref="TeammateLayoutMigration"/>'s job, not this
    /// fixture's - see §6.2's remarks).</summary>
    private sealed class ProvisionerFixture : IDisposable
    {
        private readonly TempDataDir dir;

        public ProvisionerFixture()
        {
            this.dir = new TempDataDir();
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teammates"));
            IOptions<TeamOptions> options = this.dir.Options();
            TeammatePaths paths = new(options);
            this.Personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);
            this.Roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.Roots, options, NullLogger<LibraryPathResolver>.Instance);
        }

        public PersonaStore Personas { get; }

        public LibraryRootStore Roots { get; }

        public LibraryPathResolver Resolver { get; }

        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        public string DataDir => this.dir.Path;

        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.Personas, this.Roots, this.Resolver, NullLogger<TeamFolderProvisioner>.Instance);

        public void Dispose()
        {
            this.Personas.Dispose();
            this.dir.Dispose();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Ui;

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

    /// <summary>Spec §6.3: a Project named <c>memory</c> (any case) is refused with the reserved-Memory text and nothing at all is created under the Team folder.</summary>
    [Theory]
    [InlineData("memory")]
    [InlineData("MEMORY")]
    public void EnsureProject_Memory_RefusedAndNothingCreated(string project)
    {
        using ProvisionerFixture fixture = new();
        string teamDir = Path.Combine(fixture.TeamsRoot, "Marketing");
        Directory.CreateDirectory(teamDir);
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, project);

        Assert.Null(result.Value);
        Assert.Equal("\"memory\" is reserved for the Team's shared Memory.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(teamDir));
    }

    /// <summary>Guard (passes today, corrections-D3 item 9): <c>_x</c> keeps the resolver's own refusal text, not the memory text, and nothing is created.</summary>
    [Fact]
    public void EnsureProject_UnderscoreX_StillRefusedAsReserved()
    {
        using ProvisionerFixture fixture = new();
        string teamDir = Path.Combine(fixture.TeamsRoot, "Marketing");
        Directory.CreateDirectory(teamDir);
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "_x");

        Assert.Null(result.Value);
        Assert.Equal("That folder is reserved.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(teamDir));
    }

    /// <summary>Guard (passes today): <c>memory-notes</c> is a legal Project name and its folder is created.</summary>
    [Fact]
    public void EnsureProject_MemoryNotes_Creates()
    {
        using ProvisionerFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Marketing"));
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.True(fixture.Resolver.TryResolve("teams", "Marketing", out LibraryPath? teamFolder, out _));

        LibraryResult<LibraryPath> result = provisioner.EnsureProject(teamFolder!, "memory-notes");

        Assert.NotNull(result.Value);
        Assert.Null(result.Error);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Marketing", "memory-notes")));
    }

    /// <summary>Guard (passes today): a Persona Team LABEL <c>memory</c> still gets its <c>Teams/memory/</c> folder on start, because a Team named memory is legal (Spec §6.3 restricts Projects only).</summary>
    [Fact]
    public async Task Start_LabelMemory_StillCreatesTeamFolder()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["memory"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(fixture.TeamsRoot, "memory"), Assert.Single(Directory.GetDirectories(fixture.TeamsRoot)));
    }

    /// <summary>Guard (passes today): a Persona added AFTER start with label <c>memory</c> also gets its Team folder (the <see cref="PersonaStore.PersonasChanged"/> path into the same sync).</summary>
    [Fact]
    public async Task PersonasChanged_LabelMemory_StillCreatesTeamFolder()
    {
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        await provisioner.StartAsync(TestContext.Current.CancellationToken);

        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["memory"]), "body");

        Assert.Equal(Path.Combine(fixture.TeamsRoot, "memory"), Assert.Single(Directory.GetDirectories(fixture.TeamsRoot)));
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

    /// <summary><see cref="TeamFolderProvisioner"/> is an <c>ITeamFolders</c>: the seam the Team pages
    /// call to create Teams and Projects (Spec §6.5, plan item S8).</summary>
    [Fact]
    public void Provisioner_IsAnITeamFolders()
    {
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        ITeamFolders folders = Assert.IsAssignableFrom<ITeamFolders>(provisioner);

        Assert.Same(provisioner, folders);
    }

    /// <summary>The DI container resolves <c>ITeamFolders</c> to the same singleton as
    /// <see cref="TeamFolderProvisioner"/> (the hosted service), so the pages and the start-up sync share one instance.</summary>
    [Fact]
    public async Task Resolve_ITeamFolders_ReturnsTheSameInstanceAsTheProvisioner()
    {
        await using TeamWebApplicationFactory factory = new();

        ITeamFolders byInterface = factory.Services.GetRequiredService<ITeamFolders>();
        TeamFolderProvisioner concrete = factory.Services.GetRequiredService<TeamFolderProvisioner>();

        Assert.Same(concrete, byInterface);
    }

    /// <summary>Spec §6.5: <c>EnsureTeam</c> creates <c>Teams/&lt;Name&gt;/</c>, returns its
    /// <see cref="LibraryPath"/> with the <see cref="LibraryNodeRole.TeamFolder"/> role, and the live
    /// catalog then lists the Team with <c>HasFolder</c> true (polled: the TaskStore rebuild lags ~500 ms).</summary>
    [Fact]
    public async Task EnsureTeam_CreatesTheFolder_AndReturnsATeamFolderPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam("Ops");

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(LibraryNodeRole.TeamFolder, result.Value.Role);
        Assert.Equal("teams", result.Value.Root.Id);
        Assert.Equal("Ops", result.Value.RelativePath);
        Assert.True(Directory.Exists(result.Value.FullPath));
        Assert.Equal(["Ops"], EntryNames(fixture.TeamsRoot));
        await WaitForAsync(() => fixture.Catalog.Find("Ops")?.HasFolder == true, ct);
        Assert.True(fixture.Catalog.Find("Ops")?.HasFolder);
    }

    /// <summary>Each name the Team rules refuse returns that rule's text and creates nothing: the
    /// Library's own problem for <c>a:b</c>, <c>Sales/EMEA</c> and a blank; the underscore text for
    /// <c>_x</c> and <c>.x</c>; the commas text for <c>Sales, EMEA</c>.</summary>
    /// <param name="name">The refused Team name.</param>
    /// <param name="expected">The exact refusal text.</param>
    [Theory]
    [InlineData("a:b", "A name can't contain :.")]
    [InlineData("Sales/EMEA", "A name can't contain /.")]
    [InlineData(" ", "A name can't be empty.")]
    [InlineData("_x", "Names starting with \"_\" or \".\" are reserved.")]
    [InlineData(".x", "Names starting with \"_\" or \".\" are reserved.")]
    [InlineData("Sales, EMEA", "A Team name can't contain commas, semicolons or square brackets.")]
    public void EnsureTeam_RefusedName_ReturnsTheRuleText_AndCreatesNothing(string name, string expected)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(expected);
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam(name);

        Assert.Null(result.Value);
        Assert.Equal(expected, result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.TeamsRoot));
    }

    /// <summary>The neighbours of the refused names are allowed and create their folder: <c>memory</c>
    /// is a legal Team name (Spec §6.3 restricts Projects only), so is <c>a_b</c> (only a LEADING
    /// underscore is reserved), and so is a name with a space.</summary>
    /// <param name="name">The allowed Team name.</param>
    [Theory]
    [InlineData("memory")]
    [InlineData("a_b")]
    [InlineData("Sales EMEA")]
    public void EnsureTeam_AllowedNeighbour_CreatesTheFolder(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam(name);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(LibraryNodeRole.TeamFolder, result.Value.Role);
        Assert.Equal(name, result.Value.RelativePath);
        Assert.Equal([name], EntryNames(fixture.TeamsRoot));
    }

    /// <summary>A name differing only by case from a Team folder the catalog already lists is refused
    /// with the duplicate text (it names the typed spelling) and no second folder appears.</summary>
    [Fact]
    public void EnsureTeam_ExistingFolderDifferingByCase_IsRefused()
    {
        using ProvisionerFixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam("business");

        Assert.Null(result.Value);
        Assert.Equal("A Team named \"business\" already exists.", result.Error);
        Assert.Equal(["Business"], EntryNames(fixture.TeamsRoot));
    }

    /// <summary>A folder created on disk AFTER the catalog's snapshot (which lags ~500 ms) is still
    /// found: <c>EnsureTeam</c> checks the disk itself (the <c>SyncFolders</c> pattern), so a
    /// differently-cased name is refused rather than creating a second folder on a case-sensitive file system.</summary>
    [Fact]
    public void EnsureTeam_FolderNewerThanTheCatalogSnapshot_IsRefused()
    {
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Directory.CreateDirectory(Path.Combine(fixture.TeamsRoot, "Business"));

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam("business");

        Assert.Null(result.Value);
        Assert.Equal("A Team named \"business\" already exists.", result.Error);
        Assert.Equal(["Business"], EntryNames(fixture.TeamsRoot));
    }

    /// <summary>A Team that exists only as a Persona label (no folder yet) counts as existing: a
    /// differently-cased <c>EnsureTeam</c> is refused and creates nothing. The Persona is added BEFORE the
    /// provisioner exists and <see cref="TeamFolderProvisioner.StartAsync"/> is never called (a live
    /// provisioner would create the label's folder itself, corrections-D6 item 16).</summary>
    [Fact]
    public void EnsureTeam_LabelOnlyTeamDifferingByCase_IsRefused()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Ops"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.False(fixture.Catalog.Find("Ops")?.HasFolder);

        LibraryResult<LibraryPath> result = provisioner.EnsureTeam("OPS");

        Assert.Null(result.Value);
        Assert.Equal("A Team named \"OPS\" already exists.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.TeamsRoot));
    }

    /// <summary>Back to back, <c>EnsureTeam("Ops")</c> then <c>EnsureTeam("ops")</c>: the catalog has not
    /// yet noticed the first folder, yet the second call is refused, so exactly one folder exists (on a
    /// case-sensitive file system a catalog-only check would create two).</summary>
    [Fact]
    public void EnsureTeam_TwiceDifferingByCase_SecondIsRefused()
    {
        using ProvisionerFixture fixture = new();
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> first = provisioner.EnsureTeam("Ops");
        LibraryResult<LibraryPath> second = provisioner.EnsureTeam("ops");

        Assert.Null(first.Error);
        Assert.Null(second.Value);
        Assert.Equal("A Team named \"ops\" already exists.", second.Error);
        Assert.Equal(["Ops"], EntryNames(fixture.TeamsRoot));
    }

    /// <summary><c>EnsureProjectIn</c> creates <c>Teams/&lt;Team&gt;/&lt;Project&gt;/</c> for a Team
    /// the catalog lists with a folder, and returns the <see cref="LibraryNodeRole.ProjectFolder"/> path.</summary>
    [Fact]
    public void EnsureProjectIn_ExistingTeam_CreatesTheProject()
    {
        using ProvisionerFixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Business", "Launch");

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(LibraryNodeRole.ProjectFolder, result.Value.Role);
        Assert.Equal("Business/Launch", result.Value.RelativePath);
        Assert.True(Directory.Exists(Path.Combine(fixture.TeamsRoot, "Business", "Launch")));
        Assert.Equal(["Launch"], EntryNames(Path.Combine(fixture.TeamsRoot, "Business")));
    }

    /// <summary>A Team that exists only as a Persona label gets its Team folder created first, then the
    /// Project inside it (not through <c>EnsureTeam</c>, whose duplicate check would refuse the label).
    /// The Persona is added before the provisioner exists and <c>StartAsync</c> is never called, so the
    /// folder is absent until this call (corrections-D6 item 16).</summary>
    [Fact]
    public void EnsureProjectIn_LabelOnlyTeam_CreatesTheTeamFolderFirst()
    {
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", ["Ops"]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();
        Assert.False(fixture.Catalog.Find("Ops")?.HasFolder);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.TeamsRoot));

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Ops", "Launch");

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(LibraryNodeRole.ProjectFolder, result.Value.Role);
        Assert.Equal("Ops/Launch", result.Value.RelativePath);
        Assert.Equal(["Ops"], EntryNames(fixture.TeamsRoot));
        Assert.Equal(["Launch"], EntryNames(Path.Combine(fixture.TeamsRoot, "Ops")));
    }

    /// <summary>A label-only Team whose label the Library cannot hold as a folder name is refused with
    /// the Library's problem and NOTHING is created: <c>R&amp;D/Legal</c> would otherwise be split on
    /// <c>/</c> by the resolver (corrections-D6 item 18), and <c>_Ops</c> is a reserved folder name
    /// (the reserved text is provisional: the same "That folder is reserved." as the start-up sync).</summary>
    /// <param name="label">The Persona Team label.</param>
    /// <param name="expected">The exact refusal text.</param>
    [Theory]
    [InlineData("R&D/Legal", "A name can't contain /.")]
    [InlineData("_Ops", "That folder is reserved.")]
    public void EnsureProjectIn_LabelOnlyTeamWithUnusableLabel_IsRefused_AndCreatesNothing(string label, string expected)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(expected);
        using ProvisionerFixture fixture = new();
        fixture.Personas.Add(new PersonaIdentity("Nova", "Nova", "Nova", [label]), "body");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn(label, "Launch");

        Assert.Null(result.Value);
        Assert.Equal(expected, result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.TeamsRoot));
    }

    /// <summary>A Project name the Project rules refuse returns that rule's text and creates nothing:
    /// <c>memory</c> in any case (the reserved-Memory text), <c>_x</c> (the underscore text), and the
    /// Library's problem for <c>Bad:Name</c> and <c>Sub/Folder</c>.</summary>
    /// <param name="project">The refused Project name.</param>
    /// <param name="expected">The exact refusal text.</param>
    [Theory]
    [InlineData("memory", "\"memory\" is reserved for the Team's shared Memory.")]
    [InlineData("MEMORY", "\"memory\" is reserved for the Team's shared Memory.")]
    [InlineData("_x", "Names starting with \"_\" or \".\" are reserved.")]
    [InlineData("Bad:Name", "A name can't contain :.")]
    [InlineData("Sub/Folder", "A name can't contain /.")]
    public void EnsureProjectIn_RefusedProjectName_ReturnsTheRuleText_AndCreatesNothing(string project, string expected)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(expected);
        using ProvisionerFixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Business", project);

        Assert.Null(result.Value);
        Assert.Equal(expected, result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(fixture.TeamsRoot, "Business")));
    }

    /// <summary>The neighbours of the refused Project names are allowed and create their folder:
    /// <c>memory-notes</c> and <c>x_</c> (only a LEADING underscore, or exactly <c>memory</c>, is reserved).</summary>
    /// <param name="project">The allowed Project name.</param>
    [Theory]
    [InlineData("memory-notes")]
    [InlineData("x_")]
    public void EnsureProjectIn_AllowedNeighbour_CreatesTheProject(string project)
    {
        ArgumentNullException.ThrowIfNull(project);
        using ProvisionerFixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Business", project);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal([project], EntryNames(Path.Combine(fixture.TeamsRoot, "Business")));
    }

    /// <summary>A Team the catalog does not know is refused with the exact text and nothing is created.</summary>
    [Fact]
    public void EnsureProjectIn_UnknownTeam_ReturnsThereIsNoTeamNamed()
    {
        using ProvisionerFixture fixture = new("Business");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("Ghost", "Launch");

        Assert.Null(result.Value);
        Assert.Equal("There is no Team named \"Ghost\".", result.Error);
        Assert.Equal(["Business"], EntryNames(fixture.TeamsRoot));
    }

    /// <summary>A Project whose name differs only by case from an existing one is refused with the
    /// duplicate text (Team shown in its display spelling) and no second folder appears. The Team is
    /// looked up ignoring case too (<c>business</c>).</summary>
    [Fact]
    public void EnsureProjectIn_DuplicateProjectIgnoringCase_IsRefused()
    {
        using ProvisionerFixture fixture = new("Business/Launch");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> result = provisioner.EnsureProjectIn("business", "launch");

        Assert.Null(result.Value);
        Assert.Equal("A Project named \"launch\" already exists in Business.", result.Error);
        Assert.Equal(["Launch"], EntryNames(Path.Combine(fixture.TeamsRoot, "Business")));
    }

    /// <summary>Creating never renames or deletes: a pre-existing <c>Teams/Business/keep.md</c> and the
    /// Team folder itself survive a created Team, a created Project and a refused duplicate Team.</summary>
    [Fact]
    public void Ensure_NeverRenamesOrDeletes_AnExistingFile()
    {
        using ProvisionerFixture fixture = new("Business");
        string keep = Path.Combine(fixture.TeamsRoot, "Business", "keep.md");
        File.WriteAllText(keep, "notes");
        using TeamFolderProvisioner provisioner = fixture.CreateProvisioner();

        LibraryResult<LibraryPath> team = provisioner.EnsureTeam("Ops");
        LibraryResult<LibraryPath> project = provisioner.EnsureProjectIn("Business", "Launch");
        LibraryResult<LibraryPath> duplicate = provisioner.EnsureTeam("business");

        Assert.Null(team.Error);
        Assert.Null(project.Error);
        Assert.NotNull(duplicate.Error);
        Assert.Equal("notes", File.ReadAllText(keep));
        Assert.Equal(["Business", "Ops"], EntryNames(fixture.TeamsRoot));
        Assert.Equal(["Launch", "keep.md"], EntryNames(Path.Combine(fixture.TeamsRoot, "Business")));
    }

    /// <summary>Lists a directory's entry names (files and folders), ordinal-sorted, so a whole listing can be asserted.</summary>
    /// <param name="directory">The directory to list.</param>
    private static string[] EntryNames(string directory) =>
        [.. Directory.GetFileSystemEntries(directory).Select(static entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Polls <paramref name="condition"/> until it is true or a generous timeout elapses, for
    /// asserting on the catalog's watcher-driven rebuild without a bare delay.
    /// </summary>
    /// <param name="condition">Checked repeatedly until it returns <see langword="true"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the catalog to pick up the change.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    /// <summary>An isolated <see cref="PersonaStore"/>, <see cref="LibraryRootStore"/> and
    /// <see cref="LibraryPathResolver"/> over a fresh temp <c>DataDir</c>, with <c>Teams/</c> and
    /// <c>Teammates/</c> already created (<see cref="TeammateLayoutMigration"/>'s job, not this
    /// fixture's - see §6.2's remarks).</summary>
    private sealed class ProvisionerFixture : IDisposable
    {
        private readonly TempDataDir dir;

        /// <param name="seedFolders">Folders (relative to <c>Teams/</c>, e.g. <c>Business</c> or
        /// <c>Business/Launch</c>) created BEFORE the <see cref="TaskStore"/> is built, so the
        /// <see cref="TeamCatalog"/> lists them from its first snapshot instead of after the ~500 ms
        /// rebuild.</param>
        public ProvisionerFixture(params string[] seedFolders)
        {
            ArgumentNullException.ThrowIfNull(seedFolders);

            this.dir = new TempDataDir();
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teammates"));
            foreach (string seed in seedFolders)
            {
                Directory.CreateDirectory(Path.Combine(this.dir.Path, "Teams", seed));
            }

            IOptions<TeamOptions> options = this.dir.Options();
            TeammatePaths paths = new(options);
            this.Personas = new PersonaStore(paths, new PersonaModelStore(options), new PersonaEffortStore(options), NullLogger<PersonaStore>.Instance);
            this.Roots = new LibraryRootStore(options, paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.Roots, options, NullLogger<LibraryPathResolver>.Instance);
            this.Tasks = TestTaskStore.CreateTaskStore(this.dir, this.Personas);
            this.Catalog = new TeamCatalog(this.Personas, this.Tasks);
        }

        public TaskStore Tasks { get; }

        public TeamCatalog Catalog { get; }

        public PersonaStore Personas { get; }

        public LibraryRootStore Roots { get; }

        public LibraryPathResolver Resolver { get; }

        public string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

        public string DataDir => this.dir.Path;

        public TeamFolderProvisioner CreateProvisioner() =>
            new(this.Personas, this.Roots, this.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: this.Catalog);

        public void Dispose()
        {
            this.Catalog.Dispose();
            this.Tasks.Dispose();
            this.Personas.Dispose();
            this.dir.Dispose();
        }
    }
}

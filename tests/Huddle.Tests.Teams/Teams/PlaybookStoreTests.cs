using System.Security.Cryptography;
using System.Text;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins <see cref="PlaybookStore"/> (Team Playbooks spec §3.3, §5.1, §5.2): Customize, Keep my version and Reset on a Team's Playbook
/// files, the state function, the diff from the base to the current default, and the startup pruning of unnamed bases.
/// </summary>
public sealed class PlaybookStoreTests : IDisposable
{
    private const string MemberKey = "systemPrompt.memberPlaybook";

    private const string ManagerKey = "systemPrompt.managerPlaybook";

    private const string DefaultV1 = "## How to work\n\nKeep passes short.\n";

    private readonly string sandbox = Path.Combine(Path.GetTempPath(), "huddle-playbook-store-" + Guid.NewGuid().ToString("N"));

    private readonly string teamsRoot;

    private readonly string dataDir;

    private readonly FakePromptSource prompts = new();

    private readonly PlaybookStore store;

    private readonly PlaybookBaseStore bases;

    /// <summary>Creates the Teams root with the Team folders <c>Sales</c> and <c>Ops</c>, and a store whose Member default is <see cref="DefaultV1"/>.</summary>
    public PlaybookStoreTests()
    {
        this.teamsRoot = Path.Combine(this.sandbox, "Teams");
        this.dataDir = Path.Combine(this.sandbox, "Data");
        _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, "Sales"));
        _ = Directory.CreateDirectory(Path.Combine(this.teamsRoot, "Ops"));
        this.prompts.SetOverride(MemberKey, DefaultV1);
        this.prompts.SetOverride(ManagerKey, "## Run the Team\n\nTriage first.\n");
        this.bases = new PlaybookBaseStore(this.dataDir);
        this.store = new PlaybookStore(this.teamsRoot, this.bases, this.prompts, new PlaybookOptions { MaxChars = 100 });
    }

    /// <summary>Removes the sandbox.</summary>
    public void Dispose() => Directory.Delete(this.sandbox, recursive: true);

    /// <summary>With no file the state is Default: the effective default is the text, and there is no diff and no over-limit length.</summary>
    [Fact]
    public void GetState_WithNoFile_IsDefault()
    {
        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);

        Assert.Equal(PlaybookStatus.Default, state.Status);
        Assert.Equal(DefaultV1, state.Text);
        Assert.Null(state.Diff);
        Assert.Equal(0, state.OverLimitLength);
        Assert.Equal(Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md"), state.Path);
    }

    /// <summary>Customize writes the effective default as the body and the hash of it as <c>basedOn</c>, in the file of the kind.</summary>
    /// <param name="manager">Whether the Manager Playbook is customized.</param>
    /// <param name="fileName">The file written.</param>
    [Theory]
    [InlineData(false, "_member-playbook.md")]
    [InlineData(true, "_manager-playbook.md")]
    public async Task CustomizeAsync_WritesTheBodyAndBasedOn_AndStoresTheBase(bool manager, string fileName)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybookKind kind = manager ? PlaybookKind.Manager : PlaybookKind.Member;
        string effective = this.prompts.Raw(manager ? ManagerKey : MemberKey);

        bool written = await this.store.CustomizeAsync("Sales", kind, ct);

        Assert.True(written);
        PlaybookFile file = PlaybookFile.Parse(await File.ReadAllTextAsync(Path.Combine(this.teamsRoot, "Sales", fileName), ct));
        Assert.Equal("sha256:" + ExpectedHash(effective), file.BasedOn);
        Assert.Equal(effective, file.Body);
        Assert.Equal(effective, this.bases.Read(kind, ExpectedHash(effective)));
        Assert.Equal(PlaybookStatus.Customized, this.store.GetState("Sales", kind).Status);
    }

    /// <summary>The hash is of the LF-normalised, trimmed default: a default with CRLF and blank space around it hashes as the plain one.</summary>
    [Fact]
    public async Task CustomizeAsync_HashesTheLfNormalisedTrimmedDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.prompts.SetOverride(MemberKey, "\r\n## Rule\r\n\r\nBe brief.\r\n\r\n");

        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);

        PlaybookFile file = PlaybookFile.Parse(await File.ReadAllTextAsync(Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md"), ct));
        Assert.Equal("sha256:" + ExpectedHash("## Rule\n\nBe brief."), file.BasedOn);
    }

    /// <summary>A file that already exists is never overwritten: Customize reports it and leaves the bytes alone.</summary>
    [Fact]
    public async Task CustomizeAsync_WithAnExistingFile_NeverOverwritesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md");
        await File.WriteAllTextAsync(path, "My own rules.\n", ct);

        bool written = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);

        Assert.False(written);
        Assert.Equal("My own rules.\n", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>Keep my version restamps <c>basedOn</c> to the current default and stores its base, and leaves the body and the unknown lines as they were.</summary>
    [Fact]
    public async Task KeepMyVersionAsync_ChangesOnlyBasedOn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md");
        PlaybookFile mine = new("sha256:0000000000000000", "My edited body.\n\nSecond paragraph.\n") { UnknownLines = ["owner: Dana"] };
        await File.WriteAllTextAsync(path, mine.Compose(), ct);

        await this.store.KeepMyVersionAsync("Sales", PlaybookKind.Member, ct);

        PlaybookFile after = PlaybookFile.Parse(await File.ReadAllTextAsync(path, ct));
        Assert.Equal("sha256:" + ExpectedHash(DefaultV1), after.BasedOn);
        Assert.Equal("My edited body.\n\nSecond paragraph.\n", after.Body);
        Assert.Equal(["owner: Dana"], after.UnknownLines);
        Assert.Equal(DefaultV1, this.bases.Read(PlaybookKind.Member, ExpectedHash(DefaultV1)));
        Assert.Equal(PlaybookStatus.Customized, this.store.GetState("Sales", PlaybookKind.Member).Status);
    }

    /// <summary>Keep my version on a hand-made file with no frontmatter stamps a base and keeps the body: the Unknown base state ends.</summary>
    [Fact]
    public async Task KeepMyVersionAsync_OnAFileWithNoBase_StampsTheCurrentDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md"), "Hand made.\n", ct);
        Assert.Equal(PlaybookStatus.UnknownBase, this.store.GetState("Sales", PlaybookKind.Member).Status);

        await this.store.KeepMyVersionAsync("Sales", PlaybookKind.Member, ct);

        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);
        Assert.Equal(PlaybookStatus.Customized, state.Status);
        Assert.Equal("Hand made.\n", state.Text);
    }

    /// <summary>Reset deletes the file, and the state is Default again.</summary>
    [Fact]
    public async Task ResetAsync_DeletesTheFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md");
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);

        await this.store.ResetAsync("Sales", PlaybookKind.Member, ct);

        Assert.False(File.Exists(path));
        Assert.Equal(PlaybookStatus.Default, this.store.GetState("Sales", PlaybookKind.Member).Status);
    }

    /// <summary>Reset touches only the file of its kind and its Team.</summary>
    [Fact]
    public async Task ResetAsync_LeavesTheOtherKindAndTheOtherTeamAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Manager, ct);
        _ = await this.store.CustomizeAsync("Ops", PlaybookKind.Member, ct);

        await this.store.ResetAsync("Sales", PlaybookKind.Member, ct);

        Assert.True(File.Exists(Path.Combine(this.teamsRoot, "Sales", "_manager-playbook.md")));
        Assert.True(File.Exists(Path.Combine(this.teamsRoot, "Ops", "_member-playbook.md")));
    }

    /// <summary>Reset with no file is not an error.</summary>
    [Fact]
    public async Task ResetAsync_WithNoFile_DoesNothing()
    {
        await this.store.ResetAsync("Sales", PlaybookKind.Member, TestContext.Current.CancellationToken);

        Assert.Equal(PlaybookStatus.Default, this.store.GetState("Sales", PlaybookKind.Member).Status);
    }

    /// <summary>A file whose <c>basedOn</c> is the hash of the effective default is Customized and shows its own body.</summary>
    [Fact]
    public async Task GetState_FileBasedOnTheEffectiveDefault_IsCustomized()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        PlaybookFile file = new("sha256:" + ExpectedHash(DefaultV1), "Our edit.\n");
        await File.WriteAllTextAsync(Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md"), file.Compose(), ct);

        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);

        Assert.Equal(PlaybookStatus.Customized, state.Status);
        Assert.Equal("Our edit.\n", state.Text);
        Assert.Null(state.Diff);
    }

    /// <summary>A file whose <c>basedOn</c> differs from the effective default's hash is Default updated and still shows its own body, never the new default.</summary>
    [Fact]
    public async Task GetState_AfterTheDefaultChanged_IsDefaultUpdated_WithTheFilesBody()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        this.prompts.SetOverride(MemberKey, "## How to work\n\nKeep passes long.\n");

        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);

        Assert.Equal(PlaybookStatus.DefaultUpdated, state.Status);
        Assert.Equal(DefaultV1, state.Text);
    }

    /// <summary>The diff of Default updated runs from the base (the default the file started from) to the current default.</summary>
    [Fact]
    public async Task GetState_DefaultUpdated_CarriesTheDiffFromTheBaseToTheCurrentDefault()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        this.prompts.SetOverride(MemberKey, "## How to work\n\nKeep passes long.\n");

        string? diff = this.store.GetState("Sales", PlaybookKind.Member).Diff;

        Assert.NotNull(diff);
        Assert.Contains("-Keep passes short.", diff, StringComparison.Ordinal); // contains-ok: the store wires the diff; PlaybookDiffTests pins its whole shape
        Assert.Contains("+Keep passes long.", diff, StringComparison.Ordinal); // contains-ok: the store wires the diff; PlaybookDiffTests pins its whole shape
    }

    /// <summary>With no base file the state is still Default updated, but there is no diff.</summary>
    [Fact]
    public async Task GetState_DefaultUpdated_WithNoBaseFile_HasNoDiff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        File.Delete(this.bases.PathFor(PlaybookKind.Member, ExpectedHash(DefaultV1)));
        this.prompts.SetOverride(MemberKey, "## How to work\n\nKeep passes long.\n");

        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);

        Assert.Equal(PlaybookStatus.DefaultUpdated, state.Status);
        Assert.Null(state.Diff);
    }

    /// <summary>Keep my version after a default change ends Default updated.</summary>
    [Fact]
    public async Task GetState_AfterKeepMyVersion_IsCustomizedAgain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        this.prompts.SetOverride(MemberKey, "## How to work\n\nKeep passes long.\n");

        await this.store.KeepMyVersionAsync("Sales", PlaybookKind.Member, ct);

        Assert.Equal(PlaybookStatus.Customized, this.store.GetState("Sales", PlaybookKind.Member).Status);
    }

    /// <summary>A file with no frontmatter, or a <c>basedOn</c> that is not a hash, is Unknown base and shows its body.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="body">The body the state shows.</param>
    [Theory]
    [InlineData("Made by hand.\n", "Made by hand.\n")]
    [InlineData("---\nbasedOn: 'banana'\n---\nBad stamp.\n", "Bad stamp.\n")]
    [InlineData("---\nowner: Dana\n---\nNo stamp.\n", "No stamp.\n")]
    public async Task GetState_WithNoReadableBasedOn_IsUnknownBase(string text, string body)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md"), text, ct);

        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);

        Assert.Equal(PlaybookStatus.UnknownBase, state.Status);
        Assert.Equal(body, state.Text);
        Assert.Null(state.Diff);
    }

    /// <summary>
    /// Windows only (a lock on a file is a Windows feature): a file that exists and cannot be read is Unreadable, and the text is the
    /// effective default.
    /// </summary>
    [Fact]
    public void GetState_OfAFileThatCannotBeRead_IsUnreadable_WithTheDefaultText()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows can lock a file against reading.");
        string path = Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md");
        File.WriteAllText(path, "Locked.\n");
        using FileStream hold = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        PlaybookState state = this.store.GetState("Sales", PlaybookKind.Member);

        Assert.Equal(PlaybookStatus.Unreadable, state.Status);
        Assert.Equal(DefaultV1, state.Text);
        Assert.Equal(path, state.Path);
        Assert.Null(state.Diff);
    }

    /// <summary>A body longer than <c>MaxChars</c> reports its length, and one at or under it reports none.</summary>
    [Fact]
    public async Task GetState_OverTheLimit_ReportsTheBodyLength()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string path = Path.Combine(this.teamsRoot, "Sales", "_member-playbook.md");
        await File.WriteAllTextAsync(path, new string('x', 101), ct);
        Assert.Equal(101, this.store.GetState("Sales", PlaybookKind.Member).OverLimitLength);

        await File.WriteAllTextAsync(path, new string('x', 100), ct);

        Assert.Equal(0, this.store.GetState("Sales", PlaybookKind.Member).OverLimitLength);
    }

    /// <summary>Startup prunes a base that no customized file names, and keeps one that a file names, whichever Team and kind.</summary>
    [Fact]
    public async Task PruneBases_DeletesAnUnnamedBase_AndKeepsANamedOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        string named = this.bases.PathFor(PlaybookKind.Member, ExpectedHash(DefaultV1));
        this.prompts.SetOverride(MemberKey, "A later default.\n");
        _ = await this.store.CustomizeAsync("Ops", PlaybookKind.Member, ct);
        string unnamed = this.bases.PathFor(PlaybookKind.Member, ExpectedHash("A later default.\n"));
        await this.store.ResetAsync("Ops", PlaybookKind.Member, ct);
        Assert.True(File.Exists(unnamed));

        this.store.PruneBases();

        Assert.True(File.Exists(named));
        Assert.False(File.Exists(unnamed));
    }

    /// <summary>A base is named by the kind too: a Manager file naming a hash does not keep a Member base of the same hash.</summary>
    [Fact]
    public async Task PruneBases_KeepsABaseOnlyForTheKindThatNamesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string text = "Same default for both.\n";
        this.prompts.SetOverride(MemberKey, text);
        this.prompts.SetOverride(ManagerKey, text);
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Manager, ct);
        await this.store.ResetAsync("Sales", PlaybookKind.Member, ct);

        this.store.PruneBases();

        Assert.False(File.Exists(this.bases.PathFor(PlaybookKind.Member, ExpectedHash(text))));
        Assert.True(File.Exists(this.bases.PathFor(PlaybookKind.Manager, ExpectedHash(text))));
    }

    /// <summary>With no base folder at all, pruning is not an error.</summary>
    [Fact]
    public void PruneBases_WithNoBaseFolder_DoesNothing()
    {
        this.store.PruneBases();

        Assert.False(Directory.Exists(Path.Combine(this.dataDir, "playbook-defaults")));
    }

    /// <summary>Each write (Customize, Keep my version, Reset) raises <see cref="PlaybookStore.Changed"/> once.</summary>
    [Fact]
    public async Task Changed_IsRaisedOnEachWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int raised = 0;
        this.store.Changed += () => raised++;

        _ = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);
        Assert.Equal(1, raised);

        await this.store.KeepMyVersionAsync("Sales", PlaybookKind.Member, ct);
        Assert.Equal(2, raised);

        await this.store.ResetAsync("Sales", PlaybookKind.Member, ct);
        Assert.Equal(3, raised);
    }

    /// <summary>A subscriber that throws does not fail the write nor stop the other subscribers (HUD0001).</summary>
    [Fact]
    public async Task Changed_AThrowingSubscriber_DoesNotFailTheWrite()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int raised = 0;
        this.store.Changed += () => throw new InvalidOperationException("subscriber failed");
        this.store.Changed += () => raised++;

        bool written = await this.store.CustomizeAsync("Sales", PlaybookKind.Member, ct);

        Assert.True(written);
        Assert.Equal(1, raised);
    }

    /// <summary>The first 16 hex characters of the SHA-256 of the LF-normalised, trimmed text, computed here independently of the product.</summary>
    /// <param name="text">The default's text.</param>
    private static string ExpectedHash(string text)
    {
        string normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalised)))[..16];
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Feed;

namespace Agency.Huddle.Tests.Agents;

/// <summary>
/// Pins the start-up update of an installed Chief of Staff (#343 Existing installs): an untouched
/// earlier shipped version is replaced by the current default, an edited file is never touched and
/// earns one Notification per shipped version, and the hash list cannot rot.
/// </summary>
public sealed class BuiltinAgentUpdateTests
{
    private const string OldSentence = "You are the Chief of Staff of an earlier release.";

    private static readonly string EarlierText = BuiltinAgent.DefaultText.Replace(
        "You are the Chief of Staff.", OldSentence, StringComparison.Ordinal);

    private static PersonaStore NewStore(TempDataDir dataDir) => new(
        new AgentPaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()),
        new PersonaWorkModeStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);

    private static BuiltinAgentSeeder NewSeeder(PersonaStore personas, RecordingFeedPublisher feed) =>
        new(personas, NullLogger<BuiltinAgentSeeder>.Instance, feed, new BuiltinAgentVersions([BuiltinAgentVersions.Hash(EarlierText)]));

    private static void Install(PersonaStore personas, string text, string name, string alias)
    {
        Assert.True(PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out _));
        var (_, body) = PersonaFrontmatter.Parse(text);
        _ = personas.Add(identity with { Name = name, Alias = alias }, body, model: "opus-test", effort: "high", workMode: "plan");
    }

    private static string FileOf(PersonaStore personas, string name) =>
        File.ReadAllText(Assert.Single(personas.Entries, e => string.Equals(e.Name, name, StringComparison.Ordinal)).Path);

    /// <summary>
    /// An untouched earlier version is replaced by the current default; a renamed Name and Alias
    /// survive, and the stored Model, Effort and Work Mode are kept (the reset would clear them).
    /// </summary>
    [Fact]
    public async Task Start_UntouchedEarlierVersion_UpdatesKeepingNameAliasAndSettings()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        RecordingFeedPublisher feed = new();
        Install(personas, EarlierText, "Alfred", "alf");

        await NewSeeder(personas, feed).StartAsync(ct);

        var text = FileOf(personas, "Alfred");
        Assert.Contains("name: 'Alfred'", text, StringComparison.Ordinal); // contains-ok: the renamed front matter line survives
        Assert.Contains("alias: 'alf'", text, StringComparison.Ordinal); // contains-ok: the renamed front matter line survives
        Assert.DoesNotContain(OldSentence, text, StringComparison.Ordinal);
        Assert.Equal(BuiltinAgentVersions.CurrentHash, BuiltinAgentVersions.Hash(text));
        Persona? persona = personas.Get("Alfred");
        Assert.NotNull(persona);
        Assert.Equal("opus-test", persona.Model);
        Assert.Equal("high", persona.Effort);
        Assert.Equal("plan", persona.WorkMode);
        Assert.Empty(feed.Posts);
    }

    /// <summary>
    /// D14: after the seeder replaces the file, the store itself serves the new body to a session: the Persona
    /// it returns holds the new text, not a cached copy of the old one.
    /// </summary>
    [Fact]
    public async Task Start_UntouchedEarlierVersion_StoreServesTheNewBodyNotTheCachedOne()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        Install(personas, EarlierText, "Alfred", "alf");
        Persona? before = personas.Get("Alfred");
        Assert.NotNull(before);
        Assert.Contains(OldSentence, before.Text, StringComparison.Ordinal); // contains-ok: proves the starting text really was the old one

        await NewSeeder(personas, new RecordingFeedPublisher()).StartAsync(ct);

        Persona? after = personas.Get("Alfred");
        Assert.NotNull(after);
        Assert.DoesNotContain(OldSentence, after.Text, StringComparison.Ordinal);
        var (_, newBody) = PersonaFrontmatter.Parse(BuiltinAgent.DefaultText);
        var (_, servedBody) = PersonaFrontmatter.Parse(after.Text);
        Assert.Equal(newBody.Trim(), servedBody.Trim());
    }

    /// <summary>A file the Human edited stays byte-identical and gets one Notification, not one per start.</summary>
    [Fact]
    public async Task Start_EditedFile_StaysIdenticalWithOneNotificationPerVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        RecordingFeedPublisher feed = new();
        Install(personas, EarlierText.Replace(OldSentence, "My own words.", StringComparison.Ordinal), "Alfred", "alf");
        var before = FileOf(personas, "Alfred");

        await NewSeeder(personas, feed).StartAsync(ct);
        await NewSeeder(personas, feed).StartAsync(ct);

        Assert.Equal(before, FileOf(personas, "Alfred"));
        FeedMessage post = Assert.Single(feed.Posts);
        Assert.Equal(FeedSourceType.BuiltinAgentUpdateAvailable, post.SourceType);
        Assert.True(post.Notify);
        BuiltinAgentUpdatePayload payload = Assert.IsType<BuiltinAgentUpdatePayload>(post.Payload);
        Assert.Contains("Reset to default", payload.Message, StringComparison.Ordinal); // contains-ok: names the Human's action
    }

    /// <summary>The Notification's wire name is stored in the Feed table, so it is pinned.</summary>
    [Fact]
    public void UpdateNotification_WireName_IsStable()
    {
        Assert.Equal("builtin-agent-update-available", FeedSourceTypes.ToWire(FeedSourceType.BuiltinAgentUpdateAvailable));
    }

    /// <summary>A file already at the current version is not written and not announced.</summary>
    [Fact]
    public async Task Start_CurrentVersion_DoesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        RecordingFeedPublisher feed = new();
        Install(personas, BuiltinAgent.DefaultText, "Alfred", "alf");
        var path = Assert.Single(personas.Entries).Path;
        DateTime stamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);
        var before = File.ReadAllText(path);

        await NewSeeder(personas, feed).StartAsync(ct);

        Assert.Equal(before, File.ReadAllText(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.Empty(feed.Posts);
    }

    /// <summary>A second start after an update does nothing: no write and no Notification.</summary>
    [Fact]
    public async Task Start_SecondStartAfterUpdate_DoesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        RecordingFeedPublisher feed = new();
        Install(personas, EarlierText, "Alfred", "alf");
        await NewSeeder(personas, feed).StartAsync(ct);
        var path = Assert.Single(personas.Entries).Path;
        DateTime stamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);
        var after = File.ReadAllText(path);

        await NewSeeder(personas, feed).StartAsync(ct);

        Assert.Equal(after, File.ReadAllText(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.Empty(feed.Posts);
    }

    /// <summary>
    /// A seeded file is what <c>PersonaStore.Add</c> composes, not the embedded text; it must hash as
    /// the current version, or every install would look edited.
    /// </summary>
    [Fact]
    public async Task Start_FreshSeed_HashesAsCurrentVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        RecordingFeedPublisher feed = new();

        await NewSeeder(personas, feed).StartAsync(ct);
        await NewSeeder(personas, feed).StartAsync(ct);

        Assert.Equal(BuiltinAgentVersions.CurrentHash, BuiltinAgentVersions.Hash(FileOf(personas, "Chief of Staff")));
        Assert.Empty(feed.Posts);
    }

    /// <summary>A Human edit that lands after the check is not overwritten: the replacement is refused.</summary>
    [Fact]
    public void TryReplaceText_FileChangedSinceTheCheck_IsRefusedAndKeepsTheEdit()
    {
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        Install(personas, EarlierText, "Alfred", "alf");
        var judged = FileOf(personas, "Alfred");
        var path = Assert.Single(personas.Entries).Path;
        File.WriteAllText(path, judged.Replace(OldSentence, "A Human edit.", StringComparison.Ordinal));
        var edited = File.ReadAllText(path);

        var replaced = personas.TryReplaceText("Alfred", judged, BuiltinAgent.DefaultText);

        Assert.False(replaced);
        Assert.Equal(edited, File.ReadAllText(path));
    }

    /// <summary>The hash ignores the name and alias lines and the line endings, and nothing else.</summary>
    [Fact]
    public void Hash_IgnoresNameAliasAndLineEndings_ButNotTheBody()
    {
        var renamed = BuiltinAgent.DefaultText.Replace("name: 'Chief of Staff'", "name: 'Alfred'", StringComparison.Ordinal)
            .Replace("alias: 'cos'", "alias: 'alf'", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal(BuiltinAgentVersions.CurrentHash, BuiltinAgentVersions.Hash(renamed));
        Assert.NotEqual(BuiltinAgentVersions.CurrentHash, BuiltinAgentVersions.Hash(EarlierText));
    }

    /// <summary>
    /// The guard (#343): the shipped hash list must hold this build's default. Change
    /// <c>chief-of-staff.md</c> without running <c>agents/scripts/Get-ChiefOfStaffHashes.ps1 -Write</c>
    /// and this fails, so the version it replaces can still be recognised and updated.
    /// </summary>
    [Fact]
    public void ShippedList_HoldsTheCurrentDefault()
    {
        Assert.True(
            BuiltinAgentVersions.Embedded.Contains(BuiltinAgentVersions.CurrentHash),
            "chief-of-staff.md changed; run agents/scripts/Get-ChiefOfStaffHashes.ps1 -Write and keep the earlier lines.");
    }

    /// <summary>
    /// The list keeps every earlier line as shipped: it starts with the eight known hashes in order, and its last line is this build's
    /// default. Overwriting an old line makes that version unrecognisable, so an untouched install would stop updating. When the default
    /// changes, the Human appends a line and extends the literal.
    /// </summary>
    [Fact]
    public void ShippedList_StartsWithTheKnownHashes_AndEndsWithTheCurrentOne()
    {
        string[] known =
        [
            "de3ed0cdfaebd8872b86580d26c3a47f5ced6e0e069bcd99a90d3cd8014aa2bf",
            "b4d6bf57d9116db0cb48c96d763c2f8e8506efc968385a9233a5377f6c4a960c",
            "046e0b09d3de78a0b27046163d8996ef06693ff521c349eb3804c47838ebb01c",
            "b7b21963c6c63b4d649e2b6829b398839cb1941d855a14dfc328643a01f10f1b",
            "7aa9b87ac0f88c49197a129d24b1743ad9f8a5e5d6e6c24550b70169b47d2a78",
            "dfe6b09862daf03a332688154807157ae31a14ec4cdce232bc6c19ff9438a0fc",
            "33d40e97eb332f31971b731d8e4a1b1c89bea81e8f7ef98d8cbf4b4571e27652",
            "755dc8a7df35902466f3a9c3c38af2a6b5e2186676c384eaf058a7170e00bfd7",
            "a0ce89479e032ea66c5317845cc95d3c262b7e688722abd1c1e395dcfe6f3574",
        ];
        string[] lines = [.. EmbeddedResources.ReadText(typeof(BuiltinAgentVersions).Assembly, "Builtin/chief-of-staff.hashes.txt")
            .Split('\n').Select(static line => line.Trim()).Where(static line => line.Length >= 64 && !line.StartsWith('#')).Select(static line => line[..64])];

        Assert.Equal(known, lines.Take(known.Length));
        Assert.Equal(BuiltinAgentVersions.CurrentHash, lines[^1]);
    }

    /// <summary>With the Feed off nothing is posted and no marker is written, so a later enable still gets the notice.</summary>
    [Fact]
    public async Task Start_FeedDisabled_WritesNoMarker_SoALaterEnableStillNotifies()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        Install(personas, EarlierText.Replace(OldSentence, "My own words.", StringComparison.Ordinal), "Alfred", "alf");
        BuiltinAgentSeeder off = new(personas, NullLogger<BuiltinAgentSeeder>.Instance, new NullFeedPublisher(), new BuiltinAgentVersions([BuiltinAgentVersions.Hash(EarlierText)]));

        await off.StartAsync(ct);

        Assert.False(File.Exists(Path.Combine(personas.Paths.DataDir, "chief-of-staff-notified.txt")));
        RecordingFeedPublisher feed = new();
        await NewSeeder(personas, feed).StartAsync(ct);
        _ = Assert.Single(feed.Posts);
    }

    /// <summary>A replacement text that would rename the Persona is refused and the file stays as it was.</summary>
    [Fact]
    public void TryReplaceText_NewTextRenames_IsRefusedAndKeepsTheFile()
    {
        using TempDataDir dataDir = new();
        using PersonaStore personas = NewStore(dataDir);
        Install(personas, EarlierText, "Alfred", "alf");
        var judged = FileOf(personas, "Alfred");

        var thrown = Assert.Throws<ChatException>(() => personas.TryReplaceText("Alfred", judged, BuiltinAgent.DefaultText));

        Assert.Contains("rename", thrown.Message, StringComparison.Ordinal); // contains-ok: names the refusal
        Assert.Equal(judged, FileOf(personas, "Alfred"));
    }
}

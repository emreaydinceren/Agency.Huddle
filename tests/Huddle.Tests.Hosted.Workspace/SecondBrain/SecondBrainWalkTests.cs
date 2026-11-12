using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Ui;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>
/// Task 138 (Spec §13): the life of the Team <c>Finances</c> walked through the real host
/// (<see cref="TeamWebApplicationFactory"/>, a temp data directory, Persona <c>Kim</c>, no ACP process, no network).
/// One ordered test follows the numbered steps: build from the <c>tax-season</c> type, drop a file in the Team Room,
/// compose the Agent's prompt, archive, unarchive. Every step asserts what it produced before the next one starts.
/// </summary>
public sealed class SecondBrainWalkTests
{
    private const string Finances = "Finances";

    private const string KimText = "---\nName: Kim\nTitle: Kim\nAlias: kim\n---\nYou are Kim.";

    private const string FileName = "2025 return.pdf";

    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n");

    /// <summary>Spec §13 end to end: Day 1 build and upload, Day 2 prompt, Day 71 archive, Day 120 unarchive.</summary>
    [Fact]
    public async Task Finances_BuiltUploadedPromptedArchivedAndUnarchived_EveryStepHolds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using TeamWebApplicationFactory factory = new();
        await factory.WriteDefinitionAsync("kim", KimText, ct);
        using HttpClient client = factory.CreateClient();
        IServiceProvider services = factory.Services;
        PersonaStore personas = services.GetRequiredService<PersonaStore>();
        await Eventually.UntilAsync(() => personas.Get("Kim") is not null, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        string financesDir = BuildFinances(factory);
        await AddKimAndRoomAsync(services, financesDir, ct);
        string room = await UploadAsync(factory, ct);
        await ComposePromptAsync(factory, financesDir, ct);
        await ArchiveAsync(factory, financesDir, room, ct);
        await UnarchiveAsync(factory, financesDir, room, ct);
    }

    /// <summary>Day 1: <c>ITeamFolders.CreateTeam</c> (the call the Build-a-Team page makes) from the <c>tax-season</c> type.</summary>
    private static string BuildFinances(TeamWebApplicationFactory factory)
    {
        ITeamFolders folders = factory.Services.GetRequiredService<ITeamFolders>();
        TeamFile file = new(new VersionedRef("tax-season", "1.0.0"), null, null, null, string.Empty);

        TeamCreateResult created = folders.CreateTeam(Finances, file);

        Assert.Equal(TeamCreateOutcome.Created, created.Outcome);
        Assert.Equal(ProvisionResult.Created, factory.Services.GetRequiredService<ProcessProvisioner>().ProvisionDefault(Finances));
        string dir = Path.Combine(factory.TasksDirPath, Finances);
        foreach (string folder in new[] { "raw", "memory", "outputs" })
        {
            Assert.True(Directory.Exists(Path.Combine(dir, folder)), folder);
        }

        Assert.True(File.Exists(Path.Combine(dir, "AGENTS.md")));
        Assert.True(File.Exists(Path.Combine(dir, "_team.md")));
        TeamFile? written = folders.ReadTeamFile(Finances);
        Assert.NotNull(written);
        Assert.Equal(new VersionedRef("tax-season", "1.0.0"), written.TeamType);
        Assert.NotNull(written.Process);
        Assert.False(File.ReadAllText(Path.Combine(dir, "AGENTS.md")).StartsWith("---", StringComparison.Ordinal));
        return dir;
    }

    /// <summary>Hires Kim as the manager and gives the Team its Room.</summary>
    private static async Task AddKimAndRoomAsync(IServiceProvider services, string financesDir, CancellationToken ct)
    {
        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        ITeamFolders folders = services.GetRequiredService<ITeamFolders>();
        await Eventually.UntilAsync(() => catalog.Find(Finances) is not null, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        MembershipResult added = services.GetRequiredService<ITeamMembership>().Add(Finances, "Kim");
        Assert.Equal(MembershipOutcome.Added, added.Outcome);
        Assert.True(folders.UpdateTeamFile(Finances, current => current with { Manager = "Kim" }));
        await Eventually.UntilAsync(() => catalog.Find(Finances)?.Members.Contains("Kim") == true, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));

        ITeamDirectory directory = services.GetRequiredService<ITeamDirectory>();
        Room? room = await directory.FindTeamRoomAsync(Finances, ct);
        room ??= await directory.CreateRoomAsync(Finances, [KnownIds.Human], Finances, teamRoom: true, ct: ct);
        Assert.Equal(Finances, room.Team);
        Assert.True(Directory.Exists(financesDir));
    }

    /// <summary>Day 1 U1: the file lands in <c>Teams/Finances/raw/&lt;roomId&gt;/</c> with a <c>raw_baselines</c> row.</summary>
    private static async Task<string> UploadAsync(TeamWebApplicationFactory factory, CancellationToken ct)
    {
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        Room room = Assert.IsType<Room>(await directory.FindTeamRoomAsync(Finances, ct));

        LibraryResult<LibraryUpload> result = await factory.Services.GetRequiredService<ChatUploadService>()
            .SaveAsync(room.Id, FileName, new MemoryStream(PdfBytes), ct);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        string expected = Path.Combine(factory.TasksDirPath, Finances, "raw", room.Id, FileName);
        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(result.Value.File.FullPath));
        Assert.Equal(PdfBytes, await File.ReadAllBytesAsync(expected, ct));
        string relative = Path.GetRelativePath(Path.GetFullPath(factory.DataDirPath), expected).Replace('\\', '/');
        Assert.True(factory.Services.GetRequiredService<RawBaselineStore>().TryGet(relative, out RawBaseline? baseline));
        Assert.Equal(PdfBytes.Length, baseline.SizeBytes);
        return room.Id;
    }

    /// <summary>Day 2: the composed prompt holds the AGENTS.md block, the Second Brain summary, Huddle-wide Memory and the outputs index.</summary>
    private static async Task ComposePromptAsync(TeamWebApplicationFactory factory, string financesDir, CancellationToken ct)
    {
        await File.AppendAllTextAsync(Path.Combine(financesDir, "AGENTS.md"), "\nLead every summary with the number.\n", ct);
        await File.WriteAllTextAsync(Path.Combine(financesDir, "outputs", "2025 summary.md"), "---\ndescription: The 2025 return in one page\n---\nBody.", ct);
        string huddleMemory = Path.Combine(factory.TasksDirPath, "memory");
        Directory.CreateDirectory(huddleMemory);
        await File.WriteAllTextAsync(Path.Combine(huddleMemory, "tone.md"), "Keep replies short.", ct);
        PersonaStore personas = factory.Services.GetRequiredService<PersonaStore>();
        Persona kim = Assert.IsType<Persona>(personas.Get("Kim"));

        (IReadOnlyList<PromptPart> parts, string adapter) = factory.Services.GetRequiredService<PersonaPromptPreview>().Build(kim);

        Assert.False(string.IsNullOrEmpty(adapter));
        IReadOnlyList<PromptPart> agentsMd = [.. parts.Where(p => p.Label == "AGENTS.md block")];
        Assert.True(agentsMd.Count >= 2, "levels sentence plus at least the Team section");
        string[] teamLines = Assert.Single(agentsMd, p => Regex.IsMatch(p.Text, @"^## Team Finances \(.*AGENTS\.md\)\r?\n", RegexOptions.None, TimeSpan.FromSeconds(5))).Text.Split('\n', StringSplitOptions.TrimEntries);
        Assert.Contains("Lead every summary with the number.", teamLines);
        Assert.Contains(parts, p => p.Label == "Second Brain" && p.Text.Length > 0);
        Assert.Matches(@"Keep replies short\.", Assert.Single(parts, p => p.Label == "Huddle-wide Memory").Text);
        Assert.Matches(@"2025 summary\.md.*The 2025 return in one page", Assert.Single(parts, p => p.Label == "Outputs index").Text);
    }

    /// <summary>Day 71 U11: the folder is complete under <c>Archive/</c>, labels gone, Rooms archived, a Feed item posted.</summary>
    private static async Task ArchiveAsync(TeamWebApplicationFactory factory, string financesDir, string roomId, CancellationToken ct)
    {
        IServiceProvider services = factory.Services;
        string archived = Path.Combine(factory.DataDirPath, "Archive", Finances);

        ArchiveOutcome outcome = await services.GetRequiredService<IArchiveService>().ArchiveTeamAsync(Finances, ct);

        Assert.True(outcome.Done, outcome.Refusal);
        Assert.False(Directory.Exists(financesDir));
        Assert.True(File.Exists(Path.Combine(archived, "_team.md")));
        Assert.True(File.Exists(Path.Combine(archived, "AGENTS.md")));
        Assert.True(File.Exists(Path.Combine(archived, "raw", roomId, FileName)));
        Assert.True(File.Exists(Path.Combine(archived, "outputs", "2025 summary.md")));
        Assert.Matches(@"(?m)^archivedMembers:", File.ReadAllText(Path.Combine(archived, "_team.md")));
        PersonaStore personas = services.GetRequiredService<PersonaStore>();
        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find(Finances) is null, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        Persona kim = Assert.IsType<Persona>(personas.Get("Kim"));
        Assert.True(PersonaFrontmatter.TryReadIdentity(kim.Text, out PersonaIdentity? identity, out string error), error);
        Assert.DoesNotContain(Finances, identity.Teams);
        Room room = Assert.IsType<Room>(await services.GetRequiredService<ITeamDirectory>().GetRoomAsync(roomId, ct));
        Assert.True(room.Archived);
        FeedStore feed = services.GetRequiredService<FeedStore>();
        await Eventually.UntilAsync(
            () => feed.Page(new FeedQuery(null, 50, null, null)).Any(i => i.SourceType == FeedSourceType.TeamArchived && i.Payload is TeamArchivedPayload { Team: Finances }),
            ct,
            timeout: TestBudgets.Slow,
            interval: TimeSpan.FromMilliseconds(50));
    }

    /// <summary>Day 120 U13: the folder, the member, the manager and the Room are back.</summary>
    private static async Task UnarchiveAsync(TeamWebApplicationFactory factory, string financesDir, string roomId, CancellationToken ct)
    {
        IServiceProvider services = factory.Services;

        ArchiveOutcome outcome = await services.GetRequiredService<IArchiveService>().UnarchiveTeamAsync(Finances, ct);

        Assert.True(outcome.Done, outcome.Refusal);
        Assert.True(File.Exists(Path.Combine(financesDir, "raw", roomId, FileName)));
        Assert.False(Directory.Exists(Path.Combine(factory.DataDirPath, "Archive", Finances)));
        ITeamCatalog catalog = services.GetRequiredService<ITeamCatalog>();
        await Eventually.UntilAsync(() => catalog.Find(Finances)?.Members.Contains("Kim") == true, ct, timeout: TestBudgets.Slow, interval: TimeSpan.FromMilliseconds(50));
        TeamFile? file = services.GetRequiredService<ITeamFolders>().ReadTeamFile(Finances);
        Assert.NotNull(file);
        Assert.Equal("Kim", file.Manager);
        Assert.Empty(file.ArchivedMembers);
        Room room = Assert.IsType<Room>(await services.GetRequiredService<ITeamDirectory>().GetRoomAsync(roomId, ct));
        Assert.False(room.Archived);
    }
}

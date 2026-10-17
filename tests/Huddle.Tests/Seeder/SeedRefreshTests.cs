using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.NeedsYou;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Teammates;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>
/// Pins what the seeder writes for Team Rooms, Message References, Uploads, Sprints, Requests, Library variety, avatars and
/// Needs you against the app's own readers and builders, so a format the app changes turns these red instead of silently
/// leaving the seed stale.
/// </summary>
public sealed class SeedRefreshTests
{
    private static PersonaStore Personas(IOptions<TeamOptions> options) =>
        new(new TeammatePaths(options), new PersonaModelStore(options), new PersonaEffortStore(options), new PersonaWorkModeStore(options), NullLogger<PersonaStore>.Instance);

    private static string ReferenceKey(MessageReference reference, SeedPlan plan)
    {
        SeedTask? task = reference.Kind == ReferenceKind.Task ? plan.Tasks.First(t => t.Id == reference.Key) : null;
        string title = task?.FormerTitle is null ? reference.Title ?? string.Empty : "(renamed)";
        return $"{reference.Kind}|{reference.Key}|{reference.Label}|{reference.Status}|{title}";
    }

    /// <summary>Each Team has exactly one Team Room holding the Human and the Team's Teammates, and the Team chats and the Team one-to-one are scoped without being the Team Room.</summary>
    [Fact]
    public async Task TeamRooms_AreScopedAsSeeded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        SqliteTeamDirectory directory = new(seed.Options());

        foreach (string team in new[] { "Platform", "Growth", "Support" })
        {
            Room? teamRoom = await directory.FindTeamRoomAsync(team, ct);
            Assert.NotNull(teamRoom);
            Assert.True(teamRoom.IsTeamRoom);
            Assert.Equal(team, teamRoom.Team);
            IReadOnlyList<User> members = await directory.GetRoomMembersAsync(teamRoom.Id, ct);
            string[] expected = ["You", .. plan.Teammates.Where(t => t.Teams.Contains(team, StringComparer.OrdinalIgnoreCase)).Select(t => t.Name)];
            Assert.Equal(expected.Order(StringComparer.Ordinal), members.Select(m => m.Name).Order(StringComparer.Ordinal));
        }

        IReadOnlyList<Room> scoped = [.. (await directory.GetRoomsAsync(ct)).Where(r => r.Team is not null)];
        Assert.Equal(plan.Rooms.Count(r => r.Team is not null), scoped.Count);
        Assert.Equal(3, scoped.Count(r => r.IsTeamRoom));
        Room chat = Assert.Single(scoped, r => r.Id == seed.Summary.Ids.RoomIds[SoftwareCoRooms.ChatPlatformPagination]);
        Assert.Equal("Platform", chat.Team);
        Assert.False(chat.IsTeamRoom);
        Room oneToOne = Assert.Single(scoped, r => r.Id == seed.Summary.Ids.RoomIds[SoftwareCoRooms.DmSupportDana]);
        Assert.Equal("Support", oneToOne.Team);
        Assert.Equal(2, (await directory.GetRoomMembersAsync(oneToOne.Id, ct)).Count);
        Assert.Contains(plan.Rooms, r => r.Team is null);
    }

    /// <summary>The seeder's labels, Room and Team links and Escape match <see cref="ReferenceText"/> for every Room and Team in the plan.</summary>
    [Fact]
    public async Task ReferenceFormats_MatchTheAppsReferenceText()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;

        foreach (SeedRoom room in plan.Rooms)
        {
            Assert.Equal(ReferenceText.RoomPlainLabelFor(room.Name, room.Team, room.IsTeamRoom), MessageReferences.RoomPlainLabel(room));
            Assert.Equal(ReferenceText.RoomLabelFor(room.Name, room.Team, room.IsTeamRoom), MessageReferences.Escape(MessageReferences.RoomPlainLabel(room)));
        }

        foreach (SeedTeam team in plan.Teams)
        {
            Assert.Equal(ReferenceText.PlainLabelFor(team.Name, ReferenceKind.Team), MessageReferences.TeamPlainLabel(team.Name));
            Assert.Equal(ReferenceText.LabelFor(team.Name, ReferenceKind.Team), MessageReferences.Escape(MessageReferences.TeamPlainLabel(team.Name)));
        }

        SeedRoom linked = plan.Rooms.First(r => r.Key == SoftwareCoRooms.PlatformStandup);
        string id = seed.Summary.Ids.RoomIds[linked.Key];
        ExpandedMessage room2 = MessageReferences.Expand("see {{room:platform-standup}} and {{team:Platform}}", linked, plan, seed.DataDir, seed.Summary.Ids.RoomIds);
        // contains-ok: Message prose, not markup: pins the link target the App's own RoomHref builds.
        Assert.Contains("](" + ReferenceText.RoomHref(id) + ")", room2.Text, StringComparison.Ordinal);
        // contains-ok: Message prose, not markup: pins the link target the App's own TeamHref builds.
        Assert.Contains("](" + ReferenceText.TeamHref("Platform") + ")", room2.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every stored reference equals what the app's own snapshotter builds from the same text, apart from the deliberately renamed Task
    /// (the stored chip keeps the old title) and the one deleted document (the snapshotter drops a file that does not exist).
    /// </summary>
    [Fact]
    public async Task MessageReferences_ResolveThroughTheAppsOwnSnapshotter()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        IOptions<TeamOptions> options = seed.Options();
        using PersonaStore personas = Personas(options);
        using TaskStore tasks = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
        SqliteTeamDirectory directory = new(options);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(seed.DataDir);
        using TempDataDir typesDir = new();
        FakeTeamCatalog catalog = new()
        {
            Teams = [.. plan.Teams.Select(t => new TeamSummary(t.Name, t.Projects, [], true))],
        };
        ReferenceResolver resolver = new(tasks, directory, catalog, RoleFixtures.Types(typesDir), new LibraryReferenceResolver(library.Resolver, library.Index, options), options);
        ReferenceSnapshotter snapshotter = new(resolver, NullLogger<ReferenceSnapshotter>.Instance);
        FileChatStore chat = new(options, NullLogger<FileChatStore>.Instance);

        List<MessageReference> unavailable = [];
        int renamed = 0;
        foreach (SeedRoom room in plan.Rooms)
        {
            IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(seed.Summary.Ids.RoomIds[room.Key], ct);
            foreach (ChatMessage message in messages)
            {
                IReadOnlyList<MessageReference> expected = await snapshotter.SnapshotAsync(message.Text, ct) ?? [];
                if (!room.WithReferences)
                {
                    Assert.Null(message.References);
                    continue;
                }

                IReadOnlyList<MessageReference> stored = message.References ?? [];
                MessageReference[] gone = [.. stored.Where(r => r.Kind == ReferenceKind.LibraryFile && !File.Exists(r.Key))];
                unavailable.AddRange(gone);
                renamed += stored.Count(r => r.Kind == ReferenceKind.Task && plan.Tasks.First(t => t.Id == r.Key).FormerTitle is not null);
                Assert.Equal(expected.Select(r => ReferenceKey(r, plan)), stored.Except(gone).Select(r => ReferenceKey(r, plan)));
            }
        }

        MessageReference missing = Assert.Single(unavailable);
        // contains-ok: the Key is an absolute path under a temp seed root; only the file name is stable.
        Assert.EndsWith("Customer-Update-Draft.md", missing.Key, StringComparison.Ordinal);
        Assert.True(renamed > 0);
    }

    /// <summary>The archived Room keeps no stored References, and the app's text scanner still finds something to show in it.</summary>
    [Fact]
    public async Task ArchivedRoom_LeavesReferencesNullForTheScannerFallback()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        FileChatStore chat = new(seed.Options(), NullLogger<FileChatStore>.Instance);

        IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(seed.Summary.Ids.RoomIds[SoftwareCoRooms.Q2Retro], ct);

        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.Null(m.References));
        Assert.Contains(messages, m => ReferenceScanner.Scan(m.Text).Count > 0);
    }

    /// <summary>No transcript keeps a <c>{{token}}</c>, and the Rooms cite documents so "Documents in this chat" has two to four rows in the three named Rooms.</summary>
    [Fact]
    public async Task Tokens_NeverSurviveIntoATranscript_AndNamedRoomsCiteDocuments()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        FileChatStore chat = new(seed.Options(), NullLogger<FileChatStore>.Instance);

        foreach (SeedRoom room in seed.Summary.Plan.Rooms)
        {
            IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(seed.Summary.Ids.RoomIds[room.Key], ct);
            Assert.All(messages, m => Assert.DoesNotContain("{{", m.Text, StringComparison.Ordinal));
        }

        foreach (string key in new[] { SoftwareCoRooms.PlatformStandup, SoftwareCoRooms.LaunchCrew, SoftwareCoRooms.IncidentWarRoom })
        {
            IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(seed.Summary.Ids.RoomIds[key], ct);
            string[] files = [.. messages.SelectMany(m => m.References ?? []).Where(r => r.Kind == ReferenceKind.LibraryFile).Select(r => r.Key).Distinct(StringComparer.OrdinalIgnoreCase)];
            Assert.InRange(files.Length, 2, 4);
        }

        IReadOnlyList<ChatMessage> standup = await chat.ReadAllAsync(seed.Summary.Ids.RoomIds[SoftwareCoRooms.PlatformStandup], ct);
        Assert.Contains(standup.SelectMany(m => m.References ?? []).Where(r => r.Kind == ReferenceKind.LibraryFile).GroupBy(r => r.Key), g => g.Count() > 1);
    }

    /// <summary>Uploads sit in the folder the app's upload flow resolves for the Room, and a Message cites each the way the composer writes it: a blank line, then the path in backticks.</summary>
    [Fact]
    public async Task Uploads_ExistWhereTheAppWouldLook_AndAreCitedLikeTheComposerWritesThem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        SqliteTeamDirectory directory = new(seed.Options());
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(seed.DataDir);
        LibraryFileService files = library.CreateService();
        Agency.Huddle.App.Uploads.RoomUploadFolders folders = new(library.Resolver, files);
        FileChatStore chat = new(seed.Options(), NullLogger<FileChatStore>.Instance);

        Assert.Contains(plan.Uploads, u => plan.Rooms.First(r => r.Key == u.RoomKey).Team is null);
        Assert.Contains(plan.Uploads, u => plan.Rooms.First(r => r.Key == u.RoomKey).Team is not null);
        // contains-ok: file names, not markup: one upload of each kind must exist.
        Assert.Contains(plan.Uploads, u => u.FileName.EndsWith(".png", StringComparison.Ordinal));
        // contains-ok: file names, not markup: one upload of each kind must exist.
        Assert.Contains(plan.Uploads, u => u.FileName.EndsWith(".txt", StringComparison.Ordinal));
        foreach (SeedUpload upload in plan.Uploads)
        {
            string roomId = seed.Summary.Ids.RoomIds[upload.RoomKey];
            Room room = await directory.GetRoomAsync(roomId, ct) ?? throw new InvalidOperationException(roomId);
            LibraryPath folder = folders.For(room)[^1];
            string path = Path.Combine(folder.FullPath, upload.FileName);

            Assert.True(File.Exists(path), path);
            Assert.Equal(upload.Content, await File.ReadAllBytesAsync(path, ct));
            IReadOnlyList<ChatMessage> messages = await chat.ReadAllAsync(roomId, ct);
            ChatMessage citing = Assert.Single(messages, m => m.Text.Contains("`" + path + "`", StringComparison.Ordinal));
            // contains-ok: Message prose, not markup: pins the code span the upload flow appends after a blank line.
            Assert.Contains("\n\n`", citing.Text, StringComparison.Ordinal);
            Assert.Contains(citing.References ?? [], r => r.Kind == ReferenceKind.LibraryFile && r.Key == path && r.Label == upload.FileName);
        }

        Assert.All(plan.Uploads.Where(u => u.FileName.EndsWith(".png", StringComparison.Ordinal)), u => Assert.Equal(".png", AvatarImage.SniffExtension(u.Content)));
    }

    /// <summary><c>sprints.md</c> parses with the app's own parser, every Task's Sprint is defined in it, 8 to 10 Tasks hold one, and the current-Sprint View loads.</summary>
    [Fact]
    public async Task Sprints_ParseWithTheAppsParser_AndTasksHoldThem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SeedPlan plan = seed.Summary.Plan;
        IOptions<TeamOptions> options = seed.Options();
        using PersonaStore personas = Personas(options);
        using TaskStore tasks = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        SprintFile file = SprintFileFormat.ParseFile(await File.ReadAllTextAsync(Path.Combine(seed.DataDir, "sprints.md"), ct));

        Assert.Empty(file.Problems);
        Assert.Equal(2, file.Sprints.Count);
        Assert.Equal(plan.Sprints.Select(s => s.Name), file.Sprints.Select(s => s.Name));
        Assert.InRange(plan.Tasks.Count(t => t.Sprint is not null), 8, 10);
        Assert.All(tasks.All, loaded =>
        {
            string? seeded = plan.Tasks.First(t => t.Id == loaded.Id.ToString()).Sprint;
            Assert.Equal(seeded, loaded.Sprint?.ToString());
            Assert.True(seeded is null || file.Sprints.Any(s => s.Id.ToString() == seeded));
        });
        using Agency.Huddle.App.Tasks.Views.ViewStore views = new(options, NullLogger<Agency.Huddle.App.Tasks.Views.ViewStore>.Instance);
        Assert.NotNull(views.Get("current-sprint"));
        // contains-ok: manifest.md is a long generated document; pins that the Sprint View is listed.
        Assert.Contains("current-sprint", ManifestWriter.Render(plan, seed.Summary.Ids, seed.Root), StringComparison.Ordinal);
    }

    /// <summary>The Requests parse with <see cref="RequestFields"/>: one per triage state, plus one Done and not yet confirmed, each with the sections its kind requires.</summary>
    [Fact]
    public async Task Requests_ParseWithTheAppsRequestFields()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        IOptions<TeamOptions> options = seed.Options();
        using PersonaStore personas = Personas(options);
        using TaskStore tasks = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        Assert.Empty(tasks.RejectedFiles);
        List<(TaskItem Task, RequestFields Fields)> requests = [];
        foreach (TaskItem task in tasks.All)
        {
            if (RequestFields.TryRead(task, out RequestFields? fields) && fields is not null)
            {
                requests.Add((task, fields));
            }
        }

        Assert.Equal(seed.Summary.Plan.Tasks.Count(t => t.Request is not null), requests.Count);
        Assert.Equal(
            [TriageState.None, TriageState.NeedsInfo, TriageState.Accepted, TriageState.Declined],
            requests.Select(r => r.Fields.Triage).Distinct().Order());
        Assert.Contains(requests, r => r.Task.Status == TaskState.Done && r.Fields.ConfirmedBy.Length == 0 && !r.Task.Location.Closed);
        Assert.Contains(requests, r => r.Fields.Triage == TriageState.Declined && r.Fields.DeclineReason.Length > 0);
        Assert.All(requests, r =>
        {
            Assert.NotEqual(r.Fields.FromTeam, r.Task.Location.Team);
            // contains-ok: Task description prose; pins each section the App requires for the Request kind.
            Assert.All(RequestFields.RequiredSections(r.Fields.Kind), section => Assert.Contains("## " + section, r.Task.Description, StringComparison.Ordinal));
        });
    }

    /// <summary>The long note gives the Outline four heading levels, its wikilinks use every supported form and resolve with the app's resolver, and <c>Legal-Checklist</c> stays the one unresolved link.</summary>
    [Fact]
    public async Task Library_OffersOutlineHeadings_AndEveryWikilinkForm()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        SeedPlan plan = seed.Summary.Plan;
        const string handbookPath = "Teams/Platform/Engineering-Handbook.md";
        string handbook = plan.Files.Single(f => f.RelativePath == handbookPath).Content;
        List<string> notes = [.. plan.Files.Where(f => f.RelativePath.StartsWith("Teams/", StringComparison.Ordinal) && f.RelativePath.EndsWith(".md", StringComparison.Ordinal) && !f.RelativePath.Contains("/memory/", StringComparison.Ordinal)).Select(f => f.RelativePath["Teams/".Length..])];
        string from = handbookPath["Teams/".Length..];

        IReadOnlyList<OutlineHeading> headings = MarkdownOutline.Headings(handbook);
        IReadOnlyList<WikiLink> links = WikiLinkParser.Parse(handbook);

        Assert.True(headings.Count >= 8);
        Assert.Equal([1, 2, 3, 4], headings.Select(h => h.Level).Distinct().Order());
        Assert.Contains(links, l => l.Heading is not null);
        Assert.Contains(links, l => l.Alias is not null);
        // contains-ok: link targets, not markup: at least one folder-form wikilink must exist.
        Assert.Contains(links, l => l.Target.Contains('/', StringComparison.Ordinal));
        Assert.All(links, link => Assert.NotNull(WikiLinkResolver.Resolve(notes, from, link.Target).Path));
        WikiLink withHeading = links.First(l => l.Heading is not null);
        string target = WikiLinkResolver.Resolve(notes, from, withHeading.Target).Path ?? string.Empty;
        string targetText = plan.Files.Single(f => f.RelativePath == "Teams/" + target).Content;
        Assert.Contains(MarkdownOutline.Headings(targetText), h => h.Text == withHeading.Heading);

        string pricingBrief = plan.Files.Single(f => f.RelativePath.EndsWith("Pricing-Brief.md", StringComparison.Ordinal)).Content;
        WikiLink legal = Assert.Single(WikiLinkParser.Parse(pricingBrief), l => l.Target == "Legal-Checklist");
        Assert.Null(WikiLinkResolver.Resolve(notes, "Growth/Pricing Page/Pricing-Brief.md", legal.Target).Path);
        // contains-ok: relative paths, not markup: a Team-folder image must exist.
        Assert.Contains(plan.BinaryFiles, f => f.RelativePath.StartsWith("Teams/", StringComparison.Ordinal) && f.RelativePath.EndsWith(".png", StringComparison.Ordinal));
        // contains-ok: relative paths, not markup: a Team-folder text file must exist.
        Assert.Contains(plan.Files, f => f.RelativePath.StartsWith("Teams/", StringComparison.Ordinal) && f.RelativePath.EndsWith(".txt", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(seed.DataDir, "Teams", "Growth", "Pricing Page", "competitor-chart.png")));
    }

    /// <summary>The avatar entry's image is a real PNG in the avatars folder and the app's store resolves it.</summary>
    [Fact]
    public async Task Avatars_ImageEntryResolvesToAFileOnDisk()
    {
        using SeededFolder seed = await SeededFolder.CreateAsync(TestContext.Current.CancellationToken);
        using AvatarStore store = new(seed.Options(), NullLogger<AvatarStore>.Instance);

        Avatar marcus = store.Get("Marcus");

        Assert.NotNull(marcus.Image);
        string path = Path.Combine(store.ImageDirectory, marcus.Image);
        Assert.True(File.Exists(path));
        Assert.Equal(".png", AvatarImage.SniffExtension(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)));
    }

    /// <summary>Exactly one live Room ends with an unanswered Teammate Message that mentions the Human, by the rule the Needs you list uses.</summary>
    [Fact]
    public async Task NeedsYou_OneRoomHoldsAnUnansweredMention()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using SeededFolder seed = await SeededFolder.CreateAsync(ct);
        SqliteTeamDirectory directory = new(seed.Options());
        FileChatStore chat = new(seed.Options(), NullLogger<FileChatStore>.Instance);
        List<string> waiting = [];

        foreach (SeedRoom room in seed.Summary.Plan.Rooms)
        {
            string id = seed.Summary.Ids.RoomIds[room.Key];
            IReadOnlyList<User> members = await directory.GetRoomMembersAsync(id, ct);
            IReadOnlyList<ChatMessage> tail = await chat.ReadTailAsync(id, NeedsYouStartupScan.TailLength, ct);
            ChatMessage? mention = NeedsYouRule.OldestUnansweredMention(
                tail,
                message => MentionParser.Parse(message.Text, members, []).Any(user => user.Id == KnownIds.Human));
            if (mention is not null)
            {
                waiting.Add(room.Key);
            }
        }

        Assert.Equal([SoftwareCoRooms.LaunchCrew], waiting);
    }
}

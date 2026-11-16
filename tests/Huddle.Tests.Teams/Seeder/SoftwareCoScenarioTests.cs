using System.Text.RegularExpressions;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>Pins the <c>software-co</c> plan against the requirements and against the coverage list in <c>agents/DataSeeding.md</c>. No files are written. Seeder spec §6 and §8.</summary>
public sealed partial class SoftwareCoScenarioTests
{
    private static readonly string[] AllStatuses = ["Backlog", "To Do", "In Progress", "Review", "Done", "Cancelled", "Duplicate", "Rejected"];

    private static SeedPlan Plan() => new SoftwareCoScenario().Build(SeededFolder.Today);

    [GeneratedRegex(@"\b[A-Z]{4}-\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex TaskIdPattern();

    /// <summary>SEED-SC-12: The scenario list shows what the seed now covers: the household Team, Team Rooms, Sprints and Requests are named.</summary>
    [Fact]
    public void Description_NamesTheCoveredFeatures()
    {
        Assert.Equal(
            "A software company and a family: Platform, Growth, Support and Home and Family (12 Projects, shared Agents, Team Rooms, Sprints and Requests), plus empty-state Teams Sandbox and Records, and an archived Team, Project and chats to restore.",
            new SoftwareCoScenario().Description);
    }

    /// <summary>SEED-SC-1: Four working Teams, each with at least two Projects; three work Agents span two or more of them, and the household Agents stay in their own Team.</summary>
    [Fact]
    public void Plan_HasFourWorkingTeamsAndSharedAgents()
    {
        SeedPlan plan = Plan();

        string[] working = [.. plan.Teams.Where(t => t.Projects.Count >= 2).Select(t => t.Name)];

        Assert.Equal(["Platform", "Growth", "Support", "Home and Family"], working);
        Assert.Contains(plan.Agents, t => t.Teams.Intersect(working, StringComparer.OrdinalIgnoreCase).Count() >= 2);
        Assert.Equal(3, plan.Agents.Count(t => t.Teams.Intersect(working, StringComparer.OrdinalIgnoreCase).Count() >= 2));
    }

    /// <summary>SEED-SC-3: The cast agreed in the interview: nine work Agents, then the four household Agents, with these Team memberships.</summary>
    [Fact]
    public void Plan_HasTheAgreedCast()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Grace", "Marcus", "Lena", "Owen", "Sofia", "Dana", "Alan", "Maya", "Ben", "Hattie", "Theo", "Ravi", "Iris"], plan.Agents.Select(t => t.Name));
        Assert.Equal(["Platform", "Growth"], plan.Agents.Single(t => t.Name == "Lena").Teams);
        Assert.Equal(["Growth"], plan.Agents.Single(t => t.Name == "Maya").Teams);
        Assert.Equal(["Support", "Records"], plan.Agents.Single(t => t.Name == "Dana").Teams);
        Assert.Empty(plan.Agents.Single(t => t.Name == "Ben").Teams);
        Assert.Equal(["incident-postmortem"], plan.Agents.Single(t => t.Name == "Alan").Skills);
    }

    /// <summary>SEED-SC-2: Every Team an Agent names exists, and the two edge-case Teams are what the interview asked for.</summary>
    [Fact]
    public void Plan_HasTheEdgeTeams()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Agents.SelectMany(t => t.Teams), team => Assert.Contains(plan.Teams, t => t.Name == team));
        Assert.DoesNotContain(plan.Agents, t => t.Teams.Contains("Sandbox"));
        Assert.Empty(plan.Teams.Single(t => t.Name == "Sandbox").Projects);
        Assert.Empty(plan.Teams.Single(t => t.Name == "Records").Projects);
        Assert.Equal(["Grace", "Dana"], plan.Agents.Where(t => t.Teams.Contains("Records")).Select(t => t.Name));
    }

    /// <summary>SEED-INV-7: Names and aliases are unique, and no alias equals another Agent's Name, as the Persona loader requires.</summary>
    [Fact]
    public void Plan_AgentNamesAndAliasesAreUnique()
    {
        SeedPlan plan = Plan();

        Assert.Equal(plan.Agents.Count, plan.Agents.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(plan.Agents.Count, plan.Agents.Select(t => t.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(plan.Agents, t => Assert.DoesNotContain(plan.Agents.Where(o => o != t), o => string.Equals(o.Name, t.Alias, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>SEED-SC-4: All eight statuses appear, and 46 Tasks are seeded: 41 ordinary ones and 5 Requests.</summary>
    [Fact]
    public void Plan_CoversEveryTaskStatus()
    {
        SeedPlan plan = Plan();

        Assert.Equal(64, plan.Tasks.Count);
        Assert.All(AllStatuses, status => Assert.Contains(plan.Tasks, t => t.Status == status));
    }

    /// <summary>SEED-INV-3: Ids are unique, the number is 4 digits, and the prefix is what the app would derive from the Team name.</summary>
    [Fact]
    public void Plan_TaskIdsAreUniqueAndUseTheDerivedPrefix()
    {
        SeedPlan plan = Plan();

        Assert.Equal(plan.Tasks.Count, plan.Tasks.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(plan.Tasks, task =>
        {
            string prefix = new([.. task.Location.Team.Where(char.IsLetterOrDigit).Take(4)]);
            Assert.StartsWith(prefix.ToUpperInvariant() + "-", task.Id, StringComparison.Ordinal);
            Assert.Matches("^[A-Z0-9]{4}-\\d{4}$", task.Id);
        });
    }

    /// <summary>SEED-INV-6: Every reference between Tasks, Agents, Teams and Projects points at something that exists.</summary>
    [Fact]
    public void Plan_ReferencesResolve()
    {
        SeedPlan plan = Plan();
        HashSet<string> ids = [.. plan.Tasks.Select(t => t.Id)];
        HashSet<string> people = [plan.HumanName, .. plan.Agents.Select(t => t.Name)];

        Assert.All(plan.Tasks, task =>
        {
            Assert.Contains(task.Creator, people);
            Assert.True(task.Assignee is null || people.Contains(task.Assignee));
            Assert.True(task.Parent is null || ids.Contains(task.Parent));
            Assert.All(task.BlockedBy, id => Assert.Contains(id, ids));
            Assert.Equal(task.Status == "Duplicate", task.DuplicateOf is not null);
            Assert.True(task.DuplicateOf is null || ids.Contains(task.DuplicateOf));
            Assert.True(task.OriginRoom is null || plan.Rooms.Any(r => r.Key == task.OriginRoom));
            SeedTeam team = Assert.Single(plan.Teams, t => t.Name == task.Location.Team);
            Assert.True(task.Location.Project is null || team.Projects.Contains(task.Location.Project));
        });
    }

    /// <summary>
    /// SEED-INV-4: A finished Task is closed and an unfinished one is open, so the <c>_closed</c> folder always agrees with the status. The one
    /// exception is a Done Request nobody has confirmed: the app refuses to close it until the requesting Team's QA confirms.
    /// </summary>
    [Fact]
    public void Plan_ClosedMeansFinished()
    {
        Assert.All(Plan().Tasks, task =>
        {
            bool awaitingConfirmation = task.Status == "Done" && task.Request is { ConfirmedBy.Length: 0 };
            Assert.Equal(ManifestWriter.IsFinished(task.Status) && !awaitingConfirmation, task.Closed);
        });
    }

    /// <summary>SEED-INV-5: Timeline times must run forward, and the last entry must be in the past so Huddle does not see the file as newer.</summary>
    [Fact]
    public void Plan_TimelinesRunForwardAndEndInThePast()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Tasks, task =>
        {
            Assert.True(task.CreatedDaysAgo >= task.LastTouchedDaysAgo, task.Id);
            Assert.True(task.LastTouchedDaysAgo >= 1, task.Id);
            IReadOnlyList<TimelineEntry> log = TaskFileWriter.BuildTimeline(task, plan.Today);
            Assert.Equal("created", log[0].Text);
            Assert.Equal(log.OrderBy(l => l.At).ToList(), log);
            Assert.Equal(log.Count, log.Select(l => l.At).Distinct().Count());
            Assert.True(log[^1].At < new DateTimeOffset(plan.Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), task.Id);
            Assert.Equal(task.Closed, log[^1].Text == "closed");
        });
    }

    /// <summary>SEED-SC-4: The derived states a tester needs: overdue, blocked, blocked only by a finished Task, sub-task, Duplicate, unassigned, Human-assigned.</summary>
    [Fact]
    public void Plan_CoversTheDerivedTaskStates()
    {
        SeedPlan plan = Plan();

        Assert.NotEmpty(ManifestWriter.Overdue(plan));
        Assert.NotEmpty(ManifestWriter.Blocked(plan));
        Assert.Contains(plan.Tasks, t => t.BlockedBy.Count > 0 && !ManifestWriter.Blocked(plan).Contains(t.Id));
        Assert.Contains(plan.Tasks, t => t.Parent is not null);
        Assert.Contains(plan.Tasks, t => t.Status == "Duplicate");
        Assert.Contains(plan.Tasks, t => t.Assignee is null);
        Assert.Contains(plan.Tasks, t => t.Assignee == plan.HumanName);
        Assert.Contains(plan.Tasks, t => t.Location.Project is null);
        Assert.Contains(plan.Tasks, t => t.Location.Project is not null);
        Assert.Contains(plan.Tasks, t => t.Tags.Count > 0);
        Assert.Contains(plan.Tasks, t => t.Body.Length > 600);
        Assert.Contains(plan.Tasks, t => t.ExtraEvents.Any(e => e.Summary.StartsWith("moved:", StringComparison.Ordinal))); // contains-ok: an event summary's kind prefix, not markup
        Assert.Contains(plan.Tasks, t => t.Creator == plan.HumanName);
        Assert.Contains(plan.Tasks, t => t.Creator != plan.HumanName);
    }

    /// <summary>SEED-INV-2: Overdue dates are at least two days back so they stay overdue in every time zone.</summary>
    [Fact]
    public void Plan_OverdueTasksAreAtLeastTwoDaysLate()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Tasks.Where(t => ManifestWriter.Overdue(plan).Contains(t.Id)), t => Assert.True(t.DueOffset <= -2, t.Id));
    }

    /// <summary>SEED-SC-5: At least one Project has no Tasks, so the empty-Project state is testable.</summary>
    [Fact]
    public void Plan_HasAProjectWithNoTasks()
    {
        SeedPlan plan = Plan();

        Assert.Contains(plan.Teams.SelectMany(t => t.Projects.Select(p => (Team: t.Name, Project: p))),
            x => !plan.Tasks.Any(t => t.Location.Team == x.Team && t.Location.Project == x.Project));
    }

    /// <summary>SEED-SC-7, SEED-SC-15: Notes link to one another: one note has several backlinks and exactly one link is unresolved.</summary>
    [Fact]
    public void Plan_LibraryHasBacklinksAndOneUnresolvedLink()
    {
        SeedPlan plan = Plan();
        IReadOnlyDictionary<string, IReadOnlyList<string>> links = ManifestWriter.LinksByNote(plan);

        Assert.True(links.Count(l => l.Value.Contains("Personas", StringComparer.OrdinalIgnoreCase)) >= 3);
        string[] unresolved = [.. links.SelectMany(l => l.Value).Where(target => !links.ContainsKey(target)).Distinct(StringComparer.OrdinalIgnoreCase)];

        // Missing on purpose: Legal-Checklist, plus the front matter properties demo's missing link ([[Nobody]]) and bare alias ([[Plan]]).
        Assert.Equal(["Legal-Checklist", "Nobody", "Plan"], unresolved.Order(StringComparer.Ordinal));
        Assert.Contains(plan.Files, f => f.RelativePath.StartsWith("Agents/Alan/work/", StringComparison.Ordinal) && !f.RelativePath.Contains("/memory/", StringComparison.Ordinal));
    }

    /// <summary>SEED-SC-6: Team memory, Project memory and Agent memory each exist.</summary>
    [Fact]
    public void Plan_HasThreeKindsOfMemory()
    {
        SeedPlan plan = Plan();

        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teams/[^/]+/memory/[^/]+\\.md$"));
        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teams/[^/]+/[^/]+/memory/[^/]+\\.md$"));
        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Agents/[^/]+/work/memory/[^/]+\\.md$"));
    }

    /// <summary>
    /// SEED-SC-9: Six root Rooms (two one-to-one, three groups and one archived; one custom-named) and the Team-scoped ones: a Team
    /// Room per live Team and the archived Team, and nine other Team chats, three of them archived (SEED-SC-18); transcripts of 8 to 15 messages.
    /// </summary>
    [Fact]
    public void Plan_HasTheAgreedRooms()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Grace", "Owen", "Platform standup", "Launch crew", "Q2 retro", "Incident 0412 war room"], plan.Rooms.Where(r => r.Team is null).Select(r => r.Name));
        Assert.Equal(["Platform", "Growth", "Support", "Home and Family", SoftwareCoArchive.LegacySite], plan.Rooms.Where(r => r.IsTeamRoom).Select(r => r.Team));
        Assert.Equal(9, plan.Rooms.Count(r => r.Team is not null && !r.IsTeamRoom));
        Assert.Equal(2, plan.Rooms.Count(r => r.Team is null && r.Members.Count == 1));
        Assert.Equal(["Q2 retro", SoftwareCoArchive.LegacySite, "Checkout v1 sunset", "Log shipping spike", "Spring promo wrap-up"], plan.Rooms.Where(r => r.Archived).Select(r => r.Name));
        Assert.All(plan.Rooms, r => Assert.InRange(r.Messages.Count, 8, 15));
    }

    /// <summary>
    /// SEED-SC-16: Legacy Site is archived as <c>ArchiveService</c> leaves a Team: it is not a live Team, its notes are under
    /// <c>Archive/Legacy Site/</c>, its roster names Agents who exist, none of them still carries its label, and every Room
    /// scoped to it, its Team Room included, is archived.
    /// </summary>
    [Fact]
    public void Plan_HasAnArchivedTeam_AsTheArchiveLeavesIt()
    {
        SeedPlan plan = Plan();

        SeedArchivedTeam archived = Assert.Single(plan.ArchivedTeams);
        Assert.Equal(SoftwareCoArchive.LegacySite, archived.Name);
        Assert.Equal([SoftwareCoArchive.CheckoutV1, "Old Marketing Site"], archived.Projects);
        Assert.DoesNotContain(plan.Teams, t => string.Equals(t.Name, archived.Name, StringComparison.OrdinalIgnoreCase));
        Assert.All(archived.Members, member => Assert.Contains(plan.Agents, a => a.Name == member));
        Assert.DoesNotContain(plan.Agents, a => a.Teams.Contains(archived.Name, StringComparer.OrdinalIgnoreCase));
        SeedRoom[] rooms = [.. plan.Rooms.Where(r => r.Team == archived.Name)];
        Assert.Equal(2, rooms.Length);
        Assert.All(rooms, r => Assert.True(r.Archived));
        Assert.Single(rooms, r => r.IsTeamRoom);
        Assert.All(rooms, r => Assert.All(r.Members, m => Assert.Contains(m, archived.Members)));
        Assert.DoesNotContain(plan.Files, f => f.RelativePath.StartsWith("Teams/" + archived.Name + "/", StringComparison.Ordinal));
        string[] projectsWithNotes = [.. archived.Projects.Where(p => plan.Files.Any(f => f.RelativePath.StartsWith($"{archived.Folder}/{p}/", StringComparison.Ordinal)))];
        Assert.Equal(archived.Projects, projectsWithNotes);
    }

    /// <summary>SEED-SC-17: Growth has one archived Project, Spring Promo, kept out of its live Projects and holding notes and a memory fact under <c>Archive/Growth/</c>.</summary>
    [Fact]
    public void Plan_GrowthHasAnArchivedProject()
    {
        SeedPlan plan = Plan();

        SeedTeam growth = Assert.Single(plan.Teams, t => t.Name == "Growth");
        Assert.Equal([SoftwareCoArchive.SpringPromo], growth.ArchivedProjects);
        Assert.DoesNotContain(SoftwareCoArchive.SpringPromo, growth.Projects);
        Assert.All(plan.Teams.Where(t => t.Name != "Growth"), t => Assert.Empty(t.ArchivedProjects));
        string folder = $"Archive/Growth/{SoftwareCoArchive.SpringPromo}/";
        Assert.Equal(2, plan.Files.Count(f => f.RelativePath.StartsWith(folder, StringComparison.Ordinal) && !f.RelativePath.Contains("/memory/", StringComparison.Ordinal)));
        Assert.Single(plan.Files, f => f.RelativePath.StartsWith(folder + "memory/", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Tasks, t => t.Location.Project == SoftwareCoArchive.SpringPromo);
    }

    /// <summary>
    /// SEED-SC-18: Four archived Team chats cover each place a chat can belong: the archived Team's Team Room, a chat in one of its
    /// Projects, a chat in a live Team's live Project (Platform / Observability), and a chat whose Project is archived (Growth / Spring Promo).
    /// </summary>
    [Fact]
    public void Plan_HasArchivedTeamChatsInEveryKindOfScope()
    {
        SeedPlan plan = Plan();

        SeedRoom[] archived = [.. plan.Rooms.Where(r => r.Archived && r.Team is not null)];

        Assert.Equal(
            [SoftwareCoArchive.TeamLegacySite, SoftwareCoArchive.ChatLegacyCheckout, SoftwareCoArchive.ChatPlatformLogSpike, SoftwareCoArchive.ChatGrowthSpringPromo],
            archived.Select(r => r.Key));
        Assert.Equal(
            [(SoftwareCoArchive.LegacySite, (string?)null), (SoftwareCoArchive.LegacySite, SoftwareCoArchive.CheckoutV1), ("Platform", "Observability"), ("Growth", SoftwareCoArchive.SpringPromo)],
            archived.Select(r => (r.Team ?? string.Empty, r.Project)));
        Assert.All(plan.Rooms.Where(r => r.Project is not null), r => Assert.NotNull(r.Team));
    }

    /// <summary>SEED-INV-6, SEED-INV-8: Messages come only from the Human or a member, run forward in time, and every Task id they mention exists.</summary>
    [Fact]
    public void Plan_RoomTranscriptsAreConsistent()
    {
        SeedPlan plan = Plan();
        HashSet<string> ids = [.. plan.Tasks.Select(t => t.Id)];

        Assert.All(plan.Rooms, room =>
        {
            Assert.All(room.Members, m => Assert.Contains(plan.Agents, t => t.Name == m));
            Assert.All(room.Messages, m => Assert.True(m.Sender == plan.HumanName || room.Members.Contains(m.Sender), $"{room.Key}: {m.Sender}"));
            DateTime[] times = [.. room.Messages.Select(m => plan.Today.AddDays(-m.DaysAgo).ToDateTime(TimeOnly.ParseExact(m.Time, "HH:mm", System.Globalization.CultureInfo.InvariantCulture)))];
            Assert.Equal([.. times.Order()], times);
            Assert.All(room.Messages.SelectMany(m => TaskIdPattern().Matches(m.Text).Select(x => x.Value)), id => Assert.Contains(id, ids));
        });
        Assert.Contains(plan.Rooms, r => r.Messages.Any(m => TaskIdPattern().IsMatch(m.Text)));
        Assert.Contains(plan.Rooms, r => r.Members.Count > 1 && r.Messages.Any(m => m.Text.Contains('@', StringComparison.Ordinal)));
    }

    /// <summary>
    /// SEED-SC-8: Each tagged note shows one tag feature through ordinary content: a comma string, a quoted flow list, a block list with a
    /// non-Latin tag and an emoji, a malformed <c>tags: #draft</c>, a <c>...</c> closer, tags in headings, and what is not a tag
    /// (ticket numbers, Task ids, code, URLs and comments). The expected tags are exact.
    /// </summary>
    [Theory]
    [InlineData("Teams/Platform/Meeting-Notes-2026-10-14.md", "platform|meeting/weekly|2026/q4|platform/ops|platform/api/v2")]
    [InlineData("Teams/Platform/API v2/Deprecation-Plan.md", "platform/api|v1-sunset|Deprecation|2026/q4|2027/q2")]
    [InlineData("Teams/Growth/Onboarding Revamp/Interview-Notes.md", "growth/research|user-interview|müşteri/geri-bildirim|persona/solo-founder|onboarding/import|persona/team-lead|onboarding/invite|\U0001F680")]
    [InlineData("Teams/Growth/Pricing Page/Competitor-Teardown.md", "pricing/per-seat|pricing/usage|growth/pricing")]
    [InlineData("Teams/Growth/Lifecycle Emails/Welcome-Email-Draft.md", "email/welcome|draft")]
    [InlineData("Teams/Support/Help Center/Release-Notes-Draft.md", "support/docs|release-notes")]
    [InlineData("Teams/Support/Customer-Feedback-Log.md", "feedback/bug|Reports|feedback/feature-request|feedback/praise|onboarding|billing")]
    [InlineData("Teams/Support/Help Center/Style-Guide.md", "")]
    public void Plan_TaggedNotes_HaveTheExpectedTags(string path, string expected)
    {
        SeedFile note = Plan().Files.Single(f => f.RelativePath == path);

        Assert.Equal(expected, string.Join("|", NoteTags.Of(note.Content)));
    }

    /// <summary>SEED-SC-8, SEED-SC-15: The seeded Library has nested tags across all three Teams, a casing variant that merges, and a tag shared by several notes.</summary>
    [Fact]
    public void Plan_Library_HasNestedSharedTagsAcrossTeams()
    {
        Dictionary<string, IReadOnlyList<string>> tagged = Plan().Files
            .Where(static f => f.RelativePath.EndsWith(".md", StringComparison.Ordinal))
            .ToDictionary(static f => f.RelativePath, static f => NoteTags.Of(f.Content), StringComparer.Ordinal);

        string[] incident = [.. tagged.Where(static n => n.Value.Contains("support/incident", StringComparer.OrdinalIgnoreCase)).Select(static n => n.Key).Order(StringComparer.Ordinal)];
        string[] research = [.. tagged.Where(static n => n.Value.Contains("growth/research", StringComparer.OrdinalIgnoreCase)).Select(static n => n.Key).Order(StringComparer.Ordinal)];

        Assert.Equal(
            ["Agents/Alan/work/Postmortem-Draft-0412.md", "Teams/Platform/Observability/Runbook.md", "Teams/Support/Escalation-Guide.md", "Teams/Support/Incident Tooling/Incident-0412.md"],
            incident);
        Assert.Equal(["Teams/Growth/Onboarding Revamp/Interview-Notes.md", "Teams/Growth/Personas.md", "Teams/Growth/Pricing Page/Pricing-Research.md"], research);
        Assert.Equal(["growth/pricing", "Launch"], tagged["Teams/Growth/Pricing Page/Pricing-Brief.md"]);
        Assert.Equal(["growth/email", "launch"], tagged["Teams/Growth/Lifecycle Emails/Email-Ideas.md"]);
        Assert.Equal(["platform/api", "platform/api/v2"], tagged["Teams/Platform/API v2/API-v2-Design.md"]);
    }

    /// <summary>SEED-SC-10: Every view id is unique and a saved View exists for a list, a kanban and a closed scope.</summary>
    [Fact]
    public void Plan_ViewsCoverListKanbanAndClosed()
    {
        using System.Text.Json.JsonDocument views = System.Text.Json.JsonDocument.Parse(Plan().ViewsJson);
        System.Text.Json.JsonElement[] items = [.. views.RootElement.GetProperty("views").EnumerateArray()];

        Assert.Equal(items.Length, items.Select(v => v.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(items, v => v.GetProperty("kind").GetString() == "kanban");
        Assert.Contains(items, v => v.GetProperty("kind").GetString() == "list");
        Assert.Contains(items, v => v.GetProperty("scope").GetString() == "closed");
    }

    private static string Rows(SeedPlan plan, string path)
    {
        SeedFile note = plan.Files.Single(f => f.RelativePath == path);
        IReadOnlyDictionary<string, NotePropertyKind> types = PropertyTypes.Parse(plan.Files.Single(f => f.RelativePath == "Teams/" + LibraryOptions.DefaultPropertyTypesFile).Content);
        IReadOnlyList<NoteProperty> typed = PropertyTyping.Apply(NoteProperties.Read(NoteFrontmatter.Locate(note.Content)), types);
        return string.Join("|", typed.Select(static p => $"{p.Key}={p.Kind}"));
    }

    /// <summary>FP-T16: the plan holds the four front matter property notes, and the types file at the option's default path, in the Teams root.</summary>
    [Fact]
    public void Plan_HoldsTheFrontMatterPropertyNotesAndTheTypesFile()
    {
        SeedPlan plan = Plan();
        string[] paths = [.. plan.Files.Select(static f => f.RelativePath)];

        Assert.Contains("Teams/Platform/AllTypes.md", paths);
        Assert.Contains("Teams/Platform/AllTypes-Json.md", paths);
        Assert.Contains("Teams/Platform/Properties-Edge-Cases.md", paths);
        Assert.Contains("Teams/Platform/Plan-Review.md", paths);
        SeedFile types = plan.Files.Single(static f => f.RelativePath == "Teams/" + LibraryOptions.DefaultPropertyTypesFile);
        Assert.Equal(new Dictionary<string, NotePropertyKind> { ["pages"] = NotePropertyKind.Number, ["due"] = NotePropertyKind.Date }.OrderBy(static p => p.Key), PropertyTypes.Parse(types.Content).OrderBy(static p => p.Key));
    }

    /// <summary>FP-T16: <c>AllTypes.md</c> is the example's text, the same text the FP-T1 fixture reads.</summary>
    [Fact]
    public void Plan_AllTypes_EqualsTheExampleFixture()
    {
        SeedFile note = Plan().Files.Single(static f => f.RelativePath == "Teams/Platform/AllTypes.md");

        Assert.Equal(SoftwareCoLibrary.AllTypesNote, note.Content);
    }

    /// <summary>FP-T16: reading each seeded note through the reader and the typing gives the rows the Seeded demo lists.</summary>
    [Theory]
    [InlineData("Teams/Platform/AllTypes.md", "title=Text|url=Text|pages=Number|reviewed=Checkbox|created=Date|logged=DateTime|due=Mismatch|owner=Text|authors=List|tags=Tags|aliases=Aliases|cssclasses=List|budget=Unsupported|notes=Text|empty=Text")]
    [InlineData("Teams/Platform/AllTypes-Json.md", "title=Text|url=Text|pages=Number|reviewed=Checkbox|created=Text|logged=Text|due=Mismatch|owner=Text|authors=List|tags=Tags|aliases=Aliases|cssclasses=List|empty=Text")]
    [InlineData("Teams/Platform/Properties-Edge-Cases.md", "reviewer=Text|related=Unsupported|flag=Text|tag=Text|cssclass=Text|publish=Checkbox|broken=Unsupported|status=Text")]
    public void Plan_PropertyNotes_ReadToTheListedRows(string path, string expected) =>
        Assert.Equal(expected, Rows(Plan(), path));

    /// <summary>FP-T16: the edge-case note's last <c>status</c> wins, the quoted boolean stays Text, and the unquoted link is shown as its source.</summary>
    [Fact]
    public void Plan_EdgeCases_ShowTheirSpecificValues()
    {
        SeedFile note = Plan().Files.Single(static f => f.RelativePath == "Teams/Platform/Properties-Edge-Cases.md");

        IReadOnlyList<NoteProperty> properties = NoteProperties.Read(NoteFrontmatter.Locate(note.Content));

        Assert.Equal("final", properties.Single(static p => p.Key == "status").Value);
        Assert.Equal("true", properties.Single(static p => p.Key == "flag").Value);
        Assert.Equal("[[Architecture]]", properties.Single(static p => p.Key == "related").Source);
        Assert.Equal("status", properties[^1].Key);
    }

    /// <summary>FP-T16: the Plan-Review note links the aliased note by its file name and writes the bare alias as a link that stays missing.</summary>
    [Fact]
    public void Plan_PlanReview_LinksThroughTheAliasAndKeepsTheBareAliasMissing()
    {
        SeedFile note = Plan().Files.Single(static f => f.RelativePath == "Teams/Platform/Plan-Review.md");

        Assert.Equal(
            "# Plan review\n\nThe plan is written up in [[AllTypes|Plan]], which is also found under that alias.\n\nA bare alias is not a link target: [[Plan]] stays a missing link.",
            note.Content.ReplaceLineEndings("\n"));
    }

    /// <summary>FP-T16: every property link in the seeded AllTypes notes resolves through the real index in the note's own root (links never cross roots).</summary>
    [Theory]
    [InlineData("Teams/Platform/AllTypes.md")]
    [InlineData("Teams/Platform/AllTypes-Json.md")]
    public void Plan_PropertyLinks_ResolveInTheirOwnRoot(string path)
    {
        SeedPlan plan = Plan();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        foreach (SeedFile file in plan.Files.Where(static f => f.RelativePath.StartsWith("Teams/", StringComparison.Ordinal)))
        {
            string target = Path.Combine(fixture.DataDir, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target) ?? fixture.DataDir);
            File.WriteAllText(target, file.Content);
        }

        fixture.Reload();
        LibraryPath note = fixture.ResolveTeams(path["Teams/".Length..]);
        string content = plan.Files.Single(f => f.RelativePath == path).Content;
        List<WikiLink> links = [.. NoteProperties.Read(NoteFrontmatter.Locate(content)).SelectMany(static p => p.Items.Append(p.Value)).SelectMany(static text => WikiLinkParser.ParseSlice(text))];

        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.NotNull(fixture.Index.Resolve(note, link).Path));
    }

    /// <summary>SEED-SC-13: every Team root and every Project folder holds at least four Markdown or text documents of its own; Sandbox stays empty.</summary>
    [Fact]
    public void Plan_EveryTeamAndProjectHasFourDocuments()
    {
        SeedPlan plan = Plan();
        string[] folders =
        [
            .. plan.Teams
                .Where(static t => !string.Equals(t.Name, "Sandbox", StringComparison.Ordinal))
                .SelectMany(static t => new[] { $"Teams/{t.Name}" }.Concat(t.Projects.Select(p => $"Teams/{t.Name}/{p}"))),
        ];

        Assert.All(folders, folder =>
        {
            int documents = plan.Files.Count(f => IsDocument(f.RelativePath) && string.Equals(FolderOf(f.RelativePath), folder, StringComparison.Ordinal));
            Assert.True(documents >= 4, $"{folder} has {documents} documents.");
        });
    }

    /// <summary>SEED-SC-14: three Team memory facts per Team with members, one per Project, two per Agent, and every fact is one line.</summary>
    [Fact]
    public void Plan_MemoryCoversEveryTeamProjectAndAgent()
    {
        SeedPlan plan = Plan();
        HashSet<string> staffed = new(plan.Agents.SelectMany(static a => a.Teams), StringComparer.Ordinal);

        Assert.All(plan.Teams.Where(t => staffed.Contains(t.Name)), team => Assert.True(MemoryIn(plan, $"Teams/{team.Name}/memory") >= 3, team.Name));
        Assert.All(plan.Teams.SelectMany(static t => t.Projects.Select(p => $"Teams/{t.Name}/{p}/memory")), folder => Assert.True(MemoryIn(plan, folder) >= 1, folder));
        Assert.All(plan.Agents, agent => Assert.True(MemoryIn(plan, $"Agents/{agent.Name}/work/memory") >= 2, agent.Name));
        Assert.All(plan.Files.Where(static f => f.RelativePath.Contains("/memory/", StringComparison.Ordinal)), fact =>
            Assert.DoesNotContain('\n', fact.Content.TrimEnd('\n')));
    }

    /// <summary>SEED-HOME-1, -2: Home and Family has its five Projects, and its four Agents belong to it and nothing else.</summary>
    [Fact]
    public void Plan_HomeAndFamily_HasItsProjectsAndCast()
    {
        SeedPlan plan = Plan();

        SeedTeam home = Assert.Single(plan.Teams, t => t.Name == SoftwareCoHome.Team);
        Assert.Equal(["Kids and School", "Meals", "Home Maintenance", "Family Admin", "Holidays and Events"], home.Projects);
        Assert.Equal(["Hattie", "Theo", "Ravi", "Iris"], plan.Agents.Where(a => a.Teams.Contains(SoftwareCoHome.Team)).Select(a => a.Name));
        Assert.All(plan.Agents.Where(a => a.Teams.Contains(SoftwareCoHome.Team)), a => Assert.Equal([SoftwareCoHome.Team], a.Teams));
    }

    /// <summary>SEED-HOME-3: family members are never users: no Task is assigned to them, no Room holds them, and Sam's card is tagged instead.</summary>
    [Fact]
    public void Plan_HomeAndFamily_FamilyAreCharactersNotUsers()
    {
        SeedPlan plan = Plan();
        HashSet<string> people = new([plan.HumanName, .. plan.Agents.Select(a => a.Name)], StringComparer.Ordinal);

        Assert.All(plan.Tasks.Select(t => t.Assignee).OfType<string>(), assignee => Assert.Contains(assignee, people));
        Assert.All(plan.Rooms.SelectMany(r => r.Members), m => Assert.Contains(m, people));
        SeedTask sams = Assert.Single(plan.Tasks, t => t.Tags.Contains("owner/sam") && t.Status != "Duplicate");
        Assert.Null(sams.Assignee);
    }

    /// <summary>SEED-HOME-4: the household's Tasks use every status and show the states a family meets.</summary>
    [Fact]
    public void Plan_HomeAndFamily_TasksCoverEveryState()
    {
        SeedPlan plan = Plan();
        SeedTask[] home = [.. plan.Tasks.Where(t => t.Location.Team == SoftwareCoHome.Team)];

        Assert.All(AllStatuses, status => Assert.Contains(home, t => t.Status == status));
        Assert.Contains(home, t => t.DueOffset == 1 && t.Assignee == plan.HumanName);
        Assert.Contains(home, t => ManifestWriter.Overdue(plan).Contains(t.Id));
        Assert.Contains(home, t => ManifestWriter.Blocked(plan).Contains(t.Id));
        Assert.Contains(home, t => t.Parent is not null);
        Assert.Contains(home, t => t.Assignee is null);
        Assert.Contains(home, t => t.HumanGate);
    }

    /// <summary>SEED-HOME-6: Mia's peanut allergy is a Meals memory fact, and Theo quotes it in a household Room.</summary>
    [Fact]
    public void Plan_HomeAndFamily_AgentQuotesItsMemory()
    {
        SeedPlan plan = Plan();

        SeedFile fact = Assert.Single(plan.Files, f => f.RelativePath == $"Teams/{SoftwareCoHome.Team}/Meals/memory/mia-peanut-allergy.md");
        SeedRoom planning = Assert.Single(plan.Rooms, r => r.Key == SoftwareCoHome.ChatSundayPlanning);
        SeedMessage quote = Assert.Single(planning.Messages, m => m.Sender == "Theo" && m.Time == "16:02");

        Assert.Equal(SoftwareCoHome.Team, planning.Team);
        Assert.Equal("Mia is allergic to peanuts: nothing with peanuts comes into the kitchen.\n", fact.Content);
        Assert.Equal("Five dinners in {{lib:Teams/Home and Family/Meals/Weekly-Meal-Plan.md}}. No peanuts anywhere, as always for Mia's allergy.", quote.Text);
    }

    /// <summary>
    /// SEED-SC-19: Thirteen Jobs cover every timetable preset, the Human and Agents as owners, Jobs scheduled by the Human, by
    /// the owner, by a manager for a member of its Team and by the Chief of Staff, a paused Job, three named time zones, a
    /// one-off still ahead, and two sections Huddle cannot read.
    /// </summary>
    [Fact]
    public void Plan_HasJobsInEveryShape()
    {
        SeedPlan plan = Plan();
        Job[] jobs = [.. plan.Jobs.Select(JobSeeder.ToJob)];

        Assert.Equal(13, jobs.Length);
        Assert.Equal(Enum.GetValues<JobCadence>(), jobs.Select(j => j.When.Cadence).Distinct().Order());
        Assert.All(jobs, j => Assert.Matches("^job-[0-9a-f]{6}$", j.Id));
        Assert.Equal(jobs.Length, jobs.Select(j => j.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(plan.Jobs.Where(j => j.Owner is not null), j => Assert.Contains(plan.Agents, a => a.Name == j.Owner));
        Assert.Equal(2, jobs.Count(j => j.Owner.IsHuman));
        Assert.Equal("Chief of Staff", Assert.Single(jobs, j => j.Owner.IsHuman && j.CreatedBy != plan.HumanName).CreatedBy);
        Assert.Contains(plan.Jobs, j => j.Owner is not null && j.By == plan.HumanName);
        Assert.Contains(plan.Jobs, j => j.Owner is not null && j.By == j.Owner);
        Assert.Contains(plan.Jobs, j => j.Owner is not null && j.By != j.Owner && plan.Teams.Any(t => t.Manager == j.By && plan.Agents.Single(a => a.Name == j.Owner).Teams.Contains(t.Name)));
        Assert.Single(jobs, j => j.Paused);
        Assert.Equal(["America/New_York", "Europe/London", "UTC"], jobs.Select(j => j.TimeZoneId).OfType<string>().Distinct().Order(StringComparer.Ordinal));
        SeedJob once = Assert.Single(plan.Jobs, j => JobSeeder.ToJob(j).When.Cadence == JobCadence.Once);
        Assert.Empty(once.Runs);
        Assert.NotNull(JobSeeder.ToJob(once).When.NextAfter(new DateTimeOffset(plan.Today.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero), TimeZoneInfo.Utc));
        Assert.All(plan.JobProblems, p => Assert.Null(Assert.Single(JobDocument.Parse(p.Raw, p.Owner is null ? JobOwner.Human : JobOwner.Agent(p.Owner))).Job));
        Assert.Equal(["Fortnightly SLA report", "Pantry stock-take"], plan.JobProblems.Select(p => p.Title));
    }

    /// <summary>
    /// SEED-SC-19, SEED-INV-8: A Job's past runs are before the run date and run forward in time, every reply comes from its
    /// owner or the Human, a last run that posted nothing drew no reply, and one Job each found its Budget spent and has an
    /// archived Room.
    /// </summary>
    [Fact]
    public void Plan_JobRunsAreConsistent()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Jobs, job =>
        {
            DateTimeOffset[] times = [.. job.Runs.SelectMany(r => (IEnumerable<DateTimeOffset>)[DatabaseSeeder.Timestamp(plan.Today, r.DaysAgo, r.Time), .. r.Replies.Select(m => DatabaseSeeder.Timestamp(plan.Today, m.DaysAgo, m.Time))])];
            Assert.Equal([.. times.Order()], times);
            Assert.All(times, t => Assert.True(t < new DateTimeOffset(plan.Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), job.Id));
            Assert.All(job.Runs.SelectMany(r => r.Replies), m => Assert.True(m.Sender == (job.Owner ?? plan.HumanName) || m.Sender == plan.HumanName, job.Id));
            if (job.Runs.Count > 0 && job.LastOutcome != JobOutcome.Delivered)
            {
                Assert.Empty(job.Runs[^1].Replies);
            }
        });
        Assert.Equal(SoftwareCoJobs.BudgetSpentJob, Assert.Single(plan.Jobs, j => j.LastOutcome == JobOutcome.BudgetSpent).Id);
        SeedJob archived = Assert.Single(plan.Jobs, j => j.RoomArchived);
        Assert.Equal(SoftwareCoJobs.ArchivedRoomJob, archived.Id);
        Assert.NotEmpty(archived.Runs);
        Assert.Contains(plan.Jobs, j => j.Owner is null && j.Runs.Count > 0);
    }

    private static bool IsDocument(string path) =>
        (path.EndsWith(".md", StringComparison.Ordinal) || path.EndsWith(".txt", StringComparison.Ordinal))
        && !path.Contains("/memory/", StringComparison.Ordinal);

    private static string FolderOf(string path) => path[..path.LastIndexOf('/')];

    private static int MemoryIn(SeedPlan plan, string folder) =>
        plan.Files.Count(f => string.Equals(FolderOf(f.RelativePath), folder, StringComparison.Ordinal));
}

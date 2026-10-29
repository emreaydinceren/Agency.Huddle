using System.Text.RegularExpressions;
using Agency.Huddle.App.Library;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>Pins the <c>software-co</c> plan against the requirements and against the coverage list in <c>agents/DataSeeding.md</c>. No files are written.</summary>
public sealed partial class SoftwareCoScenarioTests
{
    private static readonly string[] AllStatuses = ["Backlog", "To Do", "In Progress", "Review", "Done", "Cancelled", "Duplicate", "Rejected"];

    private static SeedPlan Plan() => new SoftwareCoScenario().Build(SeededFolder.Today);

    [GeneratedRegex(@"\b[A-Z]{4}-\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex TaskIdPattern();

    /// <summary>The scenario list shows what the seed now covers: Team Rooms, Sprints and Requests are named.</summary>
    [Fact]
    public void Description_NamesTheCoveredFeatures()
    {
        Assert.Equal(
            "A software company: Platform, Growth and Support (7 Projects, shared Agents, Team Rooms, Sprints and Requests), plus empty-state Teams Sandbox and Archive.",
            new SoftwareCoScenario().Description);
    }

    /// <summary>The request: at least three Teams, each with at least two Projects, sharing Agents between them.</summary>
    [Fact]
    public void Plan_HasThreeTeamsWithTwoProjectsEachAndSharedAgents()
    {
        SeedPlan plan = Plan();

        string[] working = [.. plan.Teams.Where(t => t.Projects.Count >= 2).Select(t => t.Name)];

        Assert.Equal(["Platform", "Growth", "Support"], working);
        Assert.Contains(plan.Agents, t => t.Teams.Intersect(working, StringComparer.OrdinalIgnoreCase).Count() >= 2);
        Assert.Equal(3, plan.Agents.Count(t => t.Teams.Intersect(working, StringComparer.OrdinalIgnoreCase).Count() >= 2));
    }

    /// <summary>The cast agreed in the interview: nine Agents with these Team memberships.</summary>
    [Fact]
    public void Plan_HasTheAgreedCast()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Grace", "Marcus", "Lena", "Owen", "Sofia", "Dana", "Alan", "Maya", "Ben"], plan.Agents.Select(t => t.Name));
        Assert.Equal(["Platform", "Growth"], plan.Agents.Single(t => t.Name == "Lena").Teams);
        Assert.Equal(["Growth"], plan.Agents.Single(t => t.Name == "Maya").Teams);
        Assert.Equal(["Support", "Archive"], plan.Agents.Single(t => t.Name == "Dana").Teams);
        Assert.Empty(plan.Agents.Single(t => t.Name == "Ben").Teams);
        Assert.Equal(["incident-postmortem"], plan.Agents.Single(t => t.Name == "Alan").Skills);
    }

    /// <summary>Every Team an Agent names exists, and the two edge-case Teams are what the interview asked for.</summary>
    [Fact]
    public void Plan_HasTheEdgeTeams()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Agents.SelectMany(t => t.Teams), team => Assert.Contains(plan.Teams, t => t.Name == team));
        Assert.DoesNotContain(plan.Agents, t => t.Teams.Contains("Sandbox"));
        Assert.Empty(plan.Teams.Single(t => t.Name == "Sandbox").Projects);
        Assert.Empty(plan.Teams.Single(t => t.Name == "Archive").Projects);
        Assert.Equal(["Grace", "Dana"], plan.Agents.Where(t => t.Teams.Contains("Archive")).Select(t => t.Name));
    }

    /// <summary>Names and aliases are unique, and no alias equals another Agent's Name, as the Persona loader requires.</summary>
    [Fact]
    public void Plan_AgentNamesAndAliasesAreUnique()
    {
        SeedPlan plan = Plan();

        Assert.Equal(plan.Agents.Count, plan.Agents.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(plan.Agents.Count, plan.Agents.Select(t => t.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(plan.Agents, t => Assert.DoesNotContain(plan.Agents.Where(o => o != t), o => string.Equals(o.Name, t.Alias, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>All eight statuses appear, and 46 Tasks are seeded: 41 ordinary ones and 5 Requests.</summary>
    [Fact]
    public void Plan_CoversEveryTaskStatus()
    {
        SeedPlan plan = Plan();

        Assert.Equal(46, plan.Tasks.Count);
        Assert.All(AllStatuses, status => Assert.Contains(plan.Tasks, t => t.Status == status));
    }

    /// <summary>Ids are unique, the number is 4 digits, and the prefix is what the app would derive from the Team name.</summary>
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

    /// <summary>Every reference between Tasks, Agents, Teams and Projects points at something that exists.</summary>
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
    /// A finished Task is closed and an unfinished one is open, so the <c>_closed</c> folder always agrees with the status. The one
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

    /// <summary>Change log times must run forward, and the last entry must be in the past so Huddle does not see the file as newer.</summary>
    [Fact]
    public void Plan_ChangeLogsRunForwardAndEndInThePast()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Tasks, task =>
        {
            Assert.True(task.CreatedDaysAgo >= task.LastTouchedDaysAgo, task.Id);
            Assert.True(task.LastTouchedDaysAgo >= 1, task.Id);
            IReadOnlyList<TaskLogLine> log = TaskFileWriter.BuildLog(task, plan.Today);
            Assert.Equal("created", log[0].Summary);
            Assert.Equal(log.OrderBy(l => l.At).ToList(), log);
            Assert.Equal(log.Count, log.Select(l => l.At).Distinct().Count());
            Assert.True(log[^1].At < new DateTimeOffset(plan.Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), task.Id);
            Assert.Equal(task.Closed, log[^1].Summary == "closed");
        });
    }

    /// <summary>The derived states a tester needs: overdue, blocked, blocked only by a finished Task, sub-task, Duplicate, unassigned, Human-assigned.</summary>
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
        Assert.Contains(plan.Tasks, t => t.ExtraLog.Any(e => e.Summary.StartsWith("moved:", StringComparison.Ordinal)));
        Assert.Contains(plan.Tasks, t => t.Creator == plan.HumanName);
        Assert.Contains(plan.Tasks, t => t.Creator != plan.HumanName);
    }

    /// <summary>Overdue dates are at least two days back so they stay overdue in every time zone.</summary>
    [Fact]
    public void Plan_OverdueTasksAreAtLeastTwoDaysLate()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Tasks.Where(t => ManifestWriter.Overdue(plan).Contains(t.Id)), t => Assert.True(t.DueOffset <= -2, t.Id));
    }

    /// <summary>At least one Project has no Tasks, so the empty-Project state is testable.</summary>
    [Fact]
    public void Plan_HasAProjectWithNoTasks()
    {
        SeedPlan plan = Plan();

        Assert.Contains(plan.Teams.SelectMany(t => t.Projects.Select(p => (Team: t.Name, Project: p))),
            x => !plan.Tasks.Any(t => t.Location.Team == x.Team && t.Location.Project == x.Project));
    }

    /// <summary>Notes link to one another: one note has several backlinks and exactly one link is unresolved.</summary>
    [Fact]
    public void Plan_LibraryHasBacklinksAndOneUnresolvedLink()
    {
        SeedPlan plan = Plan();
        IReadOnlyDictionary<string, IReadOnlyList<string>> links = ManifestWriter.LinksByNote(plan);

        Assert.True(links.Count(l => l.Value.Contains("Personas", StringComparer.OrdinalIgnoreCase)) >= 3);
        string[] unresolved = [.. links.SelectMany(l => l.Value).Where(target => !links.ContainsKey(target)).Distinct(StringComparer.OrdinalIgnoreCase)];
        Assert.Equal(["Legal-Checklist"], unresolved);
        Assert.Contains(plan.Files, f => f.RelativePath.StartsWith("Agents/Alan/work/", StringComparison.Ordinal) && !f.RelativePath.Contains("/memory/", StringComparison.Ordinal));
    }

    /// <summary>Team memory, Project memory and Agent memory each exist.</summary>
    [Fact]
    public void Plan_HasThreeKindsOfMemory()
    {
        SeedPlan plan = Plan();

        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teams/[^/]+/memory/[^/]+\\.md$"));
        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teams/[^/]+/[^/]+/memory/[^/]+\\.md$"));
        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Agents/[^/]+/work/memory/[^/]+\\.md$"));
    }

    /// <summary>
    /// Six root Rooms (two one-to-one, three groups and one archived; one custom-named) and six Team-scoped ones (three Team Rooms,
    /// two Team chats and a Team one-to-one); transcripts of 8 to 15 messages.
    /// </summary>
    [Fact]
    public void Plan_HasTheAgreedRooms()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Grace", "Owen", "Platform standup", "Launch crew", "Q2 retro", "Incident 0412 war room"], plan.Rooms.Where(r => r.Team is null).Select(r => r.Name));
        Assert.Equal(["Platform", "Growth", "Support"], plan.Rooms.Where(r => r.IsTeamRoom).Select(r => r.Team));
        Assert.Equal(3, plan.Rooms.Count(r => r.Team is not null && !r.IsTeamRoom));
        Assert.Equal(2, plan.Rooms.Count(r => r.Team is null && r.Members.Count == 1));
        Assert.Equal(["Q2 retro"], plan.Rooms.Where(r => r.Archived).Select(r => r.Name));
        Assert.All(plan.Rooms, r => Assert.InRange(r.Messages.Count, 8, 15));
    }

    /// <summary>Messages come only from the Human or a member, run forward in time, and every Task id they mention exists.</summary>
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
    /// Each tagged note shows one tag feature through ordinary content: a comma string, a quoted flow list, a block list with a
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

    /// <summary>The seeded Library has nested tags across all three Teams, a casing variant that merges, and a tag shared by several notes.</summary>
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

    /// <summary>Every view id is unique and a saved View exists for a list, a kanban and a closed scope.</summary>
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
}

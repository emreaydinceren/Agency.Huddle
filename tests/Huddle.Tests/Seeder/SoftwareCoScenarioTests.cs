using System.Text.RegularExpressions;
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

    /// <summary>The request: at least three Teams, each with at least two Projects, sharing Teammates between them.</summary>
    [Fact]
    public void Plan_HasThreeTeamsWithTwoProjectsEachAndSharedTeammates()
    {
        SeedPlan plan = Plan();

        string[] working = [.. plan.Teams.Where(t => t.Projects.Count >= 2).Select(t => t.Name)];

        Assert.Equal(["Platform", "Growth", "Support"], working);
        Assert.Contains(plan.Teammates, t => t.Teams.Intersect(working, StringComparer.OrdinalIgnoreCase).Count() >= 2);
        Assert.Equal(3, plan.Teammates.Count(t => t.Teams.Intersect(working, StringComparer.OrdinalIgnoreCase).Count() >= 2));
    }

    /// <summary>The cast agreed in the interview: nine Teammates with these Team memberships.</summary>
    [Fact]
    public void Plan_HasTheAgreedCast()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Grace", "Marcus", "Lena", "Owen", "Sofia", "Dana", "Alan", "Maya", "Ben"], plan.Teammates.Select(t => t.Name));
        Assert.Equal(["Platform", "Growth"], plan.Teammates.Single(t => t.Name == "Lena").Teams);
        Assert.Equal(["Growth"], plan.Teammates.Single(t => t.Name == "Maya").Teams);
        Assert.Equal(["Support", "Archive"], plan.Teammates.Single(t => t.Name == "Dana").Teams);
        Assert.Empty(plan.Teammates.Single(t => t.Name == "Ben").Teams);
        Assert.Equal(["incident-postmortem"], plan.Teammates.Single(t => t.Name == "Alan").Skills);
    }

    /// <summary>Every Team a Teammate names exists, and the two edge-case Teams are what the interview asked for.</summary>
    [Fact]
    public void Plan_HasTheEdgeTeams()
    {
        SeedPlan plan = Plan();

        Assert.All(plan.Teammates.SelectMany(t => t.Teams), team => Assert.Contains(plan.Teams, t => t.Name == team));
        Assert.DoesNotContain(plan.Teammates, t => t.Teams.Contains("Sandbox"));
        Assert.Empty(plan.Teams.Single(t => t.Name == "Sandbox").Projects);
        Assert.Empty(plan.Teams.Single(t => t.Name == "Archive").Projects);
        Assert.Equal(["Grace", "Dana"], plan.Teammates.Where(t => t.Teams.Contains("Archive")).Select(t => t.Name));
    }

    /// <summary>Names and aliases are unique, and no alias equals another Teammate's Name, as the Persona loader requires.</summary>
    [Fact]
    public void Plan_TeammateNamesAndAliasesAreUnique()
    {
        SeedPlan plan = Plan();

        Assert.Equal(plan.Teammates.Count, plan.Teammates.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(plan.Teammates.Count, plan.Teammates.Select(t => t.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(plan.Teammates, t => Assert.DoesNotContain(plan.Teammates.Where(o => o != t), o => string.Equals(o.Name, t.Alias, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>All eight statuses appear, and roughly forty Tasks are seeded.</summary>
    [Fact]
    public void Plan_CoversEveryTaskStatus()
    {
        SeedPlan plan = Plan();

        Assert.Equal(41, plan.Tasks.Count);
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

    /// <summary>Every reference between Tasks, Teammates, Teams and Projects points at something that exists.</summary>
    [Fact]
    public void Plan_ReferencesResolve()
    {
        SeedPlan plan = Plan();
        HashSet<string> ids = [.. plan.Tasks.Select(t => t.Id)];
        HashSet<string> people = [plan.HumanName, .. plan.Teammates.Select(t => t.Name)];

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

    /// <summary>A finished Task is closed and an unfinished one is open, so the <c>_closed</c> folder always agrees with the status.</summary>
    [Fact]
    public void Plan_ClosedMeansFinished()
    {
        Assert.All(Plan().Tasks, task => Assert.Equal(ManifestWriter.IsFinished(task.Status), task.Closed));
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
        Assert.Contains(plan.Files, f => f.RelativePath.StartsWith("Teammates/Alan/work/", StringComparison.Ordinal) && !f.RelativePath.Contains("/memory/", StringComparison.Ordinal));
    }

    /// <summary>Team memory, Project memory and Teammate memory each exist.</summary>
    [Fact]
    public void Plan_HasThreeKindsOfMemory()
    {
        SeedPlan plan = Plan();

        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teams/[^/]+/memory/[^/]+\\.md$"));
        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teams/[^/]+/[^/]+/memory/[^/]+\\.md$"));
        Assert.Contains(plan.Files, f => Regex.IsMatch(f.RelativePath, "^Teammates/[^/]+/work/memory/[^/]+\\.md$"));
    }

    /// <summary>Six Rooms: two one-to-one, three groups and one archived; one custom-named; transcripts of 8 to 15 messages.</summary>
    [Fact]
    public void Plan_HasTheAgreedRooms()
    {
        SeedPlan plan = Plan();

        Assert.Equal(["Grace", "Owen", "Platform standup", "Launch crew", "Q2 retro", "Incident 0412 war room"], plan.Rooms.Select(r => r.Name));
        Assert.Equal(2, plan.Rooms.Count(r => r.Members.Count == 1));
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
            Assert.All(room.Members, m => Assert.Contains(plan.Teammates, t => t.Name == m));
            Assert.All(room.Messages, m => Assert.True(m.Sender == plan.HumanName || room.Members.Contains(m.Sender), $"{room.Key}: {m.Sender}"));
            DateTime[] times = [.. room.Messages.Select(m => plan.Today.AddDays(-m.DaysAgo).ToDateTime(TimeOnly.ParseExact(m.Time, "HH:mm", System.Globalization.CultureInfo.InvariantCulture)))];
            Assert.Equal([.. times.Order()], times);
            Assert.All(room.Messages.SelectMany(m => TaskIdPattern().Matches(m.Text).Select(x => x.Value)), id => Assert.Contains(id, ids));
        });
        Assert.Contains(plan.Rooms, r => r.Messages.Any(m => TaskIdPattern().IsMatch(m.Text)));
        Assert.Contains(plan.Rooms, r => r.Members.Count > 1 && r.Messages.Any(m => m.Text.Contains('@', StringComparison.Ordinal)));
    }

    /// <summary>Every view id is unique and a saved View exists for a list, a board and a closed scope.</summary>
    [Fact]
    public void Plan_ViewsCoverListBoardAndClosed()
    {
        using System.Text.Json.JsonDocument views = System.Text.Json.JsonDocument.Parse(Plan().ViewsJson);
        System.Text.Json.JsonElement[] items = [.. views.RootElement.GetProperty("views").EnumerateArray()];

        Assert.Equal(items.Length, items.Select(v => v.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(items, v => v.GetProperty("kind").GetString() == "board");
        Assert.Contains(items, v => v.GetProperty("kind").GetString() == "list");
        Assert.Contains(items, v => v.GetProperty("scope").GetString() == "closed");
    }
}

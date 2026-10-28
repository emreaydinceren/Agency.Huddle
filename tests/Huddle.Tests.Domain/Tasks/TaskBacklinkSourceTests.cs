using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Pins <see cref="TaskBacklinkSource.Scan"/>: an open Task links to a document by an attached path or a wikilink in its description.</summary>
public sealed class TaskBacklinkSourceTests
{
    /// <summary>A Team with a document <c>plan.md</c> and a Task file under its <c>_tasks/</c> folder, over a temp Library.</summary>
    private sealed class Library : IDisposable
    {
        private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        private readonly string teamFolder;

        public Library()
        {
            this.teamFolder = Path.Combine(this.fixture.DataDir, "Teams", "Platform");
            Directory.CreateDirectory(Path.Combine(this.teamFolder, "_tasks"));
            File.WriteAllText(Path.Combine(this.teamFolder, "plan.md"), "# Plan\n");
            File.WriteAllText(Path.Combine(this.teamFolder, "other.md"), "# Other\n");
            this.fixture.Reload();
        }

        /// <summary>The absolute path of <c>plan.md</c>.</summary>
        public string PlanFullPath => Path.Combine(this.teamFolder, "plan.md");

        /// <summary>The absolute path of <c>other.md</c>.</summary>
        public string OtherFullPath => Path.Combine(this.teamFolder, "other.md");

        /// <summary>Makes a Task whose file exists under <c>_tasks/</c> (or <c>_tasks/_closed/</c>).</summary>
        public TaskItem Task(string id, string description, bool closed = false)
        {
            string folder = closed ? Path.Combine(this.teamFolder, "_tasks", "_closed") : Path.Combine(this.teamFolder, "_tasks");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, id + ".md");
            File.WriteAllText(path, "---\n---\n");
            return TestTasks.Make(id: id, title: "Scope " + id, description: description, location: new TaskLocation("Platform", null, closed), path: path);
        }

        /// <summary>Scans <paramref name="tasks"/> for links to <c>plan.md</c>.</summary>
        public List<TaskBacklink> ScanPlan(params TaskItem[] tasks) =>
            TaskBacklinkSource.Scan(tasks, this.fixture.ResolveTeams("Platform/plan.md"), this.fixture.Resolver, this.fixture.Index);

        public void Dispose() => this.fixture.Dispose();
    }

    /// <summary>A Task with the document's absolute path alone in a code span is listed, with that path as its line.</summary>
    [Fact]
    public void Scan_AttachedPath_ListsTheTask()
    {
        using Library library = new();
        TaskItem task = library.Task("PLAT-0001", $"Context\n\n`{library.PlanFullPath}`");

        List<TaskBacklink> rows = library.ScanPlan(task);

        TaskBacklink row = Assert.Single(rows);
        Assert.Equal(task.Id, row.Id);
        Assert.Equal(library.PlanFullPath, row.LineText);
    }

    /// <summary>A Task whose description holds a <c>[[wikilink]]</c> that resolves to the document is listed, with the line that holds it.</summary>
    [Fact]
    public void Scan_Wikilink_ListsTheTask()
    {
        using Library library = new();
        TaskItem task = library.Task("PLAT-0001", "Intro\nSee [[plan]] for the scope\n");

        List<TaskBacklink> rows = library.ScanPlan(task);

        Assert.Equal("See [[plan]] for the scope", Assert.Single(rows).LineText);
    }

    /// <summary>A closed Task, one under <c>_closed/</c>, is not listed even though it links.</summary>
    [Fact]
    public void Scan_ClosedTask_IsNotListed()
    {
        using Library library = new();
        TaskItem task = library.Task("PLAT-0001", $"`{library.PlanFullPath}`", closed: true);

        Assert.Empty(library.ScanPlan(task));
    }

    /// <summary>A Task that links to another document, or names the path inside prose rather than alone in a code span, is not listed.</summary>
    [Fact]
    public void Scan_OtherDocumentOrPathInProse_IsNotListed()
    {
        using Library library = new();
        TaskItem otherDocument = library.Task("PLAT-0001", $"`{library.OtherFullPath}`\nSee [[other]]");
        TaskItem inProse = library.Task("PLAT-0002", $"Read `{library.PlanFullPath}` first");

        Assert.Empty(library.ScanPlan(otherDocument, inProse));
    }

    /// <summary>A Task that links twice is one row, for the first line that links.</summary>
    [Fact]
    public void Scan_LinksTwice_IsOneRowForTheFirstLine()
    {
        using Library library = new();
        TaskItem task = library.Task("PLAT-0001", $"First [[plan]]\n`{library.PlanFullPath}`");

        List<TaskBacklink> rows = library.ScanPlan(task);

        Assert.Equal("First [[plan]]", Assert.Single(rows).LineText);
    }
}

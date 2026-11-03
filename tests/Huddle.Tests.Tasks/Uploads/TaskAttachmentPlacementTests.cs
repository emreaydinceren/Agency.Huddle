using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Where a Task's attachments live (Second Brain Spec 6.1, SB-R04, correction C4): under the Task's own Team and
/// Project, in <c>raw/Tasks/&lt;ID&gt;</c>, found through <see cref="TaskStore"/>.
/// </summary>
public sealed class TaskAttachmentPlacementTests
{
    /// <summary>A Task in a Project is filed under the Team's Project folder.</summary>
    [Fact]
    public void For_TaskInProject_ResolvesUnderTeamProjectRaw()
    {
        using Rig rig = new();

        LibraryPath? folder = rig.Folders.For("GROW-0002");

        Assert.NotNull(folder);
        Assert.Equal("teams", folder.Root.Id);
        Assert.Equal(Path.Combine(rig.Dir.Path, "Teams", "Marketing", "Growth", "raw", "Tasks", "GROW-0002"), folder.FullPath);
    }

    /// <summary>A Team-level Task is filed under the Team's own raw folder.</summary>
    [Fact]
    public void For_TeamLevelTask_ResolvesUnderTeamRaw()
    {
        using Rig rig = new();

        LibraryPath? folder = rig.Folders.For("HOME-0001");

        Assert.NotNull(folder);
        Assert.Equal("teams", folder.Root.Id);
        Assert.Equal(Path.Combine(rig.Dir.Path, "Teams", "Home", "raw", "Tasks", "HOME-0001"), folder.FullPath);
    }

    /// <summary>A well-formed id that no Task has resolves to nothing.</summary>
    [Fact]
    public void For_UnknownTask_IsNull()
    {
        using Rig rig = new();

        Assert.Null(rig.Folders.For("GROW-0099"));
    }

    /// <summary>A string that is not a Task id resolves to nothing.</summary>
    [Theory]
    [InlineData("not-a-task")]
    [InlineData("..")]
    [InlineData("a/b")]
    public void For_InvalidId_IsNull(string id)
    {
        using Rig rig = new();

        Assert.Null(rig.Folders.For(id));
    }

    /// <summary>Two Tasks on disk, a Library over the same folder, and the folders under test.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly PersonaStore personas;
        private readonly TaskStore store;
        private readonly LibraryFileServiceFixture library;

        /// <summary>Writes <c>GROW-0002</c> (Marketing, Growth) and <c>HOME-0001</c> (Home, no Project) and loads them.</summary>
        public Rig()
        {
            this.Dir = new TempDataDir();
            string root = TestTaskStore.Root(this.Dir);
            _ = TestTaskStore.WriteTask(
                root,
                TestTaskStore.RelativePath("Marketing", "Growth", false, "GROW-0002.md"),
                TestTasks.Make(id: "GROW-0002", location: new("Marketing", "Growth", false)));
            _ = TestTaskStore.WriteTask(
                root,
                TestTaskStore.RelativePath("Home", null, false, "HOME-0001.md"),
                TestTasks.Make(id: "HOME-0001", location: new("Home", null, false)));

            this.personas = this.Dir.CreatePersonaStore();
            _ = this.personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Marketing", "Home"]), "You are Nova.");
            this.store = new TaskStore(this.Dir.Options(), this.personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
            this.library = LibraryFileServiceFixture.Attach(this.Dir.Path);
            this.Folders = new TaskUploadFolders(this.library.Resolver, this.store);
        }

        /// <summary>The temp data directory.</summary>
        public TempDataDir Dir { get; }

        /// <summary>The folders under test.</summary>
        public TaskUploadFolders Folders { get; }

        /// <summary>Disposes everything the rig built.</summary>
        public void Dispose()
        {
            this.library.Dispose();
            this.store.Dispose();
            this.personas.Dispose();
            this.Dir.Dispose();
        }
    }
}

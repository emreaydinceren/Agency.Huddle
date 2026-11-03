using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests that <see cref="TeamFolderCatalog.List"/> never lists the Second Brain reserved
/// folders <c>memory</c> and <c>raw</c> as Teams (Spec §6.1, SB-R02).</summary>
public sealed class TeamFolderCatalogReservedTests
{
    /// <summary>A Teams root holding <c>memory/</c>, <c>Raw/</c> and <c>Home/</c> lists only <c>Home</c>.</summary>
    [Fact]
    public void List_ReservedFolders_AreNotTeams()
    {
        using TempDataDir dir = new();
        string teamsPath = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(teamsPath);
        Directory.CreateDirectory(Path.Combine(dir.Path, "Agents"));
        Directory.CreateDirectory(Path.Combine(teamsPath, "memory"));
        Directory.CreateDirectory(Path.Combine(teamsPath, "Raw"));
        Directory.CreateDirectory(Path.Combine(teamsPath, "Home"));

        IOptions<TeamOptions> options = dir.Options();
        PersonaStore personas = dir.CreatePersonaStore();
        AgentPaths agentPaths = new(options);
        LibraryRootStore rootStore = new(options, agentPaths, NullLogger<LibraryRootStore>.Instance);
        LibraryPathResolver resolver = new(rootStore, options, NullLogger<LibraryPathResolver>.Instance);
        Assert.True(resolver.TryResolve("teams", string.Empty, out LibraryPath? teamsRoot, out _));
        Assert.NotNull(teamsRoot);
        TaskStore taskStore = TestTaskStore.CreateTaskStore(dir, personas);

        IReadOnlyList<TeamFolder> folders = TeamFolderCatalog.List(resolver, teamsRoot, taskStore.Teams);

        TeamFolder folder = Assert.Single(folders);
        Assert.Equal("Home", folder.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// <see cref="TaskStore.Teams"/>, which the Team catalog reads, lists no reserved folder either: the Huddle-wide
    /// <c>Teams/memory/</c> showed in the sidebar and the Task form as a Team called "memory".
    /// </summary>
    [Fact]
    public void TaskStoreTeams_ReservedFolders_AreNotTeams()
    {
        using TempDataDir dir = new();
        string teamsPath = Path.Combine(dir.Path, "Teams");
        Directory.CreateDirectory(Path.Combine(teamsPath, "memory"));
        Directory.CreateDirectory(Path.Combine(teamsPath, "Outputs"));
        Directory.CreateDirectory(Path.Combine(teamsPath, "Home"));
        PersonaStore personas = dir.CreatePersonaStore();

        TaskStore taskStore = TestTaskStore.CreateTaskStore(dir, personas);

        TeamFolder folder = Assert.Single(taskStore.Teams);
        Assert.Equal("Home", folder.Name, StringComparer.Ordinal);
    }
}

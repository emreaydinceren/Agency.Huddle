using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Covers the Tasks root switching from the retired <c>Team:Tasks:Dir</c> key to
/// <c>Team:Teams:Dir</c> (Library Task G1.2): <see cref="TaskStore.RootDirectory"/> now resolves
/// under <see cref="TeamsOptions.Dir"/>, the old key is rejected at startup like the
/// <c>Acp:PersonaDir</c> and <c>Acp:TeamsDir</c> guards, and the constructor's overlap check
/// reports <see cref="Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates"/>'s message.
/// </summary>
public sealed class TasksRootTests
{
    /// <summary><see cref="TaskStore.RootDirectory"/> resolves under the default <see cref="TeamsOptions.Dir"/>, not the retired <c>TasksOptions.Dir</c>.</summary>
    [Fact]
    public void RootDirectory_IsTeamsDir()
    {
        using TempDataDir dir = new();
        IOptions<TeamOptions> options = dir.Options();
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        string expectedRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));

        using TaskStore store = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        Assert.Equal(expectedRoot, store.RootDirectory);
    }

    /// <summary>A non-default <see cref="TeamsOptions.Dir"/> is honoured as the Tasks scan root.</summary>
    [Fact]
    public void RootDirectory_HonoursTeamsDir()
    {
        using TempDataDir dir = new();
        IOptions<TeamOptions> options = dir.Options();
        options.Value.Teams.Dir = "T2";
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
        string expectedRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, "T2"));

        using TaskStore store = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

        Assert.Equal(expectedRoot, store.RootDirectory);
    }

    /// <summary>
    /// <c>Team:Tasks:Dir</c> is retired: Tasks now live in each Team folder's <c>_tasks/</c>
    /// folder, so a value left behind must fail loudly rather than silently binding to nothing.
    /// </summary>
    [Fact]
    public void AddTeam_WithTasksDir_Throws()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Team:Tasks:Dir"] = "Work",
            })
            .Build();

        ServiceCollection services = new();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => services.AddTeamServices(configuration));

        Assert.Equal(
            "Configuration key 'Team:Tasks:Dir' was replaced by 'Team:Teams:Dir'. Tasks now live in each Team folder's _tasks/ folder; remove the key (the start-up migration reads {DataDir}/Tasks). There is no automatic fallback.",
            ex.Message);
    }

    /// <summary>
    /// The constructor's overlap guard reports <see cref="Agency.Huddle.App.Library.LayoutGuard.ValidateTeamsAndTeammates"/>'s
    /// message, not the retired <c>Team:Tasks:Dir</c> wording, when <c>Team:Teams:Dir</c> nests inside <c>Team:Acp:TeammatesDir</c>.
    /// </summary>
    [Fact]
    public void Constructor_TeamsInsideTeammates_ThrowsLayoutGuardMessage()
    {
        using TempDataDir dir = new();
        IOptions<TeamOptions> options = dir.Options();
        options.Value.Acp.TeammatesDir = Path.Combine(options.Value.Teams.Dir, "Sub");
        using PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);

        string expectedTeamsRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Teams.Dir));
        string expectedTeammatesRoot = Path.GetFullPath(Path.Combine(options.Value.DataDir, options.Value.Acp.TeammatesDir));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new TaskStore(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance));

        Assert.Equal(
            $"'Team:Teams:Dir' ({expectedTeamsRoot}) and 'Team:Acp:TeammatesDir' ({expectedTeammatesRoot}) must not overlap.",
            exception.Message);
    }
}

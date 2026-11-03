using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Pins which Project names <see cref="TaskService.Create"/> refuses: the Team's reserved
/// <c>raw</c> and <c>outputs</c> folders, each with its own settled text, while <c>Uploads</c> is an
/// ordinary Project name (Second Brain, task 1.4).
/// </summary>
public sealed class ProjectNameTests
{
    private static readonly TaskActor HumanActor = new(TaskActorKind.Human, "You", KnownIds.Human);

    /// <summary>A Task in Project <c>raw</c> (any case) is refused with exactly the raw text and nothing is created.</summary>
    [Theory]
    [InlineData("raw")]
    [InlineData("RAW")]
    public void Create_ProjectRaw_RefusedWithExactlyRawReservedProblem(string project)
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", project), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([TeamNames.RawReservedProblem], refused.Problems);
        Assert.Empty(store.All);
    }

    /// <summary>A Task in Project <c>outputs</c> (any case) is refused with exactly the outputs text and nothing is created.</summary>
    [Theory]
    [InlineData("outputs")]
    [InlineData("OUTPUTS")]
    public void Create_ProjectOutputs_RefusedWithExactlyOutputsReservedProblem(string project)
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", project), HumanActor);

        TaskResult.Refused refused = Assert.IsType<TaskResult.Refused>(result);
        Assert.Equal([TeamNames.OutputsReservedProblem], refused.Problems);
        Assert.False(Directory.Exists(Path.Combine(TestTaskStore.Root(dir), "Platform", project)));
        Assert.Empty(store.All);
    }

    /// <summary>A Task in Project <c>Uploads</c> is allowed: that name is no longer reserved.</summary>
    [Fact]
    public void Create_ProjectUploads_IsSaved()
    {
        using TempDataDir dir = new();
        using PersonaStore personas = CreatePersonaStore(dir);
        using TaskStore store = CreateTaskStore(dir, personas);
        TaskService service = CreateTaskService(dir, store, personas);

        TaskResult result = service.Create(new TaskDraft("T", "Platform", "Uploads"), HumanActor);

        TaskResult.Saved saved = Assert.IsType<TaskResult.Saved>(result);
        Assert.Equal("Uploads", saved.Task.Location.Project);
    }

    /// <summary>Constructs a real <see cref="PersonaStore"/> with one Persona in Team Platform over the shared temp data dir.</summary>
    private static PersonaStore CreatePersonaStore(TempDataDir dir)
    {
        PersonaStore personas = new(new AgentPaths(dir.Options()), new PersonaModelStore(dir.Options()), new PersonaEffortStore(dir.Options()), new PersonaWorkModeStore(dir.Options()), NullLogger<PersonaStore>.Instance);
        _ = personas.Add(new PersonaIdentity("Nova", "Nova", "nova", ["Platform"]), "You are Nova.");
        return personas;
    }

    /// <summary>Constructs the <see cref="TaskStore"/> under test.</summary>
    private static TaskStore CreateTaskStore(TempDataDir dir, PersonaStore personas) =>
        new(dir.Options(), personas, TimeProvider.System, NullLogger<TaskStore>.Instance);

    /// <summary>Constructs the <see cref="TaskService"/> under test.</summary>
    private static TaskService CreateTaskService(TempDataDir dir, TaskStore store, PersonaStore personas) =>
        new(store, new TaskIdAllocator(dir.Options()), new TaskEvents(), personas, new EmptySprintCatalog(), dir.Options(), TimeProvider.System, NullLogger<TaskService>.Instance);
}

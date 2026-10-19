using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Factories;

/// <summary>Helpers on <see cref="TaskToolHarness"/> that create Tasks and read the seeded Nova, replacing the per-file copies.</summary>
internal static class TaskToolHarnessExtensions
{
    /// <summary>Creates a Task through the harness's real <see cref="TaskService"/> as the Human, and returns the saved Task.</summary>
    /// <param name="harness">The harness whose service saves the Task.</param>
    /// <param name="title">The title.</param>
    /// <param name="team">The owning Team's label.</param>
    /// <param name="project">The Project label, or <see langword="null"/> for a Team-level Task.</param>
    /// <param name="status">The status; <see langword="null"/> gives <see cref="TaskState.ToDo"/>.</param>
    /// <param name="priority">The priority; <see langword="null"/> gives <see cref="TaskPriority.Medium"/>.</param>
    /// <param name="assignee">The assignee's name, or <see langword="null"/> for none.</param>
    /// <param name="parent">The parent Task, or <see langword="null"/>.</param>
    /// <param name="tags">The tags, or <see langword="null"/> for none.</param>
    /// <param name="description">The description; <see langword="null"/> gives an empty one.</param>
    /// <param name="sprint">The Sprint, or <see langword="null"/>.</param>
    /// <param name="dueDate">The due date, or <see langword="null"/> for none.</param>
    public static TaskItem CreateTask(
        this TaskToolHarness harness,
        string title = "T",
        string team = "Platform",
        string? project = null,
        TaskState? status = null,
        TaskPriority? priority = null,
        string? assignee = null,
        TaskId? parent = null,
        IReadOnlyList<string>? tags = null,
        string? description = null,
        SprintId? sprint = null,
        DateOnly? dueDate = null)
    {
        TaskDraft draft = new(
            title,
            team,
            project,
            status ?? TaskState.ToDo,
            priority ?? TaskPriority.Medium,
            assignee,
            Parent: parent,
            Tags: tags,
            Description: description ?? string.Empty,
            Sprint: sprint,
            DueDate: dueDate);
        TaskResult result = harness.Service.Create(draft, harness.HumanActor());
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Returns the seeded Nova user id, failing when the harness was built without <see cref="TaskToolHarness.CreateAsync"/>.</summary>
    /// <param name="harness">The harness to read.</param>
    public static string NovaIdOrThrow(this TaskToolHarness harness) =>
        harness.NovaId ?? throw new InvalidOperationException("The harness was not seeded: build it with TaskToolHarness.CreateAsync.");

    /// <summary>Returns the Human actor for the harness's options.</summary>
    /// <param name="harness">The harness to read.</param>
    public static TaskActor HumanActor(this TaskToolHarness harness) => TaskActors.Human(harness.Options.Value);
}

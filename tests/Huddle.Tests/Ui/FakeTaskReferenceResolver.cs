using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A dictionary-backed fake of <see cref="ITaskReferenceResolver"/>, shared by every bUnit test
/// context that renders <c>MessageList</c> (directly, or through <c>Chat.razor</c>) and so needs the
/// service satisfied even when a test never resolves a Task id.
/// </summary>
internal sealed class FakeTaskReferenceResolver : ITaskReferenceResolver
{
    private readonly Dictionary<TaskId, TaskReference> tasks = [];

    /// <summary>Makes <paramref name="task"/> resolvable by its id.</summary>
    /// <param name="task">The Task reference to add.</param>
    public void Add(TaskReference task) => this.tasks[task.Id] = task;

    /// <inheritdoc />
    public TaskReference? Resolve(TaskId id) => this.tasks.GetValueOrDefault(id);
}

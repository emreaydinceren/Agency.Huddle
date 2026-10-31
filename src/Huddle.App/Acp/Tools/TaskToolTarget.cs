using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>The existing Task a tool call acts on, and the calling Agent acting on it, as <see cref="TaskToolText.StartAsync"/> resolves them.</summary>
/// <param name="Task">The Task the <c>taskId</c> argument names.</param>
/// <param name="Actor">The calling Agent.</param>
internal sealed record TaskToolTarget(TaskItem Task, TaskActor Actor);

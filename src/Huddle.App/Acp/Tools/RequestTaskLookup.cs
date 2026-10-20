using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.App.Acp.Tools;

/// <summary>Helpers the Request tools share: finding a Request Task and turning a <see cref="TaskResult"/> into a refusal.</summary>
internal static class RequestTaskLookup
{
    /// <summary>Resolves <paramref name="text"/> to a Task that carries the Request fields.</summary>
    /// <param name="store">The Task store.</param>
    /// <param name="text">The task id argument.</param>
    /// <param name="task">The Task when the result is true.</param>
    /// <param name="fields">The Task's Request fields when the result is true.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when the result is true.</param>
    /// <returns><see langword="true"/> when the Task exists and is a Request.</returns>
    public static bool TryResolve(
        TaskStore store,
        string? text,
        [NotNullWhen(true)] out TaskItem? task,
        [NotNullWhen(true)] out RequestFields? fields,
        out string refusal)
    {
        ArgumentNullException.ThrowIfNull(store);

        fields = null;
        if (!TaskToolText.TryResolve(store, text, out task, out refusal))
        {
            return false;
        }

        Dictionary<string, string> frontmatter = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> field in task.UnknownFields)
        {
            frontmatter[field.Key] = field.Value;
        }

        if (!RequestFields.TryRead(frontmatter, out fields) || fields is null)
        {
            refusal = string.Create(CultureInfo.InvariantCulture, $"{task.Id} is not a Request.");
            task = null;
            return false;
        }

        return true;
    }

    /// <summary>Reads a <see cref="TaskResult"/> from a write: the saved Task, or the refusal to hand the caller.</summary>
    /// <param name="result">The result of a <see cref="TaskService"/> write.</param>
    /// <param name="task">The saved Task when the result is true.</param>
    /// <param name="refusal">The refusal text, or <see cref="string.Empty"/> when the result is true.</param>
    /// <returns><see langword="true"/> when the write was saved.</returns>
    public static bool TrySaved(TaskResult? result,[NotNullWhen(true)] out TaskItem? task, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(result);

        switch (result)
        {
            case TaskResult.Saved saved:
                task = saved.Task;
                refusal = string.Empty;
                return true;
            case TaskResult.Unchanged unchanged:
                task = unchanged.Task;
                refusal = string.Empty;
                return true;
            default:
                if (!TaskToolText.TryDescribeRefusal(result, out refusal))
                {
                    throw new UnreachableException("TaskService writes return only Saved, Unchanged, Refused, GateRefused, Conflict or NotFound.");
                }

                task = null;
                return false;
        }
    }
}

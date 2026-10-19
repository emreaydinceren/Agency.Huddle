using Agency.Huddle.Seeder.Model;
using System.Globalization;
using System.Text;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>Turns a <see cref="SeedTask"/> into the Markdown the Tasks feature reads, and decides where it goes.</summary>
internal static class TaskFileWriter
{
    private const string Empty = "—";

    /// <summary>The path of the Task file below the data folder, using <c>/</c>.</summary>
    /// <param name="task">The Task.</param>
    /// <returns>For example <c>Teams/Platform/API v2/_tasks/_closed/PLAT-0001.md</c>.</returns>
    internal static string RelativePath(SeedTask task)
    {
        List<string> parts = ["Teams", task.Location.Team];
        if (task.Location.Project is not null)
        {
            parts.Add(task.Location.Project);
        }

        parts.Add("_tasks");
        if (task.Closed)
        {
            parts.Add("_closed");
        }

        parts.Add(task.Id + ".md");
        return string.Join('/', parts);
    }

    /// <summary>
    /// Builds the Change log. The first entry is the Created date, the last is the Updated date, and a closed Task
    /// ends with a <c>closed</c> entry, which is what the app reads as the Closed date.
    /// </summary>
    /// <param name="task">The Task.</param>
    /// <param name="today">The run date the offsets are measured from.</param>
    /// <returns>The entries, oldest first, spread evenly between the creation and last-touched times.</returns>
    internal static IReadOnlyList<TaskLogLine> BuildLog(SeedTask task, DateOnly today)
    {
        string worker = task.Assignee ?? task.Creator;
        List<(string Actor, string Summary)> steps = [(task.Creator, "created")];

        if (task.Assignee is not null)
        {
            steps.Add((task.Creator, $"assignee: {Empty} → {task.Assignee}"));
        }

        foreach (string tag in task.Tags)
        {
            steps.Add((task.Creator, $"tags: +{tag}"));
        }

        foreach (string change in StatusChanges(task.Status))
        {
            steps.Add((worker, $"status: {change}"));
        }

        foreach (SeedLogNote note in task.ExtraLog)
        {
            steps.Add((note.Actor, note.Summary));
        }

        if (task.Closed)
        {
            steps.Add((worker, "closed"));
        }

        DateTimeOffset start = At(today, -task.CreatedDaysAgo, "09:00");
        DateTimeOffset end = At(today, -task.LastTouchedDaysAgo, "16:00");
        List<TaskLogLine> log = new(steps.Count);
        for (int i = 0; i < steps.Count; i++)
        {
            DateTimeOffset at = steps.Count == 1 ? start : start + ((end - start) * i / (steps.Count - 1));
            log.Add(new TaskLogLine(new DateTimeOffset(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, TimeSpan.Zero), steps[i].Actor, steps[i].Summary));
        }

        return log;
    }

    /// <summary>Renders the whole Task file: front matter, description and Change log.</summary>
    /// <param name="task">The Task.</param>
    /// <param name="today">The run date the offsets are measured from.</param>
    /// <param name="originRoomId">The id of the Room named by <see cref="SeedTask.OriginRoom"/>, or <see langword="null"/>.</param>
    /// <param name="dataDir">The data folder, which turns <see cref="SeedTask.Attachments"/> into absolute paths.</param>
    /// <returns>The file text, ending in a newline.</returns>
    internal static string Render(SeedTask task, DateOnly today, string? originRoomId, string dataDir)
    {
        StringBuilder text = new();
        text.Append("---\n");
        text.Append("id: ").Append(task.Id).Append('\n');
        text.Append("title: ").Append(Scalar(task.Title)).Append('\n');
        text.Append("status: ").Append(task.Status).Append('\n');
        text.Append("priority: ").Append(task.Priority).Append('\n');
        text.Append("creator: ").Append(Scalar(task.Creator)).Append('\n');
        AppendIfPresent(text, "assignee", task.Assignee is null ? null : Scalar(task.Assignee));
        AppendIfPresent(text, "origin", originRoomId);
        AppendIfPresent(text, "parent", task.Parent);
        if (task.BlockedBy.Count > 0)
        {
            text.Append("blocked_by: [").Append(string.Join(", ", task.BlockedBy)).Append("]\n");
        }

        AppendIfPresent(text, "duplicate_of", task.DuplicateOf);
        if (task.Tags.Count > 0)
        {
            text.Append("tags:\n");
            foreach (string tag in task.Tags)
            {
                text.Append("  - ").Append(tag).Append('\n');
            }
        }

        AppendIfPresent(text, "start_date", task.StartOffset is { } start ? Date(today, start) : null);
        AppendIfPresent(text, "due_date", task.DueOffset is { } due ? Date(today, due) : null);
        AppendIfPresent(text, "sprint", task.Sprint);
        if (task.Request is { } request)
        {
            text.Append("request: ").Append(request.Kind).Append('\n');
            text.Append("from_team: ").Append(Scalar(request.FromTeam)).Append('\n');
            text.Append("requester: ").Append(Scalar(request.Requester)).Append('\n');
            text.Append("triage: ").Append(Scalar(request.Triage)).Append('\n');
            text.Append("decline_reason: ").Append(Scalar(request.DeclineReason)).Append('\n');
            text.Append("confirmed_by: ").Append(Scalar(request.ConfirmedBy)).Append('\n');
        }

        text.Append("---\n");

        string body = Description(task, dataDir);
        if (body.Length > 0)
        {
            text.Append(body).Append("\n\n");
        }

        text.Append("## Change log\n");
        foreach (TaskLogLine line in BuildLog(task, today))
        {
            text.Append("- ")
                .Append(line.At.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))
                .Append(" | ").Append(line.Actor).Append(" | ").Append(line.Summary).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Formats a run-date offset as <c>yyyy-MM-dd</c>.</summary>
    /// <param name="today">The run date.</param>
    /// <param name="offset">Whole days from the run date.</param>
    /// <returns>The date text.</returns>
    internal static string Date(DateOnly today, int offset) =>
        today.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// The description: the body, then each attachment as its own code-span line holding the absolute path. Mirrors
    /// <c>TaskAttachmentText.AppendPathLine</c>: a blank line separates the run of paths from the prose, and the paths follow
    /// one another directly.
    /// </summary>
    /// <param name="task">The Task.</param>
    /// <param name="dataDir">The data folder the attachment paths are below.</param>
    /// <returns>The description without trailing line breaks; empty when the Task has neither.</returns>
    internal static string Description(SeedTask task, string dataDir)
    {
        StringBuilder body = new(task.Body.ReplaceLineEndings("\n").Trim());
        bool afterPath = false;
        foreach (string attachment in task.Attachments)
        {
            if (body.Length > 0)
            {
                body.Append(afterPath ? "\n" : "\n\n");
            }

            body.Append('`').Append(SeedIo.Resolve(dataDir, attachment)).Append('`');
            afterPath = true;
        }

        return body.ToString();
    }

    /// <summary>A UTC instant on a run-date offset.</summary>
    /// <param name="day">The run date.</param>
    /// <param name="offset">Whole days from the run date.</param>
    /// <param name="time">The UTC time of day, <c>HH:mm</c>.</param>
    /// <returns>The instant.</returns>
    internal static DateTimeOffset At(DateOnly day, int offset, string time)
    {
        TimeOnly timeOfDay = TimeOnly.ParseExact(time, "HH:mm", CultureInfo.InvariantCulture);
        return new DateTimeOffset(day.AddDays(offset).ToDateTime(timeOfDay), TimeSpan.Zero);
    }

    private static void AppendIfPresent(StringBuilder text, string key, string? value)
    {
        if (value is not null)
        {
            text.Append(key).Append(": ").Append(value).Append('\n');
        }
    }

    private static IEnumerable<string> StatusChanges(string status) => status switch
    {
        "In Progress" => ["To Do → In Progress"],
        "Review" => ["To Do → In Progress", "In Progress → Review"],
        "Done" => ["To Do → In Progress", "In Progress → Review", "Review → Done"],
        "Cancelled" or "Rejected" or "Duplicate" => [$"To Do → {status}"],
        _ => [],
    };

    private static string Scalar(string value)
    {
        bool needsQuotes = value.Length == 0 || value.IndexOfAny([':', '#', '\'', '"', '[', ']', '{', '}']) >= 0;
        return needsQuotes ? "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'" : value;
    }
}

namespace Agency.Huddle.Seeder.Model;

/// <summary>
/// A Task file. Dates are whole-day offsets from the run date so that "overdue" and "due soon" stay true
/// whenever the seed is built.
/// </summary>
internal sealed record SeedTask
{
    /// <summary>The Task id, such as <c>PLAT-0001</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The Team and Project folder the Task file lives in.</summary>
    public required SeedLocation Location { get; init; }

    /// <summary>The one-line title.</summary>
    public required string Title { get; init; }

    /// <summary>One of the eight statuses.</summary>
    public required string Status { get; init; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public required string Priority { get; init; }

    /// <summary>The Human's name or an Agent's name.</summary>
    public required string Creator { get; init; }

    /// <summary>The Agent or Human it is assigned to, or <see langword="null"/> when unassigned.</summary>
    public string? Assignee { get; init; }

    /// <summary>Whether the file lives in the <c>_closed</c> folder.</summary>
    public bool Closed { get; init; }

    /// <summary>Tags, each 1–40 characters.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Days from the run date until the due date; negative is in the past.</summary>
    public int? DueOffset { get; init; }

    /// <summary>Days from the run date until the start date.</summary>
    public int? StartOffset { get; init; }

    /// <summary>The parent Task id, which makes this a sub-task.</summary>
    public string? Parent { get; init; }

    /// <summary>Ids of the Tasks that block this one.</summary>
    public IReadOnlyList<string> BlockedBy { get; init; } = [];

    /// <summary>The Task this one duplicates; required when the status is Duplicate.</summary>
    public string? DuplicateOf { get; init; }

    /// <summary>The key of the Room this Task came from.</summary>
    public string? OriginRoom { get; init; }

    /// <summary>The Markdown description.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// Files attached to the Task, as paths below the data folder: a Library file it links, or a <see cref="SeedTaskUpload.RelativePath"/>.
    /// Each is written after <see cref="Body"/> as its own code-span line with the absolute path, the form the Task's attach buttons write.
    /// </summary>
    public IReadOnlyList<string> Attachments { get; init; } = [];

    /// <summary>How many days before the run date the Task was created.</summary>
    public int CreatedDaysAgo { get; init; } = 14;

    /// <summary>How many days before the run date the last Change log entry was written; at least 1.</summary>
    public int LastTouchedDaysAgo { get; init; } = 1;

    /// <summary>Extra Change log lines, written between the status changes and the closing entry.</summary>
    public IReadOnlyList<SeedLogNote> ExtraLog { get; init; } = [];

    /// <summary>Whether the Task requires the Human's acceptance (<c>human_gate: true</c>).</summary>
    public bool HumanGate { get; init; }

    /// <summary>The Sprint id the Task is in, such as <c>S2</c>, or <see langword="null"/>.</summary>
    public string? Sprint { get; init; }

    /// <summary>
    /// The title the Task had when Messages first referenced it. When set, a Message's Task reference snapshots this instead of
    /// <see cref="Title"/>, so the reference chip reads "changed".
    /// </summary>
    public string? FormerTitle { get; init; }

    /// <summary>The Request fields, or <see langword="null"/> for an ordinary Task.</summary>
    public SeedTaskRequest? Request { get; init; }
}

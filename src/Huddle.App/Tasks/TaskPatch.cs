namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Distinguishes "leave this field alone" (<see langword="default"/>, <see cref="IsSet"/> false)
/// from "set this field to a value, possibly null" (<see cref="Set(T)"/>) - Spec §9.1. A plain
/// nullable can't carry that distinction for a field whose "clear" value is itself
/// <see langword="null"/>, such as <see cref="TaskPatch.Assignee"/>.
/// </summary>
/// <typeparam name="T">The type of value carried when set.</typeparam>
/// <param name="IsSet">Whether this field is being changed at all.</param>
/// <param name="Value">The new value, meaningful only when <paramref name="IsSet"/> is <see langword="true"/>.</param>
internal readonly record struct Optional<T>(bool IsSet, T Value)
{
    /// <summary>Creates a set <see cref="Optional{T}"/> carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value to set the field to.</param>
    public static Optional<T> Set(T value) => new(true, value);
}

/// <summary>
/// Only the fields being changed are non-null (or, for a field whose "clear" state is itself
/// <see langword="null"/>, set through an <see cref="Optional{T}"/>) - Spec §9.1. Passed to
/// <see cref="TaskService.Update(TaskId, TaskPatch, string?, TaskActor)"/>.
/// </summary>
internal sealed record TaskPatch
{
    /// <summary>The Task's new title, or <see langword="null"/> to leave it unchanged.</summary>
    public string? Title { get; init; }

    /// <summary>The Task's new state, or <see langword="null"/> to leave it unchanged.</summary>
    public TaskState? Status { get; init; }

    /// <summary>The Task's new priority, or <see langword="null"/> to leave it unchanged.</summary>
    public TaskPriority? Priority { get; init; }

    /// <summary>The Task's new assignee. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> unassigns it.</summary>
    public Optional<string?> Assignee { get; init; }

    /// <summary>The Task's new parent. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it.</summary>
    public Optional<TaskId?> Parent { get; init; }

    /// <summary>The Task's whole new blocked-by list, replacing the old one, or <see langword="null"/> to leave it unchanged.</summary>
    public IReadOnlyList<TaskId>? BlockedBy { get; init; }

    /// <summary>What the Task is now a duplicate of. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it.</summary>
    public Optional<TaskId?> DuplicateOf { get; init; }

    /// <summary>The Task's whole new tag list, replacing the old one, or <see langword="null"/> to leave it unchanged.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>The Task's new start date. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it.</summary>
    public Optional<DateOnly?> StartDate { get; init; }

    /// <summary>The Task's new due date. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it.</summary>
    public Optional<DateOnly?> DueDate { get; init; }

    /// <summary>The Task's new description, or <see langword="null"/> to leave it unchanged.</summary>
    public string? Description { get; init; }

    /// <summary>The Task's new Team, or <see langword="null"/> to leave it unchanged.</summary>
    public string? Team { get; init; }

    /// <summary>The Task's new Project. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> moves it to the Team root.</summary>
    public Optional<string?> Project { get; init; }

    /// <summary>A reason recorded alongside a move to Cancelled or Rejected; logged only, never stored as a field.</summary>
    public string? Reason { get; init; }

    /// <summary><see langword="true"/> when this patch sets no field at all, including <see cref="Reason"/>.</summary>
    internal bool IsEmpty =>
        this.Title is null && this.Status is null && this.Priority is null &&
        !this.Assignee.IsSet && !this.Parent.IsSet && this.BlockedBy is null &&
        !this.DuplicateOf.IsSet && this.Tags is null && !this.StartDate.IsSet &&
        !this.DueDate.IsSet && this.Description is null && this.Team is null &&
        !this.Project.IsSet && this.Reason is null;

    /// <summary>
    /// The Task fields this patch actually sets (Settled corrections-B5 "Upstream additions" D6).
    /// <see cref="TaskField.Location"/> stands for either <see cref="Team"/> or <see cref="Project"/>,
    /// since both live on <see cref="TaskItem.Location"/>. Used to scope validation to only the
    /// fields being changed (Settled corrections-B2 D6 item 3) - never compare this patch, or a
    /// <see cref="TaskItem"/> built from it, with <c>==</c>, since several of its fields are lists.
    /// </summary>
    internal IReadOnlyList<TaskField> Fields()
    {
        List<TaskField> fields = [];

        if (this.Title is not null)
        {
            fields.Add(TaskField.Title);
        }

        if (this.Status is not null)
        {
            fields.Add(TaskField.Status);
        }

        if (this.Priority is not null)
        {
            fields.Add(TaskField.Priority);
        }

        if (this.Assignee.IsSet)
        {
            fields.Add(TaskField.Assignee);
        }

        if (this.Parent.IsSet)
        {
            fields.Add(TaskField.Parent);
        }

        if (this.BlockedBy is not null)
        {
            fields.Add(TaskField.BlockedBy);
        }

        if (this.DuplicateOf.IsSet)
        {
            fields.Add(TaskField.DuplicateOf);
        }

        if (this.Tags is not null)
        {
            fields.Add(TaskField.Tags);
        }

        if (this.StartDate.IsSet)
        {
            fields.Add(TaskField.StartDate);
        }

        if (this.DueDate.IsSet)
        {
            fields.Add(TaskField.DueDate);
        }

        if (this.Description is not null)
        {
            fields.Add(TaskField.Description);
        }

        if (this.Team is not null || this.Project.IsSet)
        {
            fields.Add(TaskField.Location);
        }

        return fields;
    }

    /// <summary>
    /// Applies this patch's set fields onto <paramref name="current"/>, leaving every field this
    /// patch doesn't set unchanged (Spec §9.4 step 2). Never validates - <see cref="TaskService"/>
    /// validates the candidate this returns before it's ever written.
    /// </summary>
    /// <param name="current">The Task to apply this patch to.</param>
    internal TaskItem ApplyTo(TaskItem current)
    {
        ArgumentNullException.ThrowIfNull(current);

        string team = this.Team ?? current.Location.Team;
        string? project = this.Project.IsSet ? this.Project.Value : current.Location.Project;

        return current with
        {
            Title = this.Title ?? current.Title,
            Status = this.Status ?? current.Status,
            Priority = this.Priority ?? current.Priority,
            Assignee = this.Assignee.IsSet ? this.Assignee.Value : current.Assignee,
            Parent = this.Parent.IsSet ? this.Parent.Value : current.Parent,
            BlockedBy = this.BlockedBy ?? current.BlockedBy,
            DuplicateOf = this.DuplicateOf.IsSet ? this.DuplicateOf.Value : current.DuplicateOf,
            Tags = this.Tags ?? current.Tags,
            StartDate = this.StartDate.IsSet ? this.StartDate.Value : current.StartDate,
            DueDate = this.DueDate.IsSet ? this.DueDate.Value : current.DueDate,
            Description = this.Description ?? current.Description,
            Location = current.Location with { Team = team, Project = project },
        };
    }
}

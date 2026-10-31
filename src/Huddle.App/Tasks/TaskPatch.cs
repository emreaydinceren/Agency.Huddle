namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Only the fields being changed are non-null (or, for a field whose "clear" state is itself
/// <see langword="null"/>, set through an <see cref="Optional{T}"/>) - Spec §9.1. Passed to
/// <see cref="TaskService.Update(TaskId, TaskPatch, string?, TaskActor, bool)"/>.
/// </summary>
internal sealed record TaskPatch
{
    /// <summary>The frontmatter key that records who approved a Task as a spec; an ordinary unknown field that only the Human may set.</summary>
    internal const string ApprovedByKey = "approved_by";

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

    /// <summary>The Task's new Sprint. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it.</summary>
    public Optional<SprintId?> Sprint { get; init; }

    /// <summary>The Task's new Human gate flag. <see cref="Optional{T}.Set(T)"/> with <see langword="false"/> clears it.</summary>
    public Optional<bool> HumanGate { get; init; }

    /// <summary>The Task's new action line. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it.</summary>
    public Optional<string?> Action { get; init; }

    /// <summary>The Task's new description, or <see langword="null"/> to leave it unchanged.</summary>
    public string? Description { get; init; }

    /// <summary>The Task's new Team, or <see langword="null"/> to leave it unchanged.</summary>
    public string? Team { get; init; }

    /// <summary>The Task's new Project. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> moves it to the Team root.</summary>
    public Optional<string?> Project { get; init; }

    /// <summary>A reason recorded alongside a move to Cancelled or Rejected; logged only, never stored as a field.</summary>
    public string? Reason { get; init; }

    /// <summary>Who approved the Task as a spec; written as the ordinary frontmatter key <c>approved_by</c>. <see cref="Optional{T}.Set(T)"/> with <see langword="null"/> clears it. Only the Human actor may set it.</summary>
    public Optional<string?> ApprovedBy { get; init; }

    /// <summary>
    /// Frontmatter keys to set (or replace) among the Task's non-column fields, such as the Request triage keys. A key in <see cref="ApprovedByKey"/>
    /// is ignored: only <see cref="ApprovedBy"/> sets that. <see langword="null"/> leaves every extra field unchanged.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>>? ExtraFields { get; init; }

    /// <summary><see langword="true"/> when this patch sets no field at all, including <see cref="Reason"/>.</summary>
    internal bool IsEmpty =>
        this.Title is null && this.Status is null && this.Priority is null &&
        !this.Assignee.IsSet && !this.Parent.IsSet && this.BlockedBy is null &&
        !this.DuplicateOf.IsSet && this.Tags is null && !this.StartDate.IsSet &&
        !this.DueDate.IsSet && !this.Sprint.IsSet && !this.HumanGate.IsSet && !this.Action.IsSet && this.Description is null && this.Team is null &&
        !this.Project.IsSet && this.Reason is null && !this.ApprovedBy.IsSet && this.ExtraFields is null;

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
        this.AddCoreFields(fields);
        this.AddRelationFields(fields);
        return fields;
    }

    /// <summary>Adds the title, status, priority, assignee, parent and blocked-by fields this patch sets.</summary>
    private void AddCoreFields(List<TaskField> fields)
    {
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
    }

    /// <summary>Adds the duplicate-of, tags, dates, Sprint, description and location fields this patch sets.</summary>
    private void AddRelationFields(List<TaskField> fields)
    {
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

        if (this.Sprint.IsSet)
        {
            fields.Add(TaskField.Sprint);
        }

        if (this.HumanGate.IsSet)
        {
            fields.Add(TaskField.HumanGate);
        }

        if (this.Action.IsSet)
        {
            fields.Add(TaskField.Action);
        }

        if (this.Description is not null)
        {
            fields.Add(TaskField.Description);
        }

        if (this.Team is not null || this.Project.IsSet)
        {
            fields.Add(TaskField.Location);
        }
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
        IReadOnlyList<KeyValuePair<string, string>> unknownFields = this.ApprovedBy.IsSet ? WithApprovedBy(current.UnknownFields, this.ApprovedBy.Value) : current.UnknownFields;
        if (this.ExtraFields is not null)
        {
            unknownFields = WithExtras(unknownFields, this.ExtraFields);
        }

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
            Sprint = this.Sprint.IsSet ? this.Sprint.Value : current.Sprint,
            HumanGate = this.HumanGate.IsSet ? this.HumanGate.Value : current.HumanGate,
            Action = this.Action.IsSet ? this.Action.Value : current.Action,
            Description = this.Description ?? current.Description,
            Location = current.Location with { Team = team, Project = project },
            UnknownFields = unknownFields,
        };
    }

    /// <summary>Replaces the entry of each key in <paramref name="extras"/> in <paramref name="fields"/>, keeping field order and appending new keys.</summary>
    /// <param name="fields">The Task's unknown frontmatter fields.</param>
    /// <param name="extras">The keys and values to set.</param>
    private static List<KeyValuePair<string, string>> WithExtras(IReadOnlyList<KeyValuePair<string, string>> fields, IReadOnlyList<KeyValuePair<string, string>> extras)
    {
        List<KeyValuePair<string, string>> result = [.. fields];
        foreach (KeyValuePair<string, string> extra in extras.Where(static extra => !string.Equals(extra.Key, ApprovedByKey, StringComparison.Ordinal)))
        {
            int index = result.FindIndex(field => string.Equals(field.Key, extra.Key, StringComparison.Ordinal));
            if (index >= 0)
            {
                result[index] = extra;
            }
            else
            {
                result.Add(extra);
            }
        }

        return result;
    }

    /// <summary>Replaces the <see cref="ApprovedByKey"/> entry of <paramref name="fields"/>, or removes it when <paramref name="approver"/> is blank.</summary>
    /// <param name="fields">The Task's unknown frontmatter fields.</param>
    /// <param name="approver">The approver's Name, or <see langword="null"/> to clear the approval.</param>
    private static List<KeyValuePair<string, string>> WithApprovedBy(IReadOnlyList<KeyValuePair<string, string>> fields, string? approver)
    {
        List<KeyValuePair<string, string>> result = [.. fields.Where(static field => !string.Equals(field.Key, ApprovedByKey, StringComparison.Ordinal))];
        if (!string.IsNullOrWhiteSpace(approver))
        {
            result.Add(new KeyValuePair<string, string>(ApprovedByKey, approver.Trim()));
        }

        return result;
    }
}

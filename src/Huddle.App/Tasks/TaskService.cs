using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Options;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The only way to change a Task (Spec §9): validates a <see cref="TaskDraft"/> or a
/// <see cref="TaskPatch"/> (Spec §9.2), builds the Change log entry, writes or moves the file
/// through <see cref="TaskStore"/>, and raises <see cref="TaskEvents.TaskChanged"/> after the write,
/// outside every lock. Only <see cref="Create"/> and <see cref="Update"/> are implemented so far
/// (Tasks 6.1-6.2); <c>Close</c>, <c>Reopen</c>, outside-edit logging and Teammate renaming follow
/// in later tasks of D6.
/// </summary>
internal sealed class TaskService
{
    /// <summary>The fixed Windows-illegal characters, enforced on every OS (Settled corrections-B2 D6 item 5) - not <see cref="Path.GetInvalidFileNameChars"/>, which on Linux is only NUL and '/'.</summary>
    private static readonly char[] IllegalFolderNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>The reserved Windows device names, checked with and without an extension, case-insensitively (Settled corrections-B2 D6 item 5).</summary>
    private static readonly HashSet<string> ReservedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Serialises every mutating method against every other one (Settled corrections-B2 D6 item 2).
    // Lock order: mutateGate -> TaskStore.writeGate, so this class never calls PersonaStore or
    // raises an event while a writeGate is held. Create and Update below hold this for their whole
    // "read current, validate, write" sequence and release it before TaskChanged is raised.
    private readonly Lock mutateGate = new();

    private readonly TaskStore store;
    private readonly TaskIdAllocator ids;
    private readonly TaskEvents events;
    private readonly PersonaStore personas;
    private readonly TeamOptions options;
    private readonly TimeProvider clock;
    private readonly ILogger<TaskService> logger;

    /// <summary>
    /// Wires this service to the Task index and to the Change log's clock. Deliberately does not
    /// take <see cref="Data.ITeamDirectory"/> (Settled corrections-B2 D6 item 9): it is unused,
    /// because the Human's Name comes from <see cref="TeamOptions.HumanName"/>, not from a Room
    /// membership lookup.
    /// </summary>
    /// <param name="store">Owns the Task files this service validates against and writes through.</param>
    /// <param name="ids">Allocates a new Task's id.</param>
    /// <param name="events">The hub this service raises <see cref="TaskEvents.TaskChanged"/> and re-raised reloads on.</param>
    /// <param name="personas">Supplies known Team labels and resolves an assignee's Alias to its Name.</param>
    /// <param name="options">Supplies <see cref="TeamOptions.HumanName"/>.</param>
    /// <param name="clock">Supplies the Change log entry's timestamp.</param>
    /// <param name="logger">Used for diagnostics; unused so far.</param>
    public TaskService(
        TaskStore store,
        TaskIdAllocator ids,
        TaskEvents events,
        PersonaStore personas,
        IOptions<TeamOptions> options,
        TimeProvider clock,
        ILogger<TaskService> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        this.store = store;
        this.ids = ids;
        this.events = events;
        this.personas = personas;
        this.options = options.Value;
        this.clock = clock;
        this.logger = logger;

        // Settled corrections-B2 D6 item 13: TaskEvents is a plain hub; this is the one place that
        // turns TaskStore's own index-rebuild notification into the same event a Task mutation
        // raises, so a UI list needs only one subscription regardless of which one fired.
        this.store.IndexChanged += this.events.RaiseTasksReloaded;
    }

    /// <summary>
    /// Validates <paramref name="draft"/> (Spec §9.2), allocates its id, and writes it as a brand
    /// new file that never overwrites an existing one (Settled corrections-B2 D6 item 6).
    /// </summary>
    /// <param name="draft">The new Task.</param>
    /// <param name="actor">Who is creating it; becomes the Task's <see cref="TaskItem.Creator"/> and the Change log entry's actor.</param>
    public TaskResult Create(TaskDraft draft, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.CreateCore(draft, actor);
        }

        if (change is not null)
        {
            this.events.RaiseTaskChanged(change);
        }

        return result;
    }

    /// <summary>
    /// Applies <paramref name="patch"/> to the current Task and writes the result (Spec §9.4),
    /// merging or conflicting against a stale <paramref name="baseVersion"/> first (Spec §9.3).
    /// </summary>
    /// <param name="id">The Task to update.</param>
    /// <param name="patch">Only the fields being changed.</param>
    /// <param name="baseVersion">
    /// The <see cref="TaskItem.Version"/> the caller last saw, or <see langword="null"/> when the
    /// caller didn't track one (tools, drags - Spec §9.3). <see langword="null"/>, or a value equal
    /// to the Task's current version, applies the patch outright; a stale value is merged against
    /// what changed since, or reported as a <see cref="TaskResult.Conflict"/>.
    /// </param>
    /// <param name="actor">Who is making the change.</param>
    public TaskResult Update(TaskId id, TaskPatch patch, string? baseVersion, TaskActor actor)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(actor);

        TaskChange? change;
        TaskResult result;
        lock (this.mutateGate)
        {
            (result, change) = this.UpdateCore(id, patch, baseVersion, actor);
        }

        if (change is not null)
        {
            this.events.RaiseTaskChanged(change);
        }

        return result;
    }

    /// <summary>The body of <see cref="Create"/>, run under <see cref="mutateGate"/>. Returns the change to raise, or <see langword="null"/> when nothing was written.</summary>
    private (TaskResult Result, TaskChange? Change) CreateCore(TaskDraft draft, TaskActor actor)
    {
        string team = this.CanonicalizeTeam(draft.Team);
        string? project = this.CanonicalizeProject(team, draft.Project);
        string? assignee = this.ResolveAssignee(draft.Assignee);

        TaskItem candidate = new()
        {
            Id = default,
            Title = draft.Title,
            Status = draft.Status,
            Priority = draft.Priority,
            Creator = actor.Name,
            Assignee = assignee,
            OriginRoomId = draft.OriginRoomId,
            Parent = draft.Parent,
            BlockedBy = draft.BlockedBy ?? [],
            DuplicateOf = null,
            Tags = draft.Tags ?? [],
            StartDate = draft.StartDate,
            DueDate = draft.DueDate,
            Description = draft.Description,
            Location = new TaskLocation(team, project, false),
            Path = "",
            Version = "",
        };

        List<string> problems = this.Validate(candidate, touchedFields: null, reason: null);
        if (problems.Count > 0)
        {
            return (new TaskResult.Refused(problems), null);
        }

        string prefix = this.ids.PrefixFor(team);
        int highestSeen = this.store.HighestNumber(prefix);
        TaskId id = this.ids.Next(team, highestSeen);

        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        ChangeLogEntry entry = new(at, actor.Name, "created");

        TaskItem toWrite = candidate with
        {
            Id = id,
            Path = TaskLayout.PathFor(this.store.RootDirectory, candidate.Location, id),
            ChangeLog = [entry],
        };

        string text = TaskFileFormat.Compose(toWrite);
        TaskItem? written = this.store.Create(toWrite, text);
        if (written is null)
        {
            // The id allocator guarantees a fresh number per Team, so this only fires when
            // something outside TaskService raced a file onto the exact path it just allocated -
            // an invariant break, not an expected failure, so it's logged and thrown rather than a
            // TaskResult (house principle: expected failures are TaskResults, invariant breaks are
            // exceptions).
            this.logger.LogError("Task '{TaskId}' could not be created: a file already exists at '{Path}'.", id, toWrite.Path);
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Task '{id}' already exists."));
        }

        TaskChange change = new(null, written, [], actor, entry);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>The body of <see cref="Update"/>, run under <see cref="mutateGate"/>. Returns the change to raise, or <see langword="null"/> when nothing was written.</summary>
    private (TaskResult Result, TaskChange? Change) UpdateCore(TaskId id, TaskPatch patch, string? baseVersion, TaskActor actor)
    {
        TaskItem? current = this.store.Get(id);
        if (current is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        TaskPatch resolvedPatch = this.ResolvePatch(patch, current.Location.Team);
        TaskItem candidate = resolvedPatch.ApplyTo(current);

        List<string> problems = this.Validate(candidate, resolvedPatch.Fields(), resolvedPatch.Reason);
        if (problems.Count > 0)
        {
            return (new TaskResult.Refused(problems), null);
        }

        IReadOnlyList<FieldChange> diff = TaskDiff.Compare(current, candidate);

        if (baseVersion is not null && !string.Equals(baseVersion, current.Version, StringComparison.Ordinal))
        {
            List<TaskField> conflictFields = this.FindConflictingFields(id, baseVersion, current, resolvedPatch, diff);
            if (conflictFields.Count > 0)
            {
                return (new TaskResult.Conflict(current, conflictFields), null);
            }
        }

        if (diff.Count == 0)
        {
            return (new TaskResult.Unchanged(current), null);
        }

        DateTimeOffset at = TruncateToSeconds(this.clock.GetUtcNow());
        string summary = TaskDiff.Summarise(diff);
        if (resolvedPatch.Reason is { Length: > 0 } reason)
        {
            summary = string.Create(CultureInfo.InvariantCulture, $"{summary} (reason: {reason})");
        }

        ChangeLogEntry entry = new(at, actor.Name, summary);

        string? currentText = this.store.ReadText(id);
        if (currentText is null)
        {
            return (new TaskResult.NotFound(id), null);
        }

        string headReplaced = TaskFileFormat.ReplaceHead(currentText, candidate);
        string finalText = TaskFileFormat.AppendEntry(headReplaced, entry);

        bool moved = !string.Equals(current.Location.Team, candidate.Location.Team, StringComparison.Ordinal) ||
            !string.Equals(current.Location.Project, candidate.Location.Project, StringComparison.Ordinal);

        TaskItem? written = moved
            ? this.store.Move(current, current.Version, candidate.Location, finalText)
            : this.store.Write(current, current.Version, finalText);

        if (written is null)
        {
            return (new TaskResult.Conflict(this.store.Get(id) ?? current, []), null);
        }

        TaskChange change = new(current, written, diff, actor, entry);
        return (new TaskResult.Saved(written, change), change);
    }

    /// <summary>
    /// Spec §9.3's merge/conflict check for a stale <paramref name="baseVersion"/> (already known to
    /// differ from <paramref name="current"/>'s own version): finds what the base referred to via
    /// <see cref="TaskStore.GetVersion"/>, and returns every field the caller's patch and an
    /// intervening change both touch, restricted (corrections-B5 decision C) to the fields whose
    /// patched value actually differs from <paramref name="current"/>'s - a patch that happens to
    /// resubmit the value already on disk is never a conflict. When the base version has aged out of
    /// <see cref="TaskStore.GetVersion"/>'s history (step 5), every patched field that differs from
    /// <paramref name="current"/> conflicts, since there's no way to tell which of them an
    /// intervening change actually touched.
    /// </summary>
    /// <param name="id">The Task being updated.</param>
    /// <param name="baseVersion">The caller's stale base version.</param>
    /// <param name="current">The Task as it is now.</param>
    /// <param name="resolvedPatch">The patch, with its Assignee/Team/Project already resolved.</param>
    /// <param name="patchVsCurrent">What the resolved patch actually changes relative to <paramref name="current"/>.</param>
    private List<TaskField> FindConflictingFields(TaskId id, string baseVersion, TaskItem current, TaskPatch resolvedPatch, IReadOnlyList<FieldChange> patchVsCurrent)
    {
        TaskItem? baseTask = this.store.GetVersion(id, baseVersion);
        List<TaskField> patchedFieldsThatDiffer = [.. patchVsCurrent.Select(change => change.Field)];

        if (baseTask is null)
        {
            return patchedFieldsThatDiffer;
        }

        IReadOnlyList<FieldChange> othersChanges = TaskDiff.Compare(baseTask, current);
        HashSet<TaskField> touchedByBoth = new(resolvedPatch.Fields());
        touchedByBoth.IntersectWith(othersChanges.Select(change => change.Field));

        return [.. patchedFieldsThatDiffer.Where(touchedByBoth.Contains)];
    }

    /// <summary>Resolves a patch's Assignee alias to its Name and canonicalises its Team/Project casing (Settled corrections-B2 D6 item 4), leaving every other field untouched.</summary>
    private TaskPatch ResolvePatch(TaskPatch patch, string currentTeam)
    {
        string? resolvedTeam = patch.Team is not null ? this.CanonicalizeTeam(patch.Team) : null;
        string teamForProject = resolvedTeam ?? currentTeam;

        return patch with
        {
            Assignee = patch.Assignee.IsSet ? Optional<string?>.Set(this.ResolveAssignee(patch.Assignee.Value)) : patch.Assignee,
            Team = resolvedTeam ?? patch.Team,
            Project = patch.Project.IsSet ? Optional<string?>.Set(this.CanonicalizeProject(teamForProject, patch.Project.Value)) : patch.Project,
        };
    }

    /// <summary>Resolves an assignee's Alias to its Name, or the Human's own Name to its canonical casing. An unresolvable value passes through unchanged, so <see cref="Validate"/> can report it.</summary>
    private string? ResolveAssignee(string? assignee)
    {
        if (assignee is not { Length: > 0 })
        {
            return assignee;
        }

        if (string.Equals(assignee, this.options.HumanName, StringComparison.OrdinalIgnoreCase))
        {
            return this.options.HumanName;
        }

        return this.personas.ResolveByNameOrAlias(assignee)?.Name ?? assignee;
    }

    /// <summary>Returns an existing Team folder's casing for <paramref name="team"/> (Settled corrections-B2 D6 item 4), or <paramref name="team"/> unchanged when no folder exists yet.</summary>
    private string CanonicalizeTeam(string team)
    {
        foreach (TeamFolder folder in this.store.Teams)
        {
            if (string.Equals(folder.Name, team, StringComparison.OrdinalIgnoreCase))
            {
                return folder.Name;
            }
        }

        return team;
    }

    /// <summary>Returns an existing Project sub-folder's casing under <paramref name="canonicalTeam"/> for <paramref name="project"/>, or <paramref name="project"/> unchanged.</summary>
    private string? CanonicalizeProject(string canonicalTeam, string? project)
    {
        if (project is not { Length: > 0 })
        {
            return project;
        }

        foreach (TeamFolder folder in this.store.Teams)
        {
            if (!string.Equals(folder.Name, canonicalTeam, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string existingProject in folder.Projects)
            {
                if (string.Equals(existingProject, project, StringComparison.OrdinalIgnoreCase))
                {
                    return existingProject;
                }
            }
        }

        return project;
    }

    /// <summary>True when a Team label a Persona names, or an existing Task Team folder, matches <paramref name="team"/> case-insensitively.</summary>
    private bool IsKnownTeam(string team) =>
        this.personas.Teams.Any(known => string.Equals(known, team, StringComparison.OrdinalIgnoreCase)) ||
        this.store.Teams.Any(folder => string.Equals(folder.Name, team, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every known Team label, from Persona declarations and existing Task folders, deduplicated case-insensitively and sorted for a problem message.</summary>
    private List<string> KnownTeamNames()
    {
        List<string> names = [.. this.personas.Teams];
        foreach (TeamFolder folder in this.store.Teams)
        {
            if (!names.Contains(folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(folder.Name);
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>
    /// Spec §9.2's rules, scoped by <paramref name="touchedFields"/>: <see langword="null"/> checks
    /// every rule (Create), a list checks only the rules for those fields (Update - Settled
    /// corrections-B2 D6 item 3: missing references elsewhere in the Task are left alone). The
    /// reason/status coupling rule always runs, since <see cref="TaskPatch.Reason"/> isn't itself a
    /// <see cref="TaskField"/>.
    /// </summary>
    /// <param name="candidate">The Task as it would be after the change.</param>
    /// <param name="touchedFields">The fields being changed, or <see langword="null"/> for every field.</param>
    /// <param name="reason">The reason offered with the change, if any.</param>
    private List<string> Validate(TaskItem candidate, IReadOnlyCollection<TaskField>? touchedFields, string? reason)
    {
        List<string> problems = [];

        bool Touches(TaskField field) => touchedFields is null || touchedFields.Contains(field);

        if (Touches(TaskField.Title))
        {
            AddTitleProblems(candidate.Title, problems);
        }

        if (Touches(TaskField.Description))
        {
            AddDescriptionProblems(candidate.Description, problems);
        }

        if (Touches(TaskField.Location))
        {
            this.AddTeamAndProjectProblems(candidate.Location, problems);
        }

        if (Touches(TaskField.Assignee))
        {
            this.AddAssigneeProblems(candidate.Assignee, problems);
        }

        if (Touches(TaskField.Status) || Touches(TaskField.DuplicateOf))
        {
            AddDuplicateCouplingProblems(candidate.Status, candidate.DuplicateOf, problems);
        }

        if (Touches(TaskField.Parent))
        {
            this.AddParentProblems(candidate, problems);
        }

        if (Touches(TaskField.BlockedBy))
        {
            this.AddBlockedByProblems(candidate, problems);
        }

        if (Touches(TaskField.DuplicateOf))
        {
            this.AddDuplicateOfReferenceProblems(candidate, problems);
        }

        if (Touches(TaskField.Tags))
        {
            AddTagProblems(candidate.Tags, problems);
        }

        AddReasonProblems(reason, candidate.Status, problems);

        return problems;
    }

    /// <summary>Spec §9.2: the title is 1-200 characters and one line.</summary>
    private static void AddTitleProblems(string title, List<string> problems)
    {
        string trimmed = title.Trim();
        if (trimmed.Length == 0)
        {
            problems.Add("Title is empty.");
            return;
        }

        if (trimmed.Length > 200)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Title is {trimmed.Length} characters; the limit is 200."));
            return;
        }

        if (title.Contains('\n') || title.Contains('\r'))
        {
            problems.Add("Title must be one line.");
        }
    }

    /// <summary>Spec §9.2: the description doesn't contain a reserved Change log heading.</summary>
    private static void AddDescriptionProblems(string description, List<string> problems)
    {
        if (TaskFileFormat.ContainsChangeLogHeading(description))
        {
            problems.Add("The description must not contain a '## Change log' heading; that heading is reserved for the task's history.");
        }
    }

    /// <summary>Spec §9.2: the Team and Project are legal folder names, and the Team is known.</summary>
    private void AddTeamAndProjectProblems(TaskLocation location, List<string> problems)
    {
        if (TryGetIllegalFolderNameProblem("team", location.Team, out string? teamProblem))
        {
            problems.Add(teamProblem);
        }
        else if (!this.IsKnownTeam(location.Team))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Unknown team '{location.Team}'. Known teams: {string.Join(", ", this.KnownTeamNames())}."));
        }

        if (location.Project is not { Length: > 0 } project)
        {
            return;
        }

        if (project.StartsWith('_'))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"The Project name '{project}' cannot be a folder name on this computer (it starts with '_')."));
        }
        else if (TryGetIllegalFolderNameProblem("Project", project, out string? projectProblem))
        {
            problems.Add(projectProblem);
        }
    }

    /// <summary>Spec §9.2: the assignee is a known Persona Name or Alias, or the Human's Name.</summary>
    private void AddAssigneeProblems(string? assignee, List<string> problems)
    {
        if (assignee is not { Length: > 0 })
        {
            return;
        }

        if (string.Equals(assignee, this.options.HumanName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (this.personas.ResolveByNameOrAlias(assignee) is not null)
        {
            return;
        }

        problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown teammate '{assignee}'."));
    }

    /// <summary>Spec §9.2: DuplicateOf is set exactly when the status is Duplicate.</summary>
    private static void AddDuplicateCouplingProblems(TaskState status, TaskId? duplicateOf, List<string> problems)
    {
        if (status == TaskState.Duplicate && duplicateOf is null)
        {
            problems.Add("Status Duplicate needs duplicate_of.");
        }
        else if (status != TaskState.Duplicate && duplicateOf is not null)
        {
            problems.Add("duplicate_of is only allowed when status is Duplicate.");
        }
    }

    /// <summary>Spec §9.2: a parent isn't the Task itself, must exist, and can't create a cycle.</summary>
    private void AddParentProblems(TaskItem candidate, List<string> problems)
    {
        if (candidate.Parent is not { } parent)
        {
            return;
        }

        if (parent == candidate.Id)
        {
            problems.Add("A task cannot block itself.");
            return;
        }

        if (this.store.Get(parent) is null)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown task '{parent}'."));
            return;
        }

        if (this.CreatesCycle(candidate.Id, parent))
        {
            problems.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{candidate.Id} is already a descendant of {parent}, so it cannot be its parent."));
        }
    }

    /// <summary>True when walking up from <paramref name="parentCandidate"/> through recorded parents reaches <paramref name="candidateId"/>, bounded against a malformed cycle already on disk.</summary>
    private bool CreatesCycle(TaskId candidateId, TaskId parentCandidate)
    {
        TaskId? current = parentCandidate;
        int guard = 0;
        while (current is { } id && guard < 10_000)
        {
            if (id == candidateId)
            {
                return true;
            }

            current = this.store.Get(id)?.Parent;
            guard++;
        }

        return false;
    }

    /// <summary>Spec §9.2: every blocked-by id isn't the Task itself, and must exist.</summary>
    private void AddBlockedByProblems(TaskItem candidate, List<string> problems)
    {
        foreach (TaskId blocker in candidate.BlockedBy)
        {
            if (blocker == candidate.Id)
            {
                problems.Add("A task cannot block itself.");
                continue;
            }

            if (this.store.Get(blocker) is null)
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown task '{blocker}'."));
            }
        }
    }

    /// <summary>Spec §9.2: duplicate_of isn't the Task itself, and must exist.</summary>
    private void AddDuplicateOfReferenceProblems(TaskItem candidate, List<string> problems)
    {
        if (candidate.DuplicateOf is not { } duplicateOf)
        {
            return;
        }

        if (duplicateOf == candidate.Id)
        {
            problems.Add("A task cannot block itself.");
            return;
        }

        if (this.store.Get(duplicateOf) is null)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Unknown task '{duplicateOf}'."));
        }
    }

    /// <summary>Spec §9.2/§7.2: a tag has no ',' or ';'.</summary>
    private static void AddTagProblems(IReadOnlyList<string> tags, List<string> problems)
    {
        foreach (string tag in tags)
        {
            if (tag.Contains(',', StringComparison.Ordinal) || tag.Contains(';', StringComparison.Ordinal))
            {
                problems.Add(string.Create(CultureInfo.InvariantCulture, $"Tag '{tag}' must not contain ',' or ';'."));
            }
        }
    }

    /// <summary>Spec §9.2: a reason is at most 200 characters, and only given with Cancelled or Rejected.</summary>
    private static void AddReasonProblems(string? reason, TaskState status, List<string> problems)
    {
        if (reason is not { Length: > 0 })
        {
            return;
        }

        if (reason.Length > 200)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"Reason is {reason.Length} characters; the limit is 200."));
        }

        if (status is not (TaskState.Cancelled or TaskState.Rejected))
        {
            problems.Add("A reason is only recorded when the status becomes Cancelled or Rejected.");
        }
    }

    /// <summary>Settled corrections-B2 D6 item 5: the fixed Windows-illegal-name set, checked on every OS.</summary>
    private static bool TryGetIllegalFolderNameProblem(string label, string name, [NotNullWhen(true)] out string? problem)
    {
        foreach (char c in name)
        {
            if (c < ' ' || Array.IndexOf(IllegalFolderNameCharacters, c) >= 0)
            {
                problem = string.Create(
                    CultureInfo.InvariantCulture,
                    $"The {label} name '{name}' cannot be a folder name on this computer (it contains '{c}').");
                return true;
            }
        }

        if (name.Length > 0 && name[^1] is '.' or ' ')
        {
            problem = string.Create(
                CultureInfo.InvariantCulture,
                $"The {label} name '{name}' cannot be a folder name on this computer (it ends with '{name[^1]}').");
            return true;
        }

        int dot = name.IndexOf('.', StringComparison.Ordinal);
        string stem = dot < 0 ? name : name[..dot];
        if (ReservedFolderNames.Contains(name) || ReservedFolderNames.Contains(stem))
        {
            problem = string.Create(
                CultureInfo.InvariantCulture,
                $"The {label} name '{name}' cannot be a folder name on this computer (it is reserved).");
            return true;
        }

        problem = null;
        return false;
    }

    /// <summary>Truncates to whole seconds, per Spec §9.4's Change log entry timestamp.</summary>
    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
}

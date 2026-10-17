namespace Agency.Huddle.Seeder.Model;

/// <summary>A Teammate definition file: <c>Teammates/&lt;Name&gt;/&lt;Name&gt;.md</c>.</summary>
/// <param name="Name">The Teammate's Name; also its folder and file name.</param>
/// <param name="Title">The Teammate's job title.</param>
/// <param name="Specialty">One line shown to other Teammates as part of the job description.</param>
/// <param name="Teams">The Teams the Teammate belongs to.</param>
/// <param name="Skills">The Skills the Teammate holds.</param>
/// <param name="Personality">The Markdown body: the Teammate's private system prompt.</param>
internal sealed record SeedTeammate(
    string Name,
    string Title,
    string Specialty,
    IReadOnlyList<string> Teams,
    IReadOnlyList<string> Skills,
    string Personality)
{
    /// <summary>The lower-case alias other Teammates use to mention this one.</summary>
    internal string Alias => this.Name.ToLowerInvariant();
}

/// <summary>A Skill: <c>Skills/&lt;Name&gt;/SKILL.md</c>.</summary>
/// <param name="Name">Lower-case words joined by <c>-</c>; also the folder name.</param>
/// <param name="Description">One sentence, at most 300 characters.</param>
/// <param name="Body">The Markdown steps.</param>
internal sealed record SeedSkill(string Name, string Description, string Body);

/// <summary>A Team folder and the Project folders directly inside it.</summary>
/// <param name="Name">The Team name.</param>
/// <param name="Projects">The Project names; empty for a Team with no Projects.</param>
internal sealed record SeedTeam(string Name, IReadOnlyList<string> Projects);

/// <summary>A plain file under the data folder: a Library note or a memory fact.</summary>
/// <param name="RelativePath">The path below the data folder, using <c>/</c>.</param>
/// <param name="Content">The Markdown text.</param>
internal sealed record SeedFile(string RelativePath, string Content);

/// <summary>A binary file under the data folder, such as a Library image or an avatar picture.</summary>
/// <param name="RelativePath">The path below the data folder, using <c>/</c>.</param>
/// <param name="Content">The bytes.</param>
internal sealed record SeedBinaryFile(string RelativePath, byte[] Content);

/// <summary>A file the Human attached in a chat, saved where the app's upload flow would save it.</summary>
/// <param name="RoomKey">The key of the Room the file was attached in.</param>
/// <param name="FileName">The file name; a Message cites it with the <c>{{upload:FileName}}</c> token.</param>
/// <param name="Content">The bytes.</param>
internal sealed record SeedUpload(string RoomKey, string FileName, byte[] Content);

/// <summary>A Sprint: one line of <c>sprints.md</c>.</summary>
/// <param name="Number">The number; the id is <c>S{Number}</c>.</param>
/// <param name="Name">The display name.</param>
internal sealed record SeedSprint(int Number, string Name)
{
    /// <summary>The Sprint id as a Task holds it, such as <c>S2</c>.</summary>
    internal string Id => $"S{this.Number}";
}

/// <summary>The Request fields of a Task that another Team filed (frontmatter <c>request</c>, <c>from_team</c>, <c>requester</c>, <c>triage</c>, <c>decline_reason</c>, <c>confirmed_by</c>).</summary>
/// <param name="Kind">The wire kind: <c>bug</c>, <c>feature</c>, <c>feedback</c> or <c>question</c>.</param>
/// <param name="FromTeam">The Team that filed it.</param>
/// <param name="Requester">The Teammate who filed it.</param>
/// <param name="Triage">The wire triage state: empty, <c>needs-info</c>, <c>accepted</c> or <c>declined</c>.</param>
/// <param name="DeclineReason">Why it was declined; empty otherwise.</param>
/// <param name="ConfirmedBy">The requesting Team's QA who confirmed it; empty until then.</param>
internal sealed record SeedTaskRequest(string Kind, string FromTeam, string Requester, string Triage, string DeclineReason = "", string ConfirmedBy = "");

/// <summary>Where a Task lives: a Team, and optionally one of its Projects.</summary>
/// <param name="Team">The Team name.</param>
/// <param name="Project">The Project name, or <see langword="null"/> for the Team root.</param>
internal sealed record SeedLocation(string Team, string? Project)
{
    /// <summary>The display path, <c>Team</c> or <c>Team/Project</c>.</summary>
    internal string Path => this.Project is null ? this.Team : $"{this.Team}/{this.Project}";
}

/// <summary>One extra Change log line for a Task, beyond the ones derived from its status.</summary>
/// <param name="Actor">Who did it.</param>
/// <param name="Summary">The app-style summary, such as <c>priority: Medium → High</c>.</param>
internal sealed record SeedLogNote(string Actor, string Summary);

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

    /// <summary>The Human's name or a Teammate's name.</summary>
    public required string Creator { get; init; }

    /// <summary>The Teammate or Human it is assigned to, or <see langword="null"/> when unassigned.</summary>
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

    /// <summary>How many days before the run date the Task was created.</summary>
    public int CreatedDaysAgo { get; init; } = 14;

    /// <summary>How many days before the run date the last Change log entry was written; at least 1.</summary>
    public int LastTouchedDaysAgo { get; init; } = 1;

    /// <summary>Extra Change log lines, written between the status changes and the closing entry.</summary>
    public IReadOnlyList<SeedLogNote> ExtraLog { get; init; } = [];

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

/// <summary>A chat message in a Room transcript.</summary>
/// <param name="Sender">The Human's name or a Teammate's Name.</param>
/// <param name="DaysAgo">Whole days before the run date.</param>
/// <param name="Time">The time of day in UTC, <c>HH:mm</c>.</param>
/// <param name="Text">The Markdown text.</param>
internal sealed record SeedMessage(string Sender, int DaysAgo, string Time, string Text);

/// <summary>A chat Room and its history.</summary>
/// <param name="Key">A stable key Tasks use to refer to the Room.</param>
/// <param name="Name">The Room name; when it equals the auto-derived name it is left as it is.</param>
/// <param name="Members">The Teammates in the Room; the Human is always added.</param>
/// <param name="Archived">Whether the Room is archived.</param>
/// <param name="Messages">The transcript, oldest first.</param>
/// <param name="Team">The Team the Room is scoped to, or <see langword="null"/> for a root Room.</param>
/// <param name="IsTeamRoom">Whether the Room is its Team's Team Room (at most one per Team).</param>
/// <param name="WithReferences">Whether the Messages carry <c>References</c>; <see langword="false"/> leaves them null so the reader's text scan is exercised.</param>
internal sealed record SeedRoom(
    string Key,
    string Name,
    IReadOnlyList<string> Members,
    bool Archived,
    IReadOnlyList<SeedMessage> Messages,
    string? Team = null,
    bool IsTeamRoom = false,
    bool WithReferences = true);

/// <summary>Everything a scenario asks the seeder to create.</summary>
internal sealed record SeedPlan
{
    /// <summary>The scenario name, such as <c>software-co</c>.</summary>
    public required string Scenario { get; init; }

    /// <summary>The Human's name.</summary>
    public required string HumanName { get; init; }

    /// <summary>The Teammates.</summary>
    public required IReadOnlyList<SeedTeammate> Teammates { get; init; }

    /// <summary>The Skills.</summary>
    public required IReadOnlyList<SeedSkill> Skills { get; init; }

    /// <summary>The Teams and their Projects.</summary>
    public required IReadOnlyList<SeedTeam> Teams { get; init; }

    /// <summary>The Tasks.</summary>
    public required IReadOnlyList<SeedTask> Tasks { get; init; }

    /// <summary>Library notes and memory facts.</summary>
    public required IReadOnlyList<SeedFile> Files { get; init; }

    /// <summary>The Rooms.</summary>
    public required IReadOnlyList<SeedRoom> Rooms { get; init; }

    /// <summary>The text of <c>views.json</c>.</summary>
    public required string ViewsJson { get; init; }

    /// <summary>The text of <c>avatars.json</c>.</summary>
    public required string AvatarsJson { get; init; }

    /// <summary>Binary files under the data folder (Library images, avatar pictures).</summary>
    public IReadOnlyList<SeedBinaryFile> BinaryFiles { get; init; } = [];

    /// <summary>Files attached in chats.</summary>
    public IReadOnlyList<SeedUpload> Uploads { get; init; } = [];

    /// <summary>The Sprints written to <c>sprints.md</c>; empty writes no file.</summary>
    public IReadOnlyList<SeedSprint> Sprints { get; init; } = [];

    /// <summary>The run date every offset is measured from.</summary>
    public required DateOnly Today { get; init; }
}

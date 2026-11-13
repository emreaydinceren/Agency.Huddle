using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Seeder.Model;

/// <summary>Everything a scenario asks the seeder to create. Seeder spec §5 (The plan a scenario returns).</summary>
internal sealed record SeedPlan
{
    /// <summary>The scenario name, such as <c>software-co</c>.</summary>
    public required string Scenario { get; init; }

    /// <summary>The Human's name.</summary>
    public required string HumanName { get; init; }

    /// <summary>The Agents.</summary>
    public required IReadOnlyList<SeedAgent> Agents { get; init; }

    /// <summary>The Skills.</summary>
    public required IReadOnlyList<SeedSkill> Skills { get; init; }

    /// <summary>The Teams and their Projects.</summary>
    public required IReadOnlyList<SeedTeam> Teams { get; init; }

    /// <summary>Teams already archived, under <c>Archive/</c>; never in <see cref="Teams"/>, which lists the live ones.</summary>
    public IReadOnlyList<SeedArchivedTeam> ArchivedTeams { get; init; } = [];

    /// <summary>The Tasks.</summary>
    public required IReadOnlyList<SeedTask> Tasks { get; init; }

    /// <summary>Library notes and memory facts.</summary>
    public required IReadOnlyList<SeedFile> Files { get; init; }

    /// <summary>The Rooms.</summary>
    public required IReadOnlyList<SeedRoom> Rooms { get; init; }

    /// <summary>The text of <c>views.json</c>.</summary>
    public required string ViewsJson { get; init; }

    /// <summary>The Human's avatar; the Agents' come from <see cref="SeedAgent.AvatarFile"/>.</summary>
    public Avatar HumanAvatar { get; init; } = new(Label: "ME", Image: null, Background: "#d46002");

    /// <summary>Binary files under the data folder (Library images, avatar pictures).</summary>
    public IReadOnlyList<SeedBinaryFile> BinaryFiles { get; init; } = [];

    /// <summary>Files attached in chats.</summary>
    public IReadOnlyList<SeedUpload> Uploads { get; init; } = [];

    /// <summary>Files uploaded to Tasks.</summary>
    public IReadOnlyList<SeedTaskUpload> TaskUploads { get; init; } = [];

    /// <summary>History entries of Library Markdown documents, written to <c>doc-history/</c>.</summary>
    public IReadOnlyList<SeedDocEdit> DocHistory { get; init; } = [];

    /// <summary>The Sprints written to <c>sprints.md</c>; empty writes no file.</summary>
    public IReadOnlyList<SeedSprint> Sprints { get; init; } = [];

    /// <summary>The Feed items; empty stores none.</summary>
    public IReadOnlyList<SeedFeedItem> Feed { get; init; } = [];

    /// <summary>The Jobs, written to their owners' <c>schedule.md</c>; empty writes none.</summary>
    public IReadOnlyList<SeedJob> Jobs { get; init; } = [];

    /// <summary><c>schedule.md</c> sections Huddle cannot read, written after the owner's Jobs.</summary>
    public IReadOnlyList<SeedJobProblem> JobProblems { get; init; } = [];

    /// <summary>The run date every offset is measured from.</summary>
    public required DateOnly Today { get; init; }
}

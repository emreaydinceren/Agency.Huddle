namespace Agency.Huddle.Seeder.Model;

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

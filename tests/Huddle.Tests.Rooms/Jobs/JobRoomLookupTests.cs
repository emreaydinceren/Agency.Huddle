using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Tests.Factories;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins that a Job Room is kept apart from an Agent's one-to-one, and that its link goes with the Room.</summary>
public sealed class JobRoomLookupTests : IDisposable
{
    private readonly TempDataDir dir = new();

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>A Job Room holding just the Human and one Agent is never returned as that Agent's one-to-one.</summary>
    [Fact]
    public async Task FindRoomWithExactMembers_SkipsAJobRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SqliteTeamDirectory directory = await this.dir.CreateDirectoryAsync(ct);
        User nova = await directory.UpsertAgentUserAsync("Nova", null, ct) ?? throw new InvalidOperationException("Nova was not created.");
        Room jobRoom = await directory.CreateRoomAsync("⏰ Digest", [KnownIds.Human, nova.Id], ct);
        new JobRoomStore(this.dir.Options()).Set("job-1", jobRoom.Id);

        Assert.Null(await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, ct));
        Assert.Null(await directory.FindRoomWithExactMemberSetAsync([KnownIds.Human, nova.Id], ct));

        Room oneToOne = await directory.CreateRoomAsync("Nova", [KnownIds.Human, nova.Id], ct);
        Assert.Equal(oneToOne.Id, (await directory.FindRoomWithExactMembersAsync(KnownIds.Human, nova.Id, ct))?.Id);
    }

    /// <summary>Deleting a Job Room deletes its link too, so the foreign key does not refuse the delete and the Job gets a new Room.</summary>
    [Fact]
    public async Task DeleteRoom_RemovesTheJobLink()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SqliteTeamDirectory directory = await this.dir.CreateDirectoryAsync(ct);
        Room jobRoom = await directory.CreateRoomAsync("⏰ Digest", [KnownIds.Human], ct);
        JobRoomStore links = new(this.dir.Options());
        links.Set("job-1", jobRoom.Id);

        await directory.DeleteRoomAsync(jobRoom.Id, ct);

        Assert.Null(links.RoomOf("job-1"));
    }

    /// <summary>Setting a Job's Room replaces its earlier one.</summary>
    [Fact]
    public async Task Set_ReplacesTheEarlierRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SqliteTeamDirectory directory = await this.dir.CreateDirectoryAsync(ct);
        Room first = await directory.CreateRoomAsync("first", [KnownIds.Human], ct);
        Room second = await directory.CreateRoomAsync("second", [KnownIds.Human], ct);
        JobRoomStore links = new(this.dir.Options());

        links.Set("job-1", first.Id);
        links.Set("job-1", second.Id);

        Assert.Equal(second.Id, links.RoomOf("job-1"));
    }

    /// <summary>The state store round-trips every field and forgets a removed Job.</summary>
    [Fact]
    public void JobStateStore_RoundTripsAndForgets()
    {
        JobStateStore states = new(this.dir.Options());
        JobState state = new("job-1", "daily at 09:00|UTC", DateTimeOffset.Parse("2026-11-03T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture), null, JobOutcome.BudgetSpent);

        states.Set(state);
        Assert.Equal(state, states.Get("job-1"));

        states.Remove("job-1");
        Assert.Null(states.Get("job-1"));
    }

    /// <summary>An outcome is stored by name, never by number, so reordering the enum cannot rewrite history.</summary>
    [Fact]
    public void JobStateStore_StoresTheOutcomeByName()
    {
        new JobStateStore(this.dir.Options()).Set(new JobState("job-1", "k", null, null, JobOutcome.BudgetSpent));

        using Microsoft.Data.Sqlite.SqliteConnection connection = new($"Data Source={Path.Combine(this.dir.Path, "team.db")}");
        connection.Open();
        using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT last_outcome FROM job_state WHERE job_id = 'job-1';";

        Assert.Equal("budget-spent", command.ExecuteScalar());
    }
}

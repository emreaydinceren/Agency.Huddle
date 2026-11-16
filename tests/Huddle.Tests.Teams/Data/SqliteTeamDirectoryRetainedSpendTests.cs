using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// What a deleted Room spent inside a Project stays in the Project's total through <see cref="SqliteTeamDirectory.DeleteRoomAsync"/>,
/// and follows a Project or Team rename (<c>project_spend_retained</c>).
/// </summary>
public sealed class SqliteTeamDirectoryRetainedSpendTests : IAsyncLifetime
{
    private readonly TempDataDir dir = new();
    private readonly SqliteTeamDirectory directory;
    private readonly RoomSpendStore spend;

    /// <summary>Builds the directory and the spend store over the same <c>team.db</c>.</summary>
    public SqliteTeamDirectoryRetainedSpendTests()
    {
        this.directory = new SqliteTeamDirectory(this.dir.Options());
        this.spend = new RoomSpendStore(this.dir.Options());
    }

    /// <summary>Creates the directory's tables, the spend ones included.</summary>
    public async ValueTask InitializeAsync() => await this.directory.InitializeAsync("You", TestContext.Current.CancellationToken);

    /// <summary>Deletes the temp data folder.</summary>
    public ValueTask DisposeAsync()
    {
        this.dir.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Deleting a Room of a Project keeps what it spent in the Project's total, and drops the Room's own rows.</summary>
    [Fact]
    public async Task DeleteRoom_ProjectRoom_KeepsTheProjectTotal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room gone = await this.directory.CreateRoomAsync("Gone", [KnownIds.Human], "Dev", false, "Site", ct);
        Room kept = await this.directory.CreateRoomAsync("Kept", [KnownIds.Human], "Dev", false, "Site", ct);
        this.spend.Add(gone.Id, "USD", 1.5m);
        this.spend.Add(gone.Id, "EUR", 4m);
        this.spend.Add(kept.Id, "USD", 2m);

        await this.directory.DeleteRoomAsync(gone.Id, ct);

        Assert.Equal([new StoredSpend("EUR", 4m), new StoredSpend("USD", 3.5m)], this.spend.ForProject("Dev", "Site"));
        Assert.Empty(this.spend.ForRoom(gone.Id));
    }

    /// <summary>A second Room deleted after the first adds into the same retained row.</summary>
    [Fact]
    public async Task DeleteRoom_TwoRoomsOfTheProject_RetainsTheirSum()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room first = await this.directory.CreateRoomAsync("First", [KnownIds.Human], "Dev", false, "Site", ct);
        Room second = await this.directory.CreateRoomAsync("Second", [KnownIds.Human], "Dev", false, "Site", ct);
        this.spend.Add(first.Id, "USD", 1m);
        this.spend.Add(second.Id, "USD", 2m);

        await this.directory.DeleteRoomAsync(first.Id, ct);
        await this.directory.DeleteRoomAsync(second.Id, ct);

        Assert.Equal([new StoredSpend("USD", 3m)], this.spend.ForProject("Dev", "Site"));
    }

    /// <summary>Deleting a Room that has no Project changes no Project's total, and its spend is gone.</summary>
    [Fact]
    public async Task DeleteRoom_NoProject_ChangesNoProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room loose = await this.directory.CreateRoomAsync("Loose", [KnownIds.Human], "Dev", false, null, ct);
        Room site = await this.directory.CreateRoomAsync("Site", [KnownIds.Human], "Dev", false, "Site", ct);
        this.spend.Add(loose.Id, "USD", 10m);
        this.spend.Add(site.Id, "USD", 2m);

        await this.directory.DeleteRoomAsync(loose.Id, ct);

        Assert.Equal([new StoredSpend("USD", 2m)], this.spend.ForProject("Dev", "Site"));
        Assert.Empty(this.spend.ForRoom(loose.Id));
    }

    /// <summary>A Project rename after a deletion keeps the retained amount under the new name, added to what its live Rooms spent.</summary>
    [Fact]
    public async Task RenameProjectScope_AfterDelete_MovesTheRetainedSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room gone = await this.directory.CreateRoomAsync("Gone", [KnownIds.Human], "Dev", false, "Site", ct);
        Room kept = await this.directory.CreateRoomAsync("Kept", [KnownIds.Human], "Dev", false, "Site", ct);
        this.spend.Add(gone.Id, "USD", 1m);
        this.spend.Add(kept.Id, "USD", 2m);
        await this.directory.DeleteRoomAsync(gone.Id, ct);

        _ = await this.directory.RenameProjectScopeAsync("Dev", "Site", "Web", ct);

        Assert.Equal([new StoredSpend("USD", 3m)], this.spend.ForProject("Dev", "Web"));
        Assert.Empty(this.spend.ForProject("Dev", "Site"));
    }

    /// <summary>A Project rename keeps retained spend even when no live Room is left in the Project.</summary>
    [Fact]
    public async Task RenameProjectScope_OnlyRetainedSpend_MovesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room gone = await this.directory.CreateRoomAsync("Gone", [KnownIds.Human], "Dev", false, "Site", ct);
        this.spend.Add(gone.Id, "USD", 1m);
        await this.directory.DeleteRoomAsync(gone.Id, ct);

        _ = await this.directory.RenameProjectScopeAsync("Dev", "Site", "Web", ct);

        Assert.Equal([new StoredSpend("USD", 1m)], this.spend.ForProject("Dev", "Web"));
        Assert.Empty(this.spend.ForProject("Dev", "Site"));
    }

    /// <summary>A Team rename after a deletion keeps the retained amount under the new Team name.</summary>
    [Fact]
    public async Task RenameTeamScope_AfterDelete_MovesTheRetainedSpend()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Room gone = await this.directory.CreateRoomAsync("Gone", [KnownIds.Human], "Dev", false, "Site", ct);
        this.spend.Add(gone.Id, "USD", 1m);
        await this.directory.DeleteRoomAsync(gone.Id, ct);

        _ = await this.directory.RenameTeamScopeAsync("Dev", "Core", ct);

        Assert.Equal([new StoredSpend("USD", 1m)], this.spend.ForProject("Core", "Site"));
        Assert.Empty(this.spend.ForProject("Dev", "Site"));
    }
}

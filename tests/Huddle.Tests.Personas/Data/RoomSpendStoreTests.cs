using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>Exercises <see cref="RoomSpendStore"/> against a real SQLite file under <see cref="TempDataDir"/>, with the Room-to-Project rows the join reads.</summary>
public sealed class RoomSpendStoreTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly RoomSpendStore store;

    /// <summary>Opens the store, which creates <c>room_spend</c> and the two tables the Project join reads.</summary>
    public RoomSpendStoreTests() => this.store = new RoomSpendStore(this.dir.Options());

    /// <summary>Deletes the temp data folder.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>Adds into one Room sum per currency, as exact decimals, ordered by currency code.</summary>
    [Fact]
    public void Add_ThenForRoom_SumsPerCurrencyOrderedByCode()
    {
        this.store.Add("r1", "USD", 0.1m);
        this.store.Add("r1", "USD", 0.2m);
        this.store.Add("r1", "EUR", 1m);
        this.store.Add("r2", "USD", 50m);

        Assert.Equal([new StoredSpend("EUR", 1m), new StoredSpend("USD", 0.3m)], this.store.ForRoom("r1"));
    }

    /// <summary>A zero or negative amount writes nothing, so a bad report cannot take spend away.</summary>
    [Fact]
    public void Add_ZeroOrNegative_IsIgnored()
    {
        this.store.Add("r1", "USD", 2m);

        this.store.Add("r1", "USD", 0m);
        this.store.Add("r1", "USD", -1m);

        Assert.Equal([new StoredSpend("USD", 2m)], this.store.ForRoom("r1"));
    }

    /// <summary>A database with no Rooms and no spend answers empty, not an error.</summary>
    [Fact]
    public void ForProject_FreshDatabase_IsEmpty()
    {
        Assert.Empty(this.store.ForProject("Dev", "Site"));
        Assert.Empty(this.store.ForRoom("r1"));
    }

    /// <summary>A Project's spend is only its own Rooms': another Project, the same Project name in another Team, a Room with no Project and a Room with no row at all are left out.</summary>
    [Fact]
    public void ForProject_SumsOnlyTheRoomsOfThatProjectInThatTeam()
    {
        RoomScopeSeed.Place(this.dir, "site-1", "Dev", "Site");
        RoomScopeSeed.Place(this.dir, "site-2", "dev", "SITE");
        RoomScopeSeed.Place(this.dir, "other", "Dev", "Other");
        RoomScopeSeed.Place(this.dir, "ops-site", "Ops", "Site");
        RoomScopeSeed.Place(this.dir, "loose", "Dev", null);
        this.store.Add("site-1", "USD", 1m);
        this.store.Add("site-2", "USD", 2m);
        this.store.Add("site-2", "EUR", 4m);
        this.store.Add("other", "USD", 10m);
        this.store.Add("ops-site", "USD", 100m);
        this.store.Add("loose", "USD", 1000m);
        this.store.Add("unplaced", "USD", 10000m);

        Assert.Equal([new StoredSpend("EUR", 4m), new StoredSpend("USD", 3m)], this.store.ForProject("Dev", "Site"));
    }

    /// <summary>A Room moved into a Project after it spent brings that spend with it, because the Project is joined when read.</summary>
    [Fact]
    public void ForProject_RoomMovedIntoProjectLater_CountsItsEarlierSpend()
    {
        RoomScopeSeed.Place(this.dir, "r1", "Dev", null);
        this.store.Add("r1", "USD", 3m);
        Assert.Empty(this.store.ForProject("Dev", "Site"));

        RoomScopeSeed.Place(this.dir, "r1", "Dev", "Site");

        Assert.Equal([new StoredSpend("USD", 3m)], this.store.ForProject("Dev", "Site"));
    }
}

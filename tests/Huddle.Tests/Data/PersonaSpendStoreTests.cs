using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>Exercises <see cref="PersonaSpendStore"/> against a real SQLite file under <see cref="TempDataDir"/>.</summary>
public sealed class PersonaSpendStoreTests
{
    /// <summary>Two adds for the same Persona and currency accumulate into one total.</summary>
    [Fact]
    public void Add_TwiceSameCurrency_Accumulates()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        store.Add("Maya", null, "USD", 1.5m);
        store.Add("Maya", null, "USD", 2.25m);

        StoredSpend spend = Assert.Single(store.ForPersona("Maya"));
        Assert.Equal("USD", spend.Currency);
        Assert.Equal(3.75m, spend.Amount);
    }

    /// <summary>Spend on different Tasks is split by ForTask but summed by ForPersona.</summary>
    [Fact]
    public void Add_DifferentTasks_SplitsPerTaskAndSumsPerPersona()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        store.Add("Maya", "T-1", "USD", 1m);
        store.Add("Maya", "T-2", "USD", 2m);
        store.Add("Maya", null, "USD", 4m);

        Assert.Equal(1m, Assert.Single(store.ForTask("T-1")).Amount);
        Assert.Equal(2m, Assert.Single(store.ForTask("T-2")).Amount);
        Assert.Equal(7m, Assert.Single(store.ForPersona("Maya")).Amount);
    }

    /// <summary>Two currencies for one Persona are never mixed.</summary>
    [Fact]
    public void Add_TwoCurrencies_StaySeparate()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        store.Add("Maya", null, "USD", 1m);
        store.Add("Maya", null, "EUR", 2m);

        IReadOnlyList<StoredSpend> spend = store.ForPersona("Maya");
        Assert.Equal(2, spend.Count);
        Assert.Equal(1m, Assert.Single(spend, s => s.Currency == "USD").Amount);
        Assert.Equal(2m, Assert.Single(spend, s => s.Currency == "EUR").Amount);
    }

    /// <summary>Zero and negative amounts are ignored.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Add_NonPositiveAmount_IsIgnored(int amount)
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        store.Add("Maya", null, "USD", amount);

        Assert.Empty(store.ForPersona("Maya"));
    }

    /// <summary>Rename moves every row to the new name, case-insensitively, and leaves nothing under the old name.</summary>
    [Fact]
    public void Rename_MovesAllRowsToNewName()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());
        store.Add("Maya", "T-1", "USD", 1m);
        store.Add("Maya", null, "USD", 2m);

        store.Rename("maya", "Mia");

        Assert.Empty(store.ForPersona("Maya"));
        Assert.Equal(3m, Assert.Single(store.ForPersona("Mia")).Amount);
        Assert.Equal(1m, Assert.Single(store.ForTask("T-1")).Amount);
    }

    /// <summary>Rename onto a name that already has spend merges the amounts instead of failing.</summary>
    [Fact]
    public void Rename_ToNameWithExistingSpend_MergesAmounts()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());
        store.Add("Maya", null, "USD", 1m);
        store.Add("Mia", null, "USD", 2m);

        store.Rename("Maya", "Mia");

        Assert.Equal(3m, Assert.Single(store.ForPersona("Mia")).Amount);
        Assert.Empty(store.ForPersona("Maya"));
    }

    /// <summary>Remove deletes every row of that Persona and no other.</summary>
    [Fact]
    public void Remove_DeletesOnlyThatPersona()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());
        store.Add("Maya", "T-1", "USD", 1m);
        store.Add("Noor", "T-1", "USD", 5m);

        store.Remove("MAYA");

        Assert.Empty(store.ForPersona("Maya"));
        Assert.Equal(5m, Assert.Single(store.ForTask("T-1")).Amount);
    }

    /// <summary>A second store over the same data folder sees the earlier data (survives a restart).</summary>
    [Fact]
    public void SecondStoreOverSameDataDir_SeesEarlierData()
    {
        using TempDataDir dir = new();
        PersonaSpendStore first = new(dir.Options());
        first.Add("Maya", "T-1", "USD", 1.25m);

        PersonaSpendStore second = new(dir.Options());

        Assert.Equal(1.25m, Assert.Single(second.ForPersona("Maya")).Amount);
        Assert.Equal(1.25m, Assert.Single(second.ForTask("T-1")).Amount);
    }

    /// <summary>Decimal amounts add exactly, with no binary floating-point drift.</summary>
    [Fact]
    public void Add_PointOnePlusPointTwo_SumsExactly()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        store.Add("Maya", null, "USD", 0.1m);
        store.Add("Maya", null, "USD", 0.2m);

        Assert.Equal(0.3m, Assert.Single(store.ForPersona("Maya")).Amount);
    }

    /// <summary>ForPersonas sums per currency across the given Personas and ignores others.</summary>
    [Fact]
    public void ForPersonas_SumsPerCurrencyAcrossPersonas()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());
        store.Add("Maya", null, "USD", 1m);
        store.Add("Noor", "T-1", "USD", 2m);
        store.Add("Noor", null, "EUR", 4m);
        store.Add("Zed", null, "USD", 100m);

        IReadOnlyList<StoredSpend> total = store.ForPersonas(["maya", "Noor"]);

        Assert.Equal(2, total.Count);
        Assert.Equal(3m, Assert.Single(total, s => s.Currency == "USD").Amount);
        Assert.Equal(4m, Assert.Single(total, s => s.Currency == "EUR").Amount);
    }

    /// <summary>Lookups for an unknown Persona or Task return an empty list.</summary>
    [Fact]
    public void Lookups_Unknown_ReturnEmpty()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        Assert.Empty(store.ForPersona("nobody"));
        Assert.Empty(store.ForTask("T-9"));
        Assert.Empty(store.ForPersonas([]));
    }

    /// <summary>Public methods reject null or blank arguments with the matching exception.</summary>
    [Fact]
    public void PublicMethods_InvalidArguments_Throw()
    {
        using TempDataDir dir = new();
        PersonaSpendStore store = new(dir.Options());

        Assert.Throws<ArgumentException>(() => store.Add(" ", null, "USD", 1m));
        Assert.Throws<ArgumentException>(() => store.Add("Maya", null, "", 1m));
        Assert.Throws<ArgumentNullException>(() => store.ForPersona(null!));
        Assert.Throws<ArgumentNullException>(() => store.ForPersonas(null!));
        Assert.Throws<ArgumentException>(() => store.ForTask(""));
        Assert.Throws<ArgumentException>(() => store.Rename("Maya", ""));
        Assert.Throws<ArgumentException>(() => store.Remove(""));
    }
}

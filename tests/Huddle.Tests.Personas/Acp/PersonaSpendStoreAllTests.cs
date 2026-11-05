using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="PersonaSpendStore.All"/>: the Spending panel's Total is built from one amount per Persona and
/// currency, so an Agent in two Teams counts once and two currencies never add together.
/// </summary>
public sealed class PersonaSpendStoreAllTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly PersonaSpendStore store;

    /// <summary>Opens a <see cref="PersonaSpendStore"/> over a temp data folder.</summary>
    public PersonaSpendStoreAllTests() => this.store = new PersonaSpendStore(this.dir.Options());

    /// <summary>Deletes the temp data folder.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>The rows in a stable order, as plain text, so an assertion reads at a glance.</summary>
    private IReadOnlyList<string> Rows() =>
        [.. this.store.All()
            .OrderBy(static row => row.PersonaName, StringComparer.Ordinal)
            .ThenBy(static row => row.Currency, StringComparer.Ordinal)
            .Select(static row => $"{row.PersonaName}|{row.Currency}|{row.Amount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}")];

    /// <summary>A store with nothing in it returns no rows, never a zero row.</summary>
    [Fact]
    public void All_NothingStored_IsEmpty()
    {
        Assert.Empty(this.store.All());
    }

    /// <summary>A Persona's spend over several Tasks, including the no-Task row, is one amount per currency.</summary>
    [Fact]
    public void All_SeveralTasks_SumsIntoOneRowPerPersonaAndCurrency()
    {
        this.store.Add("coo", "t1", "USD", 1.25m);
        this.store.Add("coo", "t2", "USD", 2.5m);
        this.store.Add("coo", null, "USD", 0.25m);

        Assert.Equal(["coo|USD|4"], this.Rows());
    }

    /// <summary>One Persona billed in two currencies gets two rows; the amounts are never added across currencies.</summary>
    [Fact]
    public void All_TwoCurrencies_StayApart()
    {
        this.store.Add("coo", "t1", "USD", 3m);
        this.store.Add("coo", "t1", "EUR", 5m);

        Assert.Equal(["coo|EUR|5", "coo|USD|3"], this.Rows());
    }

    /// <summary>Each Persona has its own rows, so a caller can count an Agent once however many Teams it is in.</summary>
    [Fact]
    public void All_SeveralPersonas_OneRowEachPerCurrency()
    {
        this.store.Add("coo", "t1", "USD", 1m);
        this.store.Add("dev", "t1", "USD", 2m);
        this.store.Add("dev", "t2", "EUR", 4m);

        Assert.Equal(["coo|USD|1", "dev|EUR|4", "dev|USD|2"], this.Rows());
    }

    /// <summary>A removed Persona's rows are gone, which is why the Total drops when an Agent is removed.</summary>
    [Fact]
    public void All_AfterRemove_OmitsThePersona()
    {
        this.store.Add("coo", "t1", "USD", 1m);
        this.store.Add("dev", "t1", "USD", 2m);
        this.store.Remove("coo");

        Assert.Equal(["dev|USD|2"], this.Rows());
    }

    /// <summary>Names that differ only in case are one Persona, as every other lookup in the store treats them.</summary>
    [Fact]
    public void All_NamesDifferingInCase_AreOnePersona()
    {
        this.store.Add("Coo", "t1", "USD", 1m);
        this.store.Add("coo", "t2", "USD", 2m);

        PersonaSpendRow row = Assert.Single(this.store.All());
        Assert.Equal(3m, row.Amount);
    }
}

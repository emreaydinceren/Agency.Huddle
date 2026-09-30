using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="PersonaSpend"/>: how an Adapter's running total becomes a per-Persona sum that is
/// right through session re-opens and Adapter restarts. Spend is display-only, so no test here reads
/// it as a verdict.
/// </summary>
public sealed class PersonaSpendTests
{
    /// <summary>Builds an empty <see cref="PersonaSpend"/> that logs nowhere.</summary>
    private static PersonaSpend NewSpend() => new(NullLogger<PersonaSpend>.Instance);

    /// <summary>The first running total a session reports is all new spend.</summary>
    [Fact]
    public void Add_FirstSighting_AddsTheWholeTotal()
    {
        PersonaSpend spend = NewSpend();

        spend.Add("nova", "s1", 0.0548m, "USD");

        Assert.Equal([new SpendAmount(0.0548m, "USD")], spend.Get("nova"));
    }

    /// <summary>A total that rises counts only the rise, so three Turns of one session sum to the last total, not to 0.0548 plus 0.0748 plus 0.0908.</summary>
    [Fact]
    public void Add_RisingTotal_AddsOnlyTheIncrease()
    {
        PersonaSpend spend = NewSpend();

        spend.Add("nova", "s1", 0.0548m, "USD");
        spend.Add("nova", "s1", 0.0748m, "USD");
        spend.Add("nova", "s1", 0.0908m, "USD");

        Assert.Equal([new SpendAmount(0.0908m, "USD")], spend.Get("nova"));
    }

    /// <summary>An unchanged total adds nothing and tells nobody, so a repeat report never repaints a card.</summary>
    [Fact]
    public void Add_EqualTotal_AddsNothingAndRaisesNothing()
    {
        PersonaSpend spend = NewSpend();
        spend.Add("nova", "s1", 0.05m, "USD");
        List<string> raised = [];
        spend.SpendChanged += raised.Add;

        spend.Add("nova", "s1", 0.05m, "USD");

        Assert.Equal([new SpendAmount(0.05m, "USD")], spend.Get("nova"));
        Assert.Empty(raised);
    }

    /// <summary>A lower total means the Adapter process is new and its counter restarted, so the whole lower total is new spend.</summary>
    [Fact]
    public void Add_LowerTotal_IsTreatedAsARestart()
    {
        PersonaSpend spend = NewSpend();
        spend.Add("nova", "s1", 0.363m, "USD");

        spend.Add("nova", "s1", 0.055m, "USD");

        Assert.Equal([new SpendAmount(0.418m, "USD")], spend.Get("nova"));
    }

    /// <summary>A Room Session closed and re-opened in the same Adapter process presents the same session id and total, which adds zero.</summary>
    [Fact]
    public void Add_SameSessionReopened_DoesNotDoubleCount()
    {
        PersonaSpend spend = NewSpend();
        spend.Add("nova", "s1", 0.0908m, "USD");

        spend.Add("nova", "s1", 0.0908m, "USD");
        spend.Add("nova", "s1", 0.1108m, "USD");

        Assert.Equal([new SpendAmount(0.1108m, "USD")], spend.Get("nova"));
    }

    /// <summary>Two sessions of one Persona each have their own running total, and their spend adds.</summary>
    [Fact]
    public void Add_TwoSessions_AreIndependent()
    {
        PersonaSpend spend = NewSpend();

        spend.Add("nova", "s1", 0.10m, "USD");
        spend.Add("nova", "s2", 0.04m, "USD");
        spend.Add("nova", "s1", 0.12m, "USD");

        Assert.Equal([new SpendAmount(0.16m, "USD")], spend.Get("nova"));
    }

    /// <summary>Two currencies are kept apart and never summed into one figure.</summary>
    [Fact]
    public void Add_TwoCurrencies_AreKeptApart()
    {
        PersonaSpend spend = NewSpend();

        spend.Add("nova", "s1", 0.40m, "USD");
        spend.Add("nova", "s1", 1.25m, "EUR");

        Assert.Equal(
            [new SpendAmount(1.25m, "EUR"), new SpendAmount(0.40m, "USD")],
            spend.Get("nova"));
    }

    /// <summary>A Persona's name is compared without regard to case, like the rest of the Persona stores.</summary>
    [Fact]
    public void Add_PersonaNameCase_IsIgnored()
    {
        PersonaSpend spend = NewSpend();

        spend.Add("Nova", "s1", 0.10m, "USD");
        spend.Add("nova", "s1", 0.15m, "USD");

        Assert.Equal([new SpendAmount(0.15m, "USD")], spend.Get("NOVA"));
    }

    /// <summary>A negative amount or a blank currency is ignored rather than recorded, because Spend is display-only and a bad figure must not reach a card.</summary>
    [Theory]
    [InlineData(-0.01, "USD")]
    [InlineData(0.05, "")]
    [InlineData(0.05, "   ")]
    public void Add_NegativeAmountOrBlankCurrency_IsIgnored(double amount, string currency)
    {
        PersonaSpend spend = NewSpend();
        List<string> raised = [];
        spend.SpendChanged += raised.Add;

        spend.Add("nova", "s1", (decimal)amount, currency);

        Assert.Empty(spend.Get("nova"));
        Assert.Empty(raised);
    }

    /// <summary>A Persona that has reported nothing has no Spend, so the card shows no line rather than a zero.</summary>
    [Fact]
    public void Get_ForAPersonaThatNeverReported_IsEmpty()
    {
        PersonaSpend spend = NewSpend();

        Assert.Empty(spend.Get("nova"));
    }

    /// <summary>A renamed Persona keeps its Spend and its per-session memory, so the next report after a rename adds only the increase.</summary>
    [Fact]
    public void Rename_MovesTheEntry()
    {
        PersonaSpend spend = NewSpend();
        spend.Add("nova", "s1", 0.10m, "USD");

        spend.Rename("nova", "astra");
        spend.Add("astra", "s1", 0.12m, "USD");

        Assert.Empty(spend.Get("nova"));
        Assert.Equal([new SpendAmount(0.12m, "USD")], spend.Get("astra"));
    }

    /// <summary>A removed Persona's Spend is forgotten, so a new Persona that reuses the name starts from zero.</summary>
    [Fact]
    public void Forget_ClearsTheEntry()
    {
        PersonaSpend spend = NewSpend();
        spend.Add("nova", "s1", 0.10m, "USD");

        spend.Forget("nova");

        Assert.Empty(spend.Get("nova"));
    }

    /// <summary>A rise raises <see cref="PersonaSpend.SpendChanged"/> once, with the Persona's name as it was reported.</summary>
    [Fact]
    public void Add_Rise_RaisesSpendChangedWithTheName()
    {
        PersonaSpend spend = NewSpend();
        List<string> raised = [];
        spend.SpendChanged += raised.Add;

        spend.Add("nova", "s1", 0.10m, "USD");

        Assert.Equal(["nova"], raised);
    }

    /// <summary>The event fires after the lock is released: a handler that waits on another thread which reads Spend must not deadlock.</summary>
    [Fact]
    public void SpendChanged_IsRaisedOutsideTheLock()
    {
        PersonaSpend spend = NewSpend();
        IReadOnlyList<SpendAmount>? seenOnAnotherThread = null;
        spend.SpendChanged += name =>
        {
            Task<IReadOnlyList<SpendAmount>> read = Task.Run(() => spend.Get(name));
            Assert.True(read.Wait(TimeSpan.FromSeconds(5)), "Get blocked while SpendChanged was being raised: the event fired inside the lock.");
            seenOnAnotherThread = read.Result;
        };

        spend.Add("nova", "s1", 0.10m, "USD");

        Assert.Equal([new SpendAmount(0.10m, "USD")], seenOnAnotherThread);
    }

    /// <summary>A handler that throws is caught, and the handlers after it still run, so one broken card cannot silence the others.</summary>
    [Fact]
    public void SpendChanged_AThrowingHandler_DoesNotStopTheOthers()
    {
        PersonaSpend spend = NewSpend();
        int secondRan = 0;
        spend.SpendChanged += _ => throw new InvalidOperationException("a broken subscriber");
        spend.SpendChanged += _ => secondRan++;

        spend.Add("nova", "s1", 0.10m, "USD");

        Assert.Equal(1, secondRan);
        Assert.Equal([new SpendAmount(0.10m, "USD")], spend.Get("nova"));
    }

    /// <summary>
    /// Many sessions adding on real threads still sum exactly: every increase lands once, none is
    /// lost and none is counted twice. Repeated so a missing lock is a near-certain failure, not a
    /// lucky pass.
    /// </summary>
    [Fact]
    public void ConcurrentAdds_SumExactly()
    {
        const int Rounds = 30;
        const int Sessions = 8;
        const int Steps = 200;

        for (int round = 0; round < Rounds; round++)
        {
            PersonaSpend spend = NewSpend();
            Parallel.For(
                0,
                Sessions,
                new ParallelOptions { MaxDegreeOfParallelism = Sessions },
                session =>
                {
                    for (int step = 1; step <= Steps; step++)
                    {
                        spend.Add("nova", $"s{session}", step, "USD");
                    }
                });

            Assert.Equal([new SpendAmount(Sessions * Steps, "USD")], spend.Get("nova"));
        }
    }
}

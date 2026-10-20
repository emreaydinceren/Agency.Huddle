using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <see cref="HelperStatusStore"/> keeps the last run and the last error per Helper across a restart, and
/// <see cref="NeedsHumanOverrideStore"/> keeps the Tasks the Human turned the gate off on (Helpers Spec §6.5 and §6.9, plan HL-6, HL-11).
/// </summary>
public sealed class HelperStatusStoreTests
{
    private static readonly HelperId NeedsHuman = new("NeedsHuman");
    private static readonly DateTimeOffset T1 = new(2026, 10, 20, 8, 42, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 10, 20, 8, 50, 0, TimeSpan.Zero);

    /// <summary>A Helper never run has an empty status.</summary>
    [Fact]
    public void Get_NeverRun_IsEmpty()
    {
        using TempDataDir dir = new();
        HelperStatusStore store = new(dir.Options());

        HelperStatus status = store.Get(NeedsHuman);

        Assert.Null(status.LastRunAt);
        Assert.Null(status.LastResult);
        Assert.Null(status.LastError);
        Assert.Equal(0, status.ConsecutiveFailures);
    }

    /// <summary>A success records the result and resets the failure count but keeps the earlier error.</summary>
    [Fact]
    public void RecordSuccess_SetsResultResetsFailuresKeepsError()
    {
        using TempDataDir dir = new();
        HelperStatusStore store = new(dir.Options());
        store.RecordFailure(NeedsHuman, T1, "Timed out after 60 s");
        store.RecordFailure(NeedsHuman, T1, "Timed out after 60 s");

        store.RecordSuccess(NeedsHuman, T2, "Turned the gate on for RSH-0208");

        HelperStatus status = store.Get(NeedsHuman);
        Assert.Equal("Turned the gate on for RSH-0208", status.LastResult);
        Assert.Equal(T2, status.LastRunAt);
        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.Equal("Timed out after 60 s", status.LastError);
        Assert.Equal(T1, status.LastErrorAt);
    }

    /// <summary>A failure records the error and time, adds one to the count, and keeps the earlier result with its own run time.</summary>
    [Fact]
    public void RecordFailure_SetsErrorAddsOneKeepsResult()
    {
        using TempDataDir dir = new();
        HelperStatusStore store = new(dir.Options());
        store.RecordSuccess(NeedsHuman, T1, "No change");

        store.RecordFailure(NeedsHuman, T2, "The answer wasn't the JSON the prompt asks for (no object)");

        HelperStatus status = store.Get(NeedsHuman);
        Assert.Equal("No change", status.LastResult);
        Assert.Equal(T1, status.LastRunAt);
        Assert.Equal("The answer wasn't the JSON the prompt asks for (no object)", status.LastError);
        Assert.Equal(T2, status.LastErrorAt);
        Assert.Equal(1, status.ConsecutiveFailures);
    }

    /// <summary>The status is still there for a new store instance, as after a restart.</summary>
    [Fact]
    public void Get_NewStoreOverSameDatabase_SeesEarlierStatus()
    {
        using TempDataDir dir = new();
        new HelperStatusStore(dir.Options()).RecordFailure(NeedsHuman, T1, "boom");

        HelperStatus status = new HelperStatusStore(dir.Options()).Get(NeedsHuman);

        Assert.Equal("boom", status.LastError);
        Assert.Equal(1, status.ConsecutiveFailures);
    }

    /// <summary>An override is remembered ignoring case and survives a new store instance.</summary>
    [Fact]
    public void Override_AddedThenRestart_IsStillContained()
    {
        using TempDataDir dir = new();
        NeedsHumanOverrideStore store = new(dir.Options(), TimeProvider.System);
        Assert.False(store.Contains("RSH-0208"));

        store.Add("RSH-0208");

        Assert.True(store.Contains("rsh-0208"));
        Assert.True(new NeedsHumanOverrideStore(dir.Options(), TimeProvider.System).Contains("RSH-0208"));
        Assert.False(store.Contains("RSH-0209"));
    }
}

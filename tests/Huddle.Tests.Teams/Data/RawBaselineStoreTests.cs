using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Data;

/// <summary>Pins the <c>raw_baselines</c> table (Spec 7.6): upsert on path, '/' separators, persistence in <c>team.db</c>.</summary>
public sealed class RawBaselineStoreTests : IDisposable
{
    private const string Path = "Teams/Home/raw/r1/a.pdf";

    private static readonly DateTimeOffset Recorded = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 1, 8, 30, 15, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 11, 2, 7, 45, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();
    private readonly ManualTimeProvider clock = new(Recorded);

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>A recorded path reads back with its size, modified time and the clock's time as the recorded time.</summary>
    [Fact]
    public void Record_ThenTryGet_ReturnsBaselineWithClockRecordedTime()
    {
        RawBaselineStore store = this.CreateStore();

        store.Record(Path, 1234, T1);

        Assert.True(store.TryGet(Path, out RawBaseline? baseline));
        Assert.Equal(new RawBaseline(Path, 1234, T1, Recorded), baseline);
    }

    /// <summary>Recording the same path again replaces the row with the new size and times.</summary>
    [Fact]
    public void Record_SamePathAgain_ReplacesRow()
    {
        RawBaselineStore store = this.CreateStore();
        store.Record(Path, 1234, T1);
        DateTimeOffset later = Recorded.AddHours(2);
        this.clock.UtcNow = later;

        store.Record(Path, 99, T2);

        Assert.True(store.TryGet(Path, out RawBaseline? baseline));
        Assert.Equal(new RawBaseline(Path, 99, T2, later), baseline);
    }

    /// <summary>A path that was never recorded is not found.</summary>
    [Fact]
    public void TryGet_UnknownPath_ReturnsFalse()
    {
        RawBaselineStore store = this.CreateStore();
        store.Record(Path, 1234, T1);

        Assert.False(store.TryGet("Teams/Home/raw/r1/other.pdf", out RawBaseline? baseline));
        Assert.Null(baseline);
    }

    /// <summary>A path given with backslashes is stored and found with forward slashes, and the other way round.</summary>
    [Fact]
    public void Record_BackslashPath_IsFoundWithForwardSlashes()
    {
        RawBaselineStore store = this.CreateStore();

        store.Record(@"Teams\Home\raw\r1\a.pdf", 1234, T1);

        Assert.True(store.TryGet(Path, out RawBaseline? baseline));
        Assert.Equal(Path, baseline.Path);
        Assert.True(store.TryGet(@"Teams\Home\raw\r1\a.pdf", out RawBaseline? viaBackslash));
        Assert.Equal(Path, viaBackslash.Path);
    }

    /// <summary>A second store over the same data folder sees the row, so the baseline survives a restart.</summary>
    [Fact]
    public void TryGet_SecondStoreOverSameDataDir_SeesRow()
    {
        this.CreateStore().Record(Path, 1234, T1);

        RawBaselineStore second = this.CreateStore();

        Assert.True(second.TryGet(Path, out RawBaseline? baseline));
        Assert.Equal(new RawBaseline(Path, 1234, T1, Recorded), baseline);
    }

    private RawBaselineStore CreateStore() => new(this.dir.Options(), this.clock);
}

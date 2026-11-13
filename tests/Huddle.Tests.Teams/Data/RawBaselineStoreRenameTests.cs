using Agency.Huddle.App.Data;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// <see cref="RawBaselineStore.RenamePrefix"/> (Team Rename Spec 4, Ledgers): the rows under a Team's folder move to the new folder
/// name with their sizes and times intact. The prefix is compared as text, so a <c>_</c> in a Team name is not a wildcard.
/// </summary>
public sealed class RawBaselineStoreRenameTests : IDisposable
{
    private static readonly DateTimeOffset Recorded = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 1, 8, 30, 15, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 11, 2, 7, 45, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>A row under the old prefix is read back at the new path with its size, times and recorded time; the old path is gone.</summary>
    [Fact]
    public void RenamePrefix_MovesRowsUnderThePrefix()
    {
        RawBaselineStore store = new(this.dir.Options(), new ManualTimeProvider(Recorded));
        store.Record("Teams/Growth/raw/r1/a.pdf", 1234, T1);
        store.Record("Teams/Growth/raw/r2/b.pdf", 99, T2);

        store.RenamePrefix("Teams/Growth", "Teams/Revenue");

        Assert.True(store.TryGet("Teams/Revenue/raw/r1/a.pdf", out RawBaseline? first));
        Assert.Equal(new RawBaseline("Teams/Revenue/raw/r1/a.pdf", 1234, T1, Recorded), first);
        Assert.True(store.TryGet("Teams/Revenue/raw/r2/b.pdf", out RawBaseline? second));
        Assert.Equal(new RawBaseline("Teams/Revenue/raw/r2/b.pdf", 99, T2, Recorded), second);
        Assert.False(store.TryGet("Teams/Growth/raw/r1/a.pdf", out _));
        Assert.False(store.TryGet("Teams/Growth/raw/r2/b.pdf", out _));
    }

    /// <summary>A <c>_</c> in the old prefix matches only itself: a row whose path differs at that character stays.</summary>
    [Fact]
    public void RenamePrefix_UnderscoreInTeamName_IsNotAWildcard()
    {
        RawBaselineStore store = new(this.dir.Options(), new ManualTimeProvider(Recorded));
        store.Record("Teams/A_b/raw/r1/a.pdf", 1, T1);
        store.Record("Teams/AXb/raw/r1/a.pdf", 2, T1);

        store.RenamePrefix("Teams/A_b", "Teams/C");

        Assert.True(store.TryGet("Teams/C/raw/r1/a.pdf", out RawBaseline? moved));
        Assert.Equal(1, moved.SizeBytes);
        Assert.True(store.TryGet("Teams/AXb/raw/r1/a.pdf", out RawBaseline? other));
        Assert.Equal(2, other.SizeBytes);
        Assert.False(store.TryGet("Teams/A_b/raw/r1/a.pdf", out _));
    }

    /// <summary>A row already at the new path is replaced by the moved row.</summary>
    [Fact]
    public void RenamePrefix_ClashWithExistingRow_ReplacesIt()
    {
        RawBaselineStore store = new(this.dir.Options(), new ManualTimeProvider(Recorded));
        store.Record("Teams/Growth/raw/r1/a.pdf", 10, T1);
        store.Record("Teams/Revenue/raw/r1/a.pdf", 20, T2);

        store.RenamePrefix("Teams/Growth", "Teams/Revenue");

        Assert.True(store.TryGet("Teams/Revenue/raw/r1/a.pdf", out RawBaseline? row));
        Assert.Equal(10, row.SizeBytes);
        Assert.Equal(T1, row.ModifiedUtc);
        Assert.False(store.TryGet("Teams/Growth/raw/r1/a.pdf", out _));
    }

    /// <summary>A row outside the prefix is left as it was.</summary>
    [Fact]
    public void RenamePrefix_RowOutsideThePrefix_Untouched()
    {
        RawBaselineStore store = new(this.dir.Options(), new ManualTimeProvider(Recorded));
        store.Record("Teams/Ops/raw/r1/a.pdf", 5, T1);

        store.RenamePrefix("Teams/Growth", "Teams/Revenue");

        Assert.True(store.TryGet("Teams/Ops/raw/r1/a.pdf", out RawBaseline? row));
        Assert.Equal(new RawBaseline("Teams/Ops/raw/r1/a.pdf", 5, T1, Recorded), row);
    }
}

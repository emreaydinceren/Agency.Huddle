using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>Tests for <see cref="CadenceStateStore.Rename"/> (Team rename spec §4 Cadence row, §5.3).</summary>
public sealed class CadenceStateStoreRenameTests
{
    private static readonly DateTimeOffset Digest = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Retro = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The row moves to the new name with its times and count, and the old name has none.</summary>
    [Fact]
    public void Rename_MovesRow()
    {
        using TempDataDir dir = new();
        CadenceStateStore store = new(dir.Options());
        store.Set(new CadenceState("Growth", Digest, Retro, 7));

        store.Rename("Growth", "Reach");

        Assert.Null(store.Get("Growth"));
        CadenceState? moved = store.Get("Reach");
        Assert.NotNull(moved);
        Assert.Equal(Digest, moved.LastDigestUtc);
        Assert.Equal(Retro, moved.LastRetroUtc);
        Assert.Equal(7, moved.DoneAtLastRetro);
    }

    /// <summary>A leftover row under the new name is replaced by the old row's state.</summary>
    [Fact]
    public void Rename_LeftoverRowForNewName_IsReplaced()
    {
        using TempDataDir dir = new();
        CadenceStateStore store = new(dir.Options());
        store.Set(new CadenceState("Growth", Digest, Retro, 7));
        store.Set(new CadenceState("Reach", null, null, 99));

        store.Rename("Growth", "Reach");

        Assert.Null(store.Get("Growth"));
        CadenceState? moved = store.Get("Reach");
        Assert.NotNull(moved);
        Assert.Equal(Digest, moved.LastDigestUtc);
        Assert.Equal(7, moved.DoneAtLastRetro);
    }

    /// <summary>With no row for the old name nothing changes, including a row already under the new name.</summary>
    [Fact]
    public void Rename_OldAbsent_IsNoOp()
    {
        using TempDataDir dir = new();
        CadenceStateStore store = new(dir.Options());
        store.Set(new CadenceState("Reach", Digest, Retro, 7));

        store.Rename("Growth", "Reach");

        CadenceState? kept = store.Get("Reach");
        Assert.NotNull(kept);
        Assert.Equal(Digest, kept.LastDigestUtc);
        Assert.Equal(7, kept.DoneAtLastRetro);
        Assert.Null(store.Get("Growth"));
    }

    /// <summary>A case-only rename keeps the row's state.</summary>
    [Fact]
    public void Rename_CaseOnly_KeepsState()
    {
        using TempDataDir dir = new();
        CadenceStateStore store = new(dir.Options());
        store.Set(new CadenceState("Growth", Digest, Retro, 7));

        store.Rename("Growth", "growth");

        CadenceState? after = store.Get("growth");
        Assert.NotNull(after);
        Assert.Equal(Digest, after.LastDigestUtc);
        Assert.Equal(7, after.DoneAtLastRetro);
    }
}

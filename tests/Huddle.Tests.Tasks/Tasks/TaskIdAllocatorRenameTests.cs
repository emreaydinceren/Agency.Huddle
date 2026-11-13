using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskIdAllocator.HasPrefix"/> and <see cref="TaskIdAllocator.RenameTeam"/> (Team rename spec §5.3, §5.5).</summary>
public sealed class TaskIdAllocatorRenameTests
{
    /// <summary>HasPrefix is true once a Team has a row, ignoring case, and false for a Team never seen.</summary>
    [Fact]
    public void HasPrefix_ReflectsRows()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        _ = allocator.PrefixFor("Growth");

        Assert.True(allocator.HasPrefix("Growth"));
        Assert.True(allocator.HasPrefix("growth"));
        Assert.False(allocator.HasPrefix("Sales"));
    }

    /// <summary>The prefix and the counter survive a rename: the next id continues the sequence with the same prefix.</summary>
    [Fact]
    public void RenameTeam_KeepsPrefixAndCounter()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        TaskId first = allocator.Next("Growth", 0);
        TaskId second = allocator.Next("Growth", 0);

        allocator.RenameTeam("Growth", "Reach");

        TaskId third = allocator.Next("Reach", 0);
        Assert.Equal("GROW", first.Prefix);
        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
        Assert.Equal("GROW", third.Prefix);
        Assert.Equal(3, third.Number);
        Assert.Equal("GROW", allocator.PrefixFor("Reach"));
        Assert.False(allocator.HasPrefix("Growth"));
        Assert.True(allocator.HasPrefix("Reach"));
    }

    /// <summary>The moved prefix is still known to <see cref="ITaskPrefixes.IsKnown"/>.</summary>
    [Fact]
    public void RenameTeam_PrefixStaysKnown()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        _ = allocator.PrefixFor("Growth");

        allocator.RenameTeam("Growth", "Reach");

        Assert.True(allocator.IsKnown("GROW"));
    }

    /// <summary>A leftover row for the new name makes the rename throw and leaves both rows unchanged.</summary>
    [Fact]
    public void RenameTeam_LeftoverRowForNewName_ThrowsAndChangesNothing()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        _ = allocator.Next("Growth", 0);
        _ = allocator.Next("Reach", 0);
        _ = allocator.Next("Reach", 0);
        string growthPrefix = allocator.PrefixFor("Growth");
        string reachPrefix = allocator.PrefixFor("Reach");

        _ = Assert.Throws<InvalidOperationException>(() => allocator.RenameTeam("Growth", "Reach"));

        Assert.Equal(growthPrefix, allocator.PrefixFor("Growth"));
        Assert.Equal(reachPrefix, allocator.PrefixFor("Reach"));
        Assert.Equal(2, allocator.Next("Growth", 0).Number);
        Assert.Equal(3, allocator.Next("Reach", 0).Number);
    }

    /// <summary>A case-only rename is the same row and is allowed.</summary>
    [Fact]
    public void RenameTeam_CaseOnly_IsAllowed()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        _ = allocator.Next("Growth", 0);

        allocator.RenameTeam("Growth", "growth");

        Assert.True(allocator.HasPrefix("growth"));
        Assert.Equal(2, allocator.Next("growth", 0).Number);
    }

    /// <summary>With no row for the old name the call is a no-op, so replaying it is safe.</summary>
    [Fact]
    public void RenameTeam_OldRowAbsent_IsNoOp()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        _ = allocator.Next("Reach", 0);

        allocator.RenameTeam("Growth", "Reach");

        Assert.False(allocator.HasPrefix("Growth"));
        Assert.Equal(2, allocator.Next("Reach", 0).Number);
    }

    /// <summary>Replaying a completed rename leaves the moved row as it was.</summary>
    [Fact]
    public void RenameTeam_SecondCall_IsIdempotent()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());
        _ = allocator.Next("Growth", 0);
        allocator.RenameTeam("Growth", "Reach");

        allocator.RenameTeam("Growth", "Reach");

        Assert.Equal(2, allocator.Next("Reach", 0).Number);
        Assert.False(allocator.HasPrefix("Growth"));
    }
}

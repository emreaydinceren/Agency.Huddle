using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for <see cref="TaskIdAllocator"/>: Spec §8.6, per-Team prefix derivation and
/// number allocation stored in <c>team.db</c>.</summary>
public sealed class TaskIdAllocatorTests
{
    /// <summary>Prefix derivation: ASCII letters and digits only, upper-cased, leading digits
    /// skipped, first 4 characters (or "TASK" if none are left), with a numeric suffix added
    /// when the base prefix collides (case-insensitively) with an already-taken one.</summary>
    [Theory]
    [InlineData("Platform", new string[0], "PLAT")]
    [InlineData("ab", new string[0], "AB")]
    [InlineData("3D Print", new string[0], "DPRI")]
    [InlineData("Café Ops", new string[0], "CAFO")]
    [InlineData("123", new string[0], "TASK")]
    [InlineData("Platform", new[] { "PLAT" }, "PLAT2")]
    [InlineData("Platform", new[] { "PLAT", "PLAT2" }, "PLAT3")]
    [InlineData("Platform", new[] { "plat" }, "PLAT2")]
    public void DerivePrefix_Cases(string team, string[] taken, string expectedPrefix)
    {
        string prefix = TaskIdAllocator.DerivePrefix(team, new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase));

        Assert.Equal(expectedPrefix, prefix);
    }

    /// <summary>The first Task number allocated for a fresh Team is 1.</summary>
    [Fact]
    public void Next_FirstCall_IsNumber1()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());

        TaskId id = allocator.Next("Platform", highestNumberSeen: 0);

        Assert.Equal(1, id.Number);
    }

    /// <summary>Two successive allocations for the same Team increment the number.</summary>
    [Fact]
    public void Next_Twice_Increments()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());

        TaskId first = allocator.Next("Platform", highestNumberSeen: 0);
        TaskId second = allocator.Next("Platform", highestNumberSeen: 0);

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
    }

    /// <summary>A highestNumberSeen above the stored counter jumps the allocation past it, as in
    /// the Spec §8.6 worked example: Next("Platform", 41) gives PLAT-0042.</summary>
    [Fact]
    public void Next_HighestSeenAbove_JumpsPastIt()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());

        TaskId id = allocator.Next("Platform", highestNumberSeen: 41);

        Assert.Equal("PLAT-0042", id.ToString());
    }

    /// <summary>A highestNumberSeen below the stored counter is ignored: the allocation keeps
    /// incrementing from where it was, never moving backward.</summary>
    [Fact]
    public void Next_HighestSeenBelow_Ignored()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());

        TaskId jumped = allocator.Next("Platform", highestNumberSeen: 50);
        TaskId ignored = allocator.Next("Platform", highestNumberSeen: 0);

        Assert.Equal(51, jumped.Number);
        Assert.Equal(52, ignored.Number);
    }

    /// <summary>A Team's prefix persists across allocator instances backed by the same team.db.</summary>
    [Fact]
    public void PrefixFor_Persists_AcrossInstances()
    {
        using TempDataDir dir = new();
        TaskIdAllocator first = new(dir.Options());
        string firstPrefix = first.PrefixFor("Platform");

        TaskIdAllocator second = new(dir.Options());
        string secondPrefix = second.PrefixFor("Platform");

        Assert.Equal("PLAT", firstPrefix);
        Assert.Equal("PLAT", secondPrefix);
    }

    /// <summary>A second Team whose derived prefix collides with an already-stored one gets the
    /// numeric-suffix variant.</summary>
    [Fact]
    public void PrefixFor_SecondTeamColliding_GetsSuffix()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());

        string platform = allocator.PrefixFor("Platform");
        string plateau = allocator.PrefixFor("Plateau");

        Assert.Equal("PLAT", platform);
        Assert.Equal("PLAT2", plateau);
    }

    /// <summary>Team name lookups ignore case: "platform" resolves to the same prefix as
    /// "Platform".</summary>
    [Fact]
    public void PrefixFor_TeamNameIgnoresCase()
    {
        using TempDataDir dir = new();
        TaskIdAllocator allocator = new(dir.Options());

        string original = allocator.PrefixFor("Platform");
        string sameTeamDifferentCase = allocator.PrefixFor("platform");

        Assert.Equal(original, sameTeamDifferentCase);
    }
}

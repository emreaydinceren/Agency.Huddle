using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>Tests for the Second Brain reserved folder names in <see cref="TaskLayout"/>:
/// <c>memory</c>, <c>raw</c> and <c>outputs</c> are never Teams, and <c>raw</c> and <c>outputs</c>
/// are never Projects (Spec §6.1, SB-R02).</summary>
public sealed class TaskLayoutReservedTests
{
    /// <summary>A Task-looking path under a reserved Team-level folder (any case) or a reserved
    /// Project-level folder does not map.</summary>
    [Theory]
    [InlineData("memory", "_tasks", "X-0001.md")]
    [InlineData("RAW", "_tasks", "X-0001.md")]
    [InlineData("Home", "outputs", "_tasks", "HOME-0001.md")]
    [InlineData("Home", "raw", "_tasks", "HOME-0001.md")]
    public void TryMap_ReservedFolder_DoesNotMap(params string[] segments)
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine([root, .. segments]);

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out string? error);

        Assert.False(result);
        Assert.Null(location);
        Assert.Null(error);
    }

    /// <summary>The neighbouring ordinary Project folder still maps to its Team and Project.</summary>
    [Fact]
    public void TryMap_NeighbourProject_Maps()
    {
        string root = Path.Combine("root");
        string fullPath = Path.Combine(root, "Home", "Repairs", "_tasks", "HOME-0001.md");

        bool result = TaskLayout.TryMap(root, fullPath, out TaskLocation? location, out _);

        Assert.True(result);
        Assert.NotNull(location);
        Assert.Equal("Home", location.Team);
        Assert.Equal("Repairs", location.Project);
    }

    /// <summary>A change under the reserved <c>Teams/memory/</c> folder cannot affect Tasks.</summary>
    [Fact]
    public void AffectsTasks_UnderMemory_IsFalse()
    {
        string root = Path.Combine("root");

        Assert.False(TaskLayout.AffectsTasks(root, Path.Combine(root, "memory", "note.md")));
        Assert.False(TaskLayout.AffectsTasks(root, Path.Combine(root, "memory")));
    }

    /// <summary>A change under an ordinary Team folder still affects Tasks.</summary>
    [Fact]
    public void AffectsTasks_NeighbourTeam_IsTrue()
    {
        string root = Path.Combine("root");

        Assert.True(TaskLayout.AffectsTasks(root, Path.Combine(root, "Home", "_tasks", "HOME-0001.md")));
    }
}

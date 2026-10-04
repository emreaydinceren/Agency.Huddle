using Agency.Huddle.App.Desktop;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>Pins the single-instance behaviour of <see cref="InstanceLock"/>.</summary>
public sealed class InstanceLockTests
{
    /// <summary>The first acquisition succeeds and leaves <c>instance.lock</c> in the directory.</summary>
    [Fact]
    public void TryAcquire_FirstCall_ReturnsLockAndCreatesFile()
    {
        using TempDataDir dir = new();

        using InstanceLock? held = InstanceLock.TryAcquire(dir.Path);

        Assert.NotNull(held);
        Assert.True(File.Exists(Path.Combine(dir.Path, "instance.lock")));
    }

    /// <summary>A second acquisition of the same directory while the first is held returns null.</summary>
    [Fact]
    public void TryAcquire_WhileHeld_ReturnsNull()
    {
        using TempDataDir dir = new();
        using InstanceLock? first = InstanceLock.TryAcquire(dir.Path);

        InstanceLock? second = InstanceLock.TryAcquire(dir.Path);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    /// <summary>Once the first holder is disposed, a later acquisition succeeds.</summary>
    [Fact]
    public void TryAcquire_AfterDispose_Succeeds()
    {
        using TempDataDir dir = new();
        InstanceLock? first = InstanceLock.TryAcquire(dir.Path);
        Assert.NotNull(first);
        first.Dispose();

        using InstanceLock? third = InstanceLock.TryAcquire(dir.Path);

        Assert.NotNull(third);
    }

    /// <summary>Locks on different directories do not interfere with each other.</summary>
    [Fact]
    public void TryAcquire_DifferentDirectories_AreIndependent()
    {
        using TempDataDir dirA = new();
        using TempDataDir dirB = new();

        using InstanceLock? lockA = InstanceLock.TryAcquire(dirA.Path);
        using InstanceLock? lockB = InstanceLock.TryAcquire(dirB.Path);

        Assert.NotNull(lockA);
        Assert.NotNull(lockB);
    }

    /// <summary>A directory that does not exist yet is created, including missing parents.</summary>
    [Fact]
    public void TryAcquire_MissingDirectory_CreatesIt()
    {
        using TempDataDir dir = new();
        string nested = Path.Combine(dir.Path, "one", "two");

        using InstanceLock? held = InstanceLock.TryAcquire(nested);

        Assert.NotNull(held);
        Assert.True(Directory.Exists(nested));
        Assert.True(File.Exists(Path.Combine(nested, "instance.lock")));
    }

    /// <summary>Disposing the same lock twice does not throw.</summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        using TempDataDir dir = new();
        InstanceLock? held = InstanceLock.TryAcquire(dir.Path);
        Assert.NotNull(held);

        held.Dispose();
        Exception? thrown = Record.Exception(held.Dispose);

        Assert.Null(thrown);
    }
}

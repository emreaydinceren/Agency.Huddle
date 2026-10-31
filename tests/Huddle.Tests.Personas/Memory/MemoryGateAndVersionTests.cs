namespace Agency.Huddle.Tests.Memory;

using Agency.Huddle.App.Memory;

/// <summary>
/// Pins <see cref="MemoryVersion"/> and <see cref="MemoryFolderGate"/> (Instructions spec §5.4): the token is a hash of what
/// is on disk, and one gate serialises every writer of a folder.
/// </summary>
public sealed class MemoryGateAndVersionTests
{
    /// <summary>The token is the first 12 lower-case hex characters of the SHA-256 of the bytes (the hash of nothing is the well-known e3b0c44298fc).</summary>
    [Fact]
    public void Version_IsTheFirstTwelveHexCharactersOfSha256()
    {
        Assert.Equal("e3b0c44298fc", MemoryVersion.Of([]));
        Assert.Equal("ba7816bf8f01", MemoryVersion.Of("abc"u8.ToArray()));
    }

    /// <summary>The same bytes give the same token and one changed byte gives another.</summary>
    [Fact]
    public void Version_ChangesWithContentOnly()
    {
        byte[] original = "# Topic\n"u8.ToArray();
        byte[] edited = "# Topic!\n"u8.ToArray();

        Assert.Equal(MemoryVersion.Of(original), MemoryVersion.Of([.. original]));
        Assert.NotEqual(MemoryVersion.Of(original), MemoryVersion.Of(edited));
    }

    /// <summary>Two runs on the same folder never overlap, even when the paths differ only in case.</summary>
    [Fact]
    public async Task Gate_SameFolder_SerialisesRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryFolderGate gate = new();
        int inside = 0;
        int overlaps = 0;

        async Task<int> Run(string folder)
        {
            return await gate.RunAsync(
                folder,
                async () =>
                {
                    if (Interlocked.Increment(ref inside) > 1)
                    {
                        Interlocked.Increment(ref overlaps);
                    }

                    await Task.Delay(20, ct);
                    Interlocked.Decrement(ref inside);
                    return 1;
                },
                ct);
        }

        string folder = Path.Combine(Path.GetTempPath(), "huddle-gate", "memory");
        int[] results = await Task.WhenAll(Run(folder), Run(folder.ToUpperInvariant()), Run(folder), Run(folder));

        Assert.Equal(4, results.Sum());
        Assert.Equal(0, overlaps);
    }

    /// <summary>Two different folders do not wait for each other.</summary>
    [Fact]
    public async Task Gate_DifferentFolders_RunTogether()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryFolderGate gate = new();
        TaskCompletionSource firstInside = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> first = gate.RunAsync(
            Path.Combine(Path.GetTempPath(), "a", "memory"),
            async () =>
            {
                firstInside.SetResult();
                await releaseFirst.Task;
                return true;
            },
            ct);
        await firstInside.Task;

        bool second = await gate.RunAsync(Path.Combine(Path.GetTempPath(), "b", "memory"), () => Task.FromResult(true), ct);
        releaseFirst.SetResult();

        Assert.True(second);
        Assert.True(await first);
    }

    /// <summary>An action that throws still releases the gate, so the next run is not stuck behind it.</summary>
    [Fact]
    public async Task Gate_ActionThrows_ReleasesTheGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryFolderGate gate = new();
        string folder = Path.Combine(Path.GetTempPath(), "huddle-gate", "throws");

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.RunAsync<int>(folder, () => throw new InvalidOperationException("boom"), ct));
        int next = await gate.RunAsync(folder, () => Task.FromResult(7), ct);

        Assert.Equal(7, next);
    }

    /// <summary>A caller cancelled while it waits for the gate throws and does not take the gate from the holder.</summary>
    [Fact]
    public async Task Gate_CancelledWhileWaiting_ThrowsAndLeavesTheHolderAlone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryFolderGate gate = new();
        string folder = Path.Combine(Path.GetTempPath(), "huddle-gate", "cancel");
        TaskCompletionSource holding = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> holder = gate.RunAsync(
            folder,
            async () =>
            {
                holding.SetResult();
                await release.Task;
                return true;
            },
            ct);
        await holding.Task;

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<bool> waiter = gate.RunAsync(folder, () => Task.FromResult(true), cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        release.SetResult();
        Assert.True(await holder);
        Assert.True(await gate.RunAsync(folder, () => Task.FromResult(true), ct));
    }

    /// <summary>Disposing the gate while a holder is still inside does not make the holder fail when it releases.</summary>
    [Fact]
    public async Task Gate_DisposedWhileHeld_DoesNotFailTheHolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryFolderGate gate = new();
        TaskCompletionSource holding = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> holder = gate.RunAsync(
            Path.Combine(Path.GetTempPath(), "huddle-gate", "disposed"),
            async () =>
            {
                holding.SetResult();
                await release.Task;
                return 5;
            },
            ct);
        await holding.Task;

        gate.Dispose();
        release.SetResult();

        Assert.Equal(5, await holder);
    }

    /// <summary>A disposed gate refuses new runs instead of handing out a semaphore that no longer works.</summary>
    [Fact]
    public async Task Gate_AfterDispose_RefusesNewRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        MemoryFolderGate gate = new();
        gate.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => gate.RunAsync(Path.Combine(Path.GetTempPath(), "huddle-gate", "after"), () => Task.FromResult(1), ct));
    }

    /// <summary>Many first-time callers racing for one folder all get the same gate, so none of them runs beside another.</summary>
    [Fact]
    public async Task Gate_ManyFirstCallers_ShareOneGate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryFolderGate gate = new();
        string folder = Path.Combine(Path.GetTempPath(), "huddle-gate", "race");
        int inside = 0;
        int overlaps = 0;

        Task<int>[] runs = [.. Enumerable.Range(0, 32).Select(_ => Task.Run(() => gate.RunAsync(
            folder,
            async () =>
            {
                if (Interlocked.Increment(ref inside) > 1)
                {
                    Interlocked.Increment(ref overlaps);
                }

                await Task.Yield();
                Interlocked.Decrement(ref inside);
                return 1;
            },
            ct), ct))];

        Assert.Equal(32, (await Task.WhenAll(runs)).Sum());
        Assert.Equal(0, overlaps);
    }
}
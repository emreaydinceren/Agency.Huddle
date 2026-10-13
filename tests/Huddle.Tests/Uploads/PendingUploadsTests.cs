using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Uploads;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>
/// Tests for <see cref="PendingUploads"/> (ChatUploads spec 6.4): the per-circuit, per-Room list of
/// attachment chips behind the composer.
/// </summary>
public sealed class PendingUploadsTests
{
    /// <summary>A stream that blocks every read until released, so a chip can be held in the Uploading state.</summary>
    private sealed class GatedStream : Stream
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly byte[] content = "hello"u8.ToArray();
        private int position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public void Release() => this.gate.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await this.gate.Task.WaitAsync(cancellationToken);
            int count = Math.Min(buffer.Length, this.content.Length - this.position);
            this.content.AsMemory(this.position, count).CopyTo(buffer);
            this.position += count;
            return count;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A finished upload is a Done chip carrying the saved path, size and kind, in the Room's list.</summary>
    [Fact]
    public async Task AddAsync_Completes_ChipIsDoneWithPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);

        PendingUpload chip = await pending.AddAsync(room.Id, "a.txt", _ => new MemoryStream("hello"u8.ToArray()), ct);

        Assert.Equal(UploadState.Done, chip.State);
        Assert.Equal("a.txt", chip.DisplayName);
        Assert.Equal(5, chip.Size);
        Assert.Equal(UploadKind.Text, chip.Kind);
        Assert.Equal(Path.Combine(harness.DataDir, "Uploads", room.Id, "a.txt"), chip.File?.FullPath);
        PendingUpload listed = Assert.Single(pending.For(room.Id));
        Assert.Equal(chip.Id, listed.Id);
    }

    /// <summary>A refused file leaves a Failed chip that carries the refusal text and stays in the list.</summary>
    [Fact]
    public async Task AddAsync_Refused_ChipIsFailedWithError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);

        PendingUpload chip = await pending.AddAsync(room.Id, "logo.svg", _ => new MemoryStream("<svg></svg>"u8.ToArray()), ct);

        Assert.Equal(UploadState.Failed, chip.State);
        Assert.Equal("SVG files can't be attached.", chip.Error);
        Assert.Single(pending.For(room.Id));
    }

    /// <summary>The chip is in the Uploading state, and <see cref="PendingUploads.Changed"/> fires, before the bytes arrive.</summary>
    [Fact]
    public async Task AddAsync_BytesNotYetArrived_ChipIsUploadingAndChangedRaised()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        int raised = 0;
        pending.Changed += () => raised++;
        GatedStream gated = new();

        Task<PendingUpload> upload = pending.AddAsync(room.Id, "a.txt", _ => gated, ct);

        PendingUpload chip = Assert.Single(pending.For(room.Id));
        Assert.Equal(UploadState.Uploading, chip.State);
        Assert.True(raised >= 1);
        gated.Release();
        PendingUpload done = await upload;
        Assert.Equal(UploadState.Done, done.State);
        Assert.Equal(UploadState.Done, Assert.Single(pending.For(room.Id)).State);
    }

    /// <summary>With a cap of two, a third chip is refused with the spec 7 copy, even while the first two are still uploading.</summary>
    [Fact]
    public async Task AddAsync_OverCountWhileUploading_IsRefusedWithCopy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct, maxUploadsPerMessage: 2);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        GatedStream first = new();
        GatedStream second = new();
        Task<PendingUpload> one = pending.AddAsync(room.Id, "1.txt", _ => first, ct);
        Task<PendingUpload> two = pending.AddAsync(room.Id, "2.txt", _ => second, ct);

        PendingUpload third = await pending.AddAsync(room.Id, "3.txt", _ => new MemoryStream("x"u8.ToArray()), ct);

        Assert.Equal(UploadState.Failed, third.State);
        Assert.Equal("A Message can carry up to 2 files.", third.Error);
        Assert.Equal(2, pending.For(room.Id).Count);
        Assert.False(File.Exists(Path.Combine(harness.DataDir, "Uploads", room.Id, "3.txt")));
        first.Release();
        second.Release();
        _ = await one;
        _ = await two;
    }

    /// <summary>Removing a Done chip deletes its saved file and drops the chip.</summary>
    [Fact]
    public async Task RemoveAsync_DoneChip_DeletesFileAndDropsChip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        PendingUpload chip = await pending.AddAsync(room.Id, "a.txt", _ => new MemoryStream("hello"u8.ToArray()), ct);
        string path = Path.Combine(harness.DataDir, "Uploads", room.Id, "a.txt");
        Assert.True(File.Exists(path));

        await pending.RemoveAsync(room.Id, chip.Id, ct);

        Assert.False(File.Exists(path));
        Assert.Empty(pending.For(room.Id));
    }

    /// <summary>Removing a Failed chip drops it without touching the disk.</summary>
    [Fact]
    public async Task RemoveAsync_FailedChip_DropsChip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        PendingUpload chip = await pending.AddAsync(room.Id, "logo.svg", _ => new MemoryStream("<svg></svg>"u8.ToArray()), ct);

        await pending.RemoveAsync(room.Id, chip.Id, ct);

        Assert.Empty(pending.For(room.Id));
    }

    /// <summary>Removing a chip that is still uploading cancels the upload, drops the chip and leaves no saved file or temp file.</summary>
    [Fact]
    public async Task RemoveAsync_WhileUploading_CancelsUploadAndLeavesNoFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        GatedStream gated = new();
        Task<PendingUpload> upload = pending.AddAsync(room.Id, "a.txt", _ => gated, ct);
        PendingUpload chip = Assert.Single(pending.For(room.Id));

        await pending.RemoveAsync(room.Id, chip.Id, ct);
        PendingUpload settled = await upload.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(UploadState.Failed, settled.State);
        Assert.Empty(pending.For(room.Id));
        string folder = Path.Combine(harness.DataDir, "Uploads", room.Id);
        Assert.Empty(Directory.Exists(folder) ? Directory.GetFiles(folder) : []);
    }

    /// <summary><see cref="PendingUploads.TakeAll"/> returns the completed chips in the order they were added and clears the Room's list, failed chips included.</summary>
    [Fact]
    public async Task TakeAll_ReturnsCompletedInOrderAndClears()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        _ = await pending.AddAsync(room.Id, "b.txt", _ => new MemoryStream("b"u8.ToArray()), ct);
        _ = await pending.AddAsync(room.Id, "logo.svg", _ => new MemoryStream("<svg></svg>"u8.ToArray()), ct);
        _ = await pending.AddAsync(room.Id, "a.txt", _ => new MemoryStream("a"u8.ToArray()), ct);

        IReadOnlyList<PendingUpload> taken = pending.TakeAll(room.Id, [.. pending.For(room.Id).Select(static c => c.Id)]);

        Assert.Equal(["b.txt", "a.txt"], taken.Select(static c => c.DisplayName));
        Assert.All(taken, static c => Assert.Equal(UploadState.Done, c.State));
        Assert.Empty(pending.For(room.Id));
        Assert.True(File.Exists(Path.Combine(harness.DataDir, "Uploads", room.Id, "b.txt")));
    }

    /// <summary><see cref="PendingUploads.TakeAll"/> takes only the named chips: one added after the snapshot stays listed and keeps its file.</summary>
    [Fact]
    public async Task TakeAll_OnlyTheNamedChips_LeavesALaterChip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        PendingUpload first = await pending.AddAsync(room.Id, "a.txt", _ => new MemoryStream("a"u8.ToArray()), ct);
        PendingUpload later = await pending.AddAsync(room.Id, "b.txt", _ => new MemoryStream("b"u8.ToArray()), ct);

        IReadOnlyList<PendingUpload> taken = pending.TakeAll(room.Id, [first.Id]);

        Assert.Equal("a.txt", Assert.Single(taken).DisplayName);
        Assert.Equal(later.Id, Assert.Single(pending.For(room.Id)).Id);
        Assert.True(File.Exists(Path.Combine(harness.DataDir, "Uploads", room.Id, "b.txt")));
    }

    /// <summary>A stream that cannot be opened (the browser refuses an oversized file) settles the chip as Failed with the save copy, not Uploading forever.</summary>
    [Fact]
    public async Task AddAsync_OpenStreamThrows_ChipIsFailedWithSaveCopy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);

        PendingUpload chip = await pending.AddAsync(room.Id, "big.bin", static _ => throw new IOException("too big"), ct);

        Assert.Equal(UploadState.Failed, chip.State);
        Assert.Equal("Couldn't save big.bin.", chip.Error);
        PendingUpload listed = Assert.Single(pending.For(room.Id));
        Assert.Equal(UploadState.Failed, listed.State);
        Assert.Equal("Couldn't save big.bin.", listed.Error);
    }

    /// <summary>Chips are kept per Room: one Room's chips, count and take-all never touch another's.</summary>
    [Fact]
    public async Task Rooms_AreSeparate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct, maxUploadsPerMessage: 1);
        Room first = await harness.Directory.CreateRoomAsync("One", [KnownIds.Human, harness.Nova.Id], ct);
        Room second = await harness.Directory.CreateRoomAsync("Two", [KnownIds.Human, harness.Nova.Id], ct);
        PendingUploads pending = new(harness.Service, harness.Options);
        _ = await pending.AddAsync(first.Id, "a.txt", _ => new MemoryStream("a"u8.ToArray()), ct);

        PendingUpload inSecond = await pending.AddAsync(second.Id, "b.txt", _ => new MemoryStream("b"u8.ToArray()), ct);

        Assert.Equal(UploadState.Done, inSecond.State);
        IReadOnlyList<PendingUpload> taken = pending.TakeAll(first.Id, [.. pending.For(first.Id).Select(static c => c.Id)]);
        Assert.Equal("a.txt", Assert.Single(taken).DisplayName);
        Assert.Equal("b.txt", Assert.Single(pending.For(second.Id)).DisplayName);
    }

    /// <summary>A Room with no chips has an empty list and an empty take-all.</summary>
    [Fact]
    public async Task For_UnknownRoom_IsEmpty()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        PendingUploads pending = new(harness.Service, harness.Options);

        Assert.Empty(pending.For("nothing"));
        Assert.Empty(pending.TakeAll("nothing", []));
    }
}

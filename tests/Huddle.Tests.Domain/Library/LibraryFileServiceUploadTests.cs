using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Fakes;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.CreateFromStreamAsync"/>, the chat-upload byte writer
/// (ChatUploads spec 6.2), and for <see cref="LibraryFileService.ListAsync"/> hiding its temp files.
/// </summary>
public sealed class LibraryFileServiceUploadTests
{
    private const long OneMegabyte = 1024 * 1024;

    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    /// <summary>The bytes of the stream land on disk unchanged, and the result names the file, its kind and its size.</summary>
    [Fact]
    public async Task CreateFromStream_Bytes_SavedExactly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        byte[] bytes = new byte[300_000];
        new Random(7).NextBytes(bytes);
        PngHead.CopyTo(bytes, 0);

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "photo.png", new MemoryStream(bytes), OneMegabyte, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(vault, "photo.png"), ct));
        Assert.Equal("photo.png", Path.GetFileName(result.Value.File.FullPath));
        Assert.Equal(UploadKind.Image, result.Value.Kind);
        Assert.Equal(bytes.Length, result.Value.Size);
    }

    /// <summary>A folder that does not exist yet is created, with every missing level.</summary>
    [Fact]
    public async Task CreateFromStream_MissingFolderChain_IsCreated()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, "a/b/c"), "n.txt", new MemoryStream("hi"u8.ToArray()), OneMegabyte, ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(Path.Combine(vault, "a", "b", "c", "n.txt")));
    }

    /// <summary>A name already taken gets <c>name (2).ext</c>, then <c>name (3).ext</c>, and the first file is untouched.</summary>
    [Fact]
    public async Task CreateFromStream_NameTaken_UsesNumberedNames()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath folder = fixture.Resolve(vault, string.Empty);

        LibraryResult<LibraryUpload> first = await service.CreateFromStreamAsync(folder, "a.txt", new MemoryStream("one"u8.ToArray()), OneMegabyte, ct);
        LibraryResult<LibraryUpload> second = await service.CreateFromStreamAsync(folder, "a.txt", new MemoryStream("two"u8.ToArray()), OneMegabyte, ct);
        LibraryResult<LibraryUpload> third = await service.CreateFromStreamAsync(folder, "a.txt", new MemoryStream("three"u8.ToArray()), OneMegabyte, ct);

        Assert.Equal("a.txt", Path.GetFileName(first.Value?.File.FullPath));
        Assert.Equal("a (2).txt", Path.GetFileName(second.Value?.File.FullPath));
        Assert.Equal("a (3).txt", Path.GetFileName(third.Value?.File.FullPath));
        Assert.Equal("one", await File.ReadAllTextAsync(Path.Combine(vault, "a.txt"), ct));
        Assert.Equal("two", await File.ReadAllTextAsync(Path.Combine(vault, "a (2).txt"), ct));
    }

    /// <summary>A seekable stream past the cap is refused with the cap in whole MB, and no temp file stays behind.</summary>
    [Fact]
    public async Task CreateFromStream_SeekableOverCap_RefusedWithoutTempFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        MemoryStream stream = new(new byte[OneMegabyte + 1]);

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "video.mp4", stream, OneMegabyte, ct);

        Assert.Null(result.Value);
        Assert.Equal("video.mp4 is larger than 1 MB.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(vault));
    }

    /// <summary>Deleting an unsent upload removes the file and drops the cached wikilink index, so the stale file is not listed.</summary>
    [Fact]
    public async Task DeleteUnsentUpload_ExistingFile_DeletesAndInvalidatesIndex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryResult<LibraryUpload> saved = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "a.txt", new MemoryStream("hi"u8.ToArray()), OneMegabyte, ct);
        Assert.NotNull(saved.Value);
        string rootId = saved.Value.File.Root.Id;
        Assert.Contains("a.txt", fixture.Index.FilesUnder(rootId, string.Empty));

        await service.DeleteUnsentUploadAsync(saved.Value.File, ct);

        Assert.False(File.Exists(Path.Combine(vault, "a.txt")));
        Assert.DoesNotContain("a.txt", fixture.Index.FilesUnder(rootId, string.Empty));
    }

    /// <summary>Deleting an unsent upload whose file is already gone does not throw.</summary>
    [Fact]
    public async Task DeleteUnsentUpload_MissingFile_DoesNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryResult<LibraryUpload> saved = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "a.txt", new MemoryStream("hi"u8.ToArray()), OneMegabyte, ct);
        Assert.NotNull(saved.Value);
        File.Delete(Path.Combine(vault, "a.txt"));

        await service.DeleteUnsentUploadAsync(saved.Value.File, ct);

        Assert.False(File.Exists(Path.Combine(vault, "a.txt")));
    }

    /// <summary>The refusal text for a file over the cap reads "{name} is larger than {cap} MB." in whole megabytes.</summary>
    [Fact]
    public void UploadTooLargeMessage_NameAndCap_ReadsLikeTheWriterRefusal()
    {
        Assert.Equal("video.mp4 is larger than 25 MB.", LibraryFileService.UploadTooLargeMessage("video.mp4", 25 * OneMegabyte));
    }

    /// <summary>A cap under one mebibyte is printed in kilobytes, never as "0 MB".</summary>
    [Theory]
    [InlineData(512 * 1024, "big.bin is larger than 512 KB.")]
    [InlineData(1024, "big.bin is larger than 1 KB.")]
    [InlineData(OneMegabyte, "big.bin is larger than 1 MB.")]
    public void UploadTooLargeMessage_SmallCap_PrintsKilobytes(long cap, string expected)
    {
        Assert.Equal(expected, LibraryFileService.UploadTooLargeMessage("big.bin", cap));
    }

    /// <summary>A non-seekable stream past the cap is refused while streaming, and no temp file stays behind.</summary>
    [Fact]
    public async Task CreateFromStream_NonSeekableOverCap_RefusedWithoutTempFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        NonSeekableStream stream = new(new byte[(3 * OneMegabyte) + 5]);

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "big.bin", stream, 2 * OneMegabyte, ct);

        Assert.Null(result.Value);
        Assert.Equal("big.bin is larger than 2 MB.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(vault));
    }

    /// <summary>A stream of exactly the cap is accepted.</summary>
    [Fact]
    public async Task CreateFromStream_ExactlyCap_Saved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "edge.bin", new NonSeekableStream(new byte[OneMegabyte]), OneMegabyte, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(OneMegabyte, result.Value.Size);
    }

    /// <summary>Cancelling while the bytes stream rethrows and leaves neither a temp file nor a target.</summary>
    [Fact]
    public async Task CreateFromStream_Cancelled_RethrowsAndLeavesNoTempFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CancelOnReadStream stream = new(cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "x.bin", stream, OneMegabyte, cts.Token));

        Assert.Empty(Directory.GetFileSystemEntries(vault));
    }

    /// <summary>An SVG, with or without a BOM and XML declaration, is refused with the SVG text and leaves nothing.</summary>
    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("﻿<?xml version=\"1.0\"?><!-- c --><svg/>")]
    public async Task CreateFromStream_Svg_RefusedWithoutTempFile(string text)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(
            fixture.Resolve(vault, string.Empty), "logo.png", new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), OneMegabyte, ct);

        Assert.Null(result.Value);
        Assert.Equal("SVG files can't be attached.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(vault));
    }

    /// <summary>Each program signature is refused with the program text and leaves nothing, whatever the extension says.</summary>
    [Theory]
    [InlineData("4D5A9000")]
    [InlineData("7F454C46")]
    [InlineData("FEEDFACE")]
    [InlineData("FEEDFACF")]
    [InlineData("CEFAEDFE")]
    [InlineData("CFFAEDFE")]
    [InlineData("CAFEBABE")]
    public async Task CreateFromStream_Executable_RefusedWithoutTempFile(string hex)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(
            fixture.Resolve(vault, string.Empty), "notes.txt", new MemoryStream(Convert.FromHexString(hex + "00000000")), OneMegabyte, ct);

        Assert.Null(result.Value);
        Assert.Equal("Programs can't be attached.", result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(vault));
    }

    /// <summary>The extension never decides the kind: a <c>.png</c> holding text is saved as text.</summary>
    [Fact]
    public async Task CreateFromStream_PngNamedText_SavedAsText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "fake.png", new MemoryStream("just words"u8.ToArray()), OneMegabyte, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(UploadKind.Text, result.Value.Kind);
        Assert.True(File.Exists(Path.Combine(vault, "fake.png")));
    }

    /// <summary>A name the Library refuses is refused before any byte is read, with the validator's text.</summary>
    [Fact]
    public async Task CreateFromStream_InvalidName_RefusedWithValidatorText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, string.Empty), "a:b.txt", new MemoryStream("x"u8.ToArray()), OneMegabyte, ct);

        Assert.Null(result.Value);
        Assert.Equal(LibraryNames.Validate("a:b.txt"), result.Error);
        Assert.Empty(Directory.GetFileSystemEntries(vault));
    }

    /// <summary>When the target folder is really a file the write fails with an IO error, which becomes the save refusal.</summary>
    [Fact]
    public async Task CreateFromStream_FolderIsAFile_RefusedCouldNotSave()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        await File.WriteAllTextAsync(Path.Combine(vault, "blocker"), "x", ct);
        LibraryFileService service = fixture.CreateService();

        LibraryResult<LibraryUpload> result = await service.CreateFromStreamAsync(fixture.Resolve(vault, "blocker"), "n.txt", new MemoryStream("x"u8.ToArray()), OneMegabyte, ct);

        Assert.Null(result.Value);
        Assert.Equal("Couldn't save n.txt.", result.Error);
    }

    /// <summary>A dot-file ending in <c>.upload</c> (an unfinished upload) is never listed; ordinary files still are.</summary>
    [Fact]
    public async Task List_UploadTempFile_IsSkipped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        await File.WriteAllTextAsync(Path.Combine(vault, ".0123abcd.upload"), "partial", ct);
        await File.WriteAllTextAsync(Path.Combine(vault, "kept.upload"), "x", ct);
        await File.WriteAllTextAsync(Path.Combine(vault, "visible.txt"), "x", ct);
        LibraryFileService service = fixture.CreateService();

        IReadOnlyList<LibraryEntry> entries = await service.ListAsync(fixture.Resolve(vault, string.Empty), ct);

        Assert.Equal(["kept.upload", "visible.txt"], entries.Select(static e => Path.GetFileName(e.Path.FullPath)).Order(StringComparer.Ordinal));
    }

    /// <summary>A readable stream that reports it cannot seek and has no length.</summary>
    private sealed class NonSeekableStream(byte[] data) : ScriptedReadStream(canSeek: false)
    {
        private readonly MemoryStream inner = new(data);

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.inner.ReadAsync(buffer, cancellationToken);
    }

    /// <summary>A stream whose first read hands over some bytes and cancels the source, so the copy is cancelled mid-flight.</summary>
    private sealed class CancelOnReadStream(CancellationTokenSource source) : ScriptedReadStream(canSeek: false)
    {
        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            source.Cancel();
            buffer.Span[..100].Clear();
            return ValueTask.FromResult(100);
        }
    }
}

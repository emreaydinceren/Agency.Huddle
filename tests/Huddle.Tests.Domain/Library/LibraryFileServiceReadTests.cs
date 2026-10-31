using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.ReadAsync"/> (Spec §6.4 <c>ReadTextAsync</c>, §6.11,
/// §10 E-3 and E-5): decoding, editability by kind and size, and a missing file.
/// </summary>
public sealed class LibraryFileServiceReadTests
{
    /// <summary>A Markdown file under the size limit decodes and is editable.</summary>
    [Fact]
    public async Task Read_Markdown_IsEditableWithFormat()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello world");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Equal(LibraryFileKind.Markdown, content.Kind);
        Assert.Equal("hello world", content.Text);
        Assert.NotNull(content.Format);
        Assert.True(content.Editable);
        Assert.Null(content.ViewOnlyReason);
    }

    /// <summary>A JSON file is a text kind, decoded for viewing, but not editable and carries no reason (view-only by kind).</summary>
    [Fact]
    public async Task Read_Json_IsTextViewOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "data.json");
        File.WriteAllText(filePath, "{}");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "data.json");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Equal(LibraryFileKind.Text, content.Kind);
        Assert.Equal("{}", content.Text);
        Assert.False(content.Editable);
        Assert.Null(content.ViewOnlyReason);
    }

    /// <summary>Above <see cref="LibraryOptions.MaxEditableBytes"/>, a text file is view-only with the settled too-large reason, and Text is null.</summary>
    [Fact]
    public async Task Read_OverMaxEditableBytes_IsViewOnlyWithReason()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 10);
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "this text is longer than ten bytes");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Null(content.Text);
        Assert.Null(content.Format);
        Assert.False(content.Editable);
        Assert.Equal("This file is too large to edit here.", content.ViewOnlyReason);
    }

    /// <summary>Bytes that fail to decode as text are view-only with the unsupported-format reason, and Text is null.</summary>
    [Fact]
    public async Task Read_InvalidUtf8_IsViewOnlyUnsupported()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");

        // A UTF-8 BOM (which LibraryFileKinds sniffs as text without validating the rest) followed
        // by an invalid UTF-8 continuation byte (0xC3 must be followed by a byte in 0x80-0xBF).
        byte[] invalidUtf8 = [0xEF, 0xBB, 0xBF, 0x68, 0x65, 0xC3, 0x28];
        File.WriteAllBytes(filePath, invalidUtf8);
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Null(content.Text);
        Assert.False(content.Editable);
        Assert.Equal("Unsupported text format", content.ViewOnlyReason);
    }

    /// <summary>An image file has no text and no reason: not offered as text at all.</summary>
    [Fact]
    public async Task Read_Image_HasNoText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "picture.png");
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        File.WriteAllBytes(filePath, png);
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "picture.png");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Equal(LibraryFileKind.Image, content.Kind);
        Assert.Null(content.Text);
        Assert.Null(content.Format);
        Assert.False(content.Editable);
        Assert.Null(content.ViewOnlyReason);
    }

    /// <summary>An SVG file is offered as text, but read-only (Spec §6.11), with no reason.</summary>
    [Fact]
    public async Task Read_Svg_IsTextViewOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "icon.svg");
        File.WriteAllText(filePath, "<svg></svg>");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "icon.svg");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Equal(LibraryFileKind.Svg, content.Kind);
        Assert.Equal("<svg></svg>", content.Text);
        Assert.False(content.Editable);
        Assert.Null(content.ViewOnlyReason);
    }

    /// <summary>A file that is neither text, image nor SVG has no text and no reason.</summary>
    [Fact]
    public async Task Read_Other_HasNoText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "archive.zip");
        byte[] bytes = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x01, 0x02, 0x03];
        File.WriteAllBytes(filePath, bytes);
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "archive.zip");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);

        Assert.Equal(LibraryFileKind.Other, content.Kind);
        Assert.Null(content.Text);
        Assert.Null(content.Format);
        Assert.False(content.Editable);
        Assert.Null(content.ViewOnlyReason);
    }

    /// <summary>A file that no longer exists on disk throws <see cref="FileNotFoundException"/> (Spec §10 E-3), for the UI to map to its banner.</summary>
    [Fact]
    public async Task Read_Missing_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "ghost.md");

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ReadAsync(path, ct));
    }

    /// <summary>An existing file's <see cref="LibraryFileService.StatAsync"/> returns its current entry (corrections-B6 item 11).</summary>
    [Fact]
    public async Task Stat_ExistingFile_ReturnsEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello world");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");

        LibraryEntry? entry = await service.StatAsync(path, ct);

        Assert.NotNull(entry);
        Assert.Equal("hello world".Length, entry.Length);
    }

    /// <summary>A missing file's <see cref="LibraryFileService.StatAsync"/> returns <see langword="null"/> rather than throwing (corrections-B6 item 11).</summary>
    [Fact]
    public async Task Stat_MissingFile_ReturnsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "ghost.md");

        LibraryEntry? entry = await service.StatAsync(path, ct);

        Assert.Null(entry);
    }
}

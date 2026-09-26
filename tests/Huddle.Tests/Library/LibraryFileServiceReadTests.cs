using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
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
        using Fixture fixture = Fixture.Build();
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
        using Fixture fixture = Fixture.Build();
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
        using Fixture fixture = Fixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 10);
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
        using Fixture fixture = Fixture.Build();
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
        using Fixture fixture = Fixture.Build();
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
        using Fixture fixture = Fixture.Build();
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
        using Fixture fixture = Fixture.Build();
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
        using Fixture fixture = Fixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "ghost.md");

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ReadAsync(path, ct));
    }

    /// <summary>A minimal <see cref="IRecycleBin"/> fake: this task never calls it, only satisfies the constructor.</summary>
    private sealed class RecordingRecycleBin : IRecycleBin
    {
        /// <summary>Every path passed to <see cref="TrySend"/> so far, in call order.</summary>
        public List<string> Sent { get; } = [];

        /// <summary>Records <paramref name="fullPath"/> and always succeeds.</summary>
        /// <param name="fullPath">The path this fake records.</param>
        /// <param name="error">Always <see langword="null"/>.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool TrySend(string fullPath, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
        {
            this.Sent.Add(fullPath);
            error = null;
            return true;
        }
    }

    /// <summary>An isolated fixture: a real <see cref="LibraryRootStore"/> and <see cref="LibraryPathResolver"/> over a temp <c>DataDir</c>, plus the <see cref="RecordingRecycleBin"/> fake.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly TempDataDir temp;
        private readonly TeamOptions teamOptions;

        private Fixture(TempDataDir temp, TeamOptions teamOptions)
        {
            this.temp = temp;
            this.teamOptions = teamOptions;
            this.DataDir = teamOptions.DataDir;
            TeammatePaths paths = new(Options.Create(teamOptions));
            this.RootStore = new LibraryRootStore(Options.Create(teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(teamOptions), NullLogger<LibraryPathResolver>.Instance);
        }

        public string DataDir { get; }

        public LibraryRootStore RootStore { get; private set; }

        public LibraryPathResolver Resolver { get; private set; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout, optionally customising <see cref="TeamOptions"/> first.</summary>
        public static Fixture Build(Action<TeamOptions>? configure = null)
        {
            TempDataDir temp = new();
            Directory.CreateDirectory(Path.Combine(temp.Path, "Teams"));
            Directory.CreateDirectory(Path.Combine(temp.Path, "Teammates"));

            TeamOptions teamOptions = new() { DataDir = temp.Path };
            configure?.Invoke(teamOptions);

            return new Fixture(temp, teamOptions);
        }

        /// <summary>Creates a pinned root folder named <paramref name="name"/> under the temp root and reloads the store and resolver.</summary>
        public string CreatePinnedRoot(string name)
        {
            string path = Path.Combine(this.temp.Path, name);
            Directory.CreateDirectory(path);

            List<PinnedRootOption> roots = [.. this.teamOptions.Library.Roots ?? [], new PinnedRootOption { Name = name, Path = path }];
            this.teamOptions.Library.Roots = roots;
            this.Reload();
            return path;
        }

        /// <summary>Constructs the service under test, wired to this fixture's resolver and a fresh <see cref="RecordingRecycleBin"/>.</summary>
        public LibraryFileService CreateService() => new(this.Resolver, new RecordingRecycleBin(), Options.Create(this.teamOptions), NullLogger<LibraryFileService>.Instance);

        /// <summary>Resolves <paramref name="relativePath"/> inside the pinned root at <paramref name="rootPath"/>.</summary>
        public LibraryPath Resolve(string rootPath, string relativePath)
        {
            foreach (LibraryRoot candidate in this.RootStore.Roots)
            {
                if (string.Equals(candidate.FullPath, rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(this.Resolver.TryResolve(candidate.Id, relativePath, out LibraryPath? resolved, out _));
                    Assert.NotNull(resolved);
                    return resolved;
                }
            }

            throw new InvalidOperationException($"No pinned root at '{rootPath}'.");
        }

        private void Reload()
        {
            TeammatePaths paths = new(Options.Create(this.teamOptions));
            this.RootStore = new LibraryRootStore(Options.Create(this.teamOptions), paths, NullLogger<LibraryRootStore>.Instance);
            this.Resolver = new LibraryPathResolver(this.RootStore, Options.Create(this.teamOptions), NullLogger<LibraryPathResolver>.Instance);
        }

        public void Dispose() => this.temp.Dispose();
    }
}

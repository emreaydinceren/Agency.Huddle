using System.Reflection;
using System.Text;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <see cref="LibraryFileService.WriteTextAsync"/> (Spec §6.4 <c>WriteTextAsync</c>,
/// ADR-0028 byte-exact round trips, §6.8 freshness, §6.12 Teammates-page-only rename), and
/// corrections-B4 items 12-18.
/// </summary>
public sealed class LibraryFileServiceWriteTests
{
    /// <summary>Byte-exact round trips for the recorded encodings and BOMs (ADR-0028).</summary>
    public static TheoryData<byte[]> RoundTripEncodings
    {
        get
        {
            byte[] utf8BomCrLf = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("hello\r\nworld")];
            byte[] utf8Lf = Encoding.UTF8.GetBytes("hello\nworld");
            byte[] utf8BomLf = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("hello\nworld")];
            byte[] utf16LeBomCrLf = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("hello\r\nworld")];
            return new TheoryData<byte[]>
            {
                utf8BomCrLf,
                utf8Lf,
                utf8BomLf,
                utf16LeBomCrLf,
            };
        }
    }

    /// <summary>Reading then writing the same text back produces byte-identical content, for UTF-8 BOM CRLF,
    /// UTF-8 LF, UTF-8 BOM LF and UTF-16 LE BOM CRLF.</summary>
    [Theory]
    [MemberData(nameof(RoundTripEncodings))]
    public async Task Write_Unchanged_IsByteIdentical(byte[] originalBytes)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllBytes(filePath, originalBytes);
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");

        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Text);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, content.Text, content.Format, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(originalBytes, File.ReadAllBytes(filePath));
    }

    /// <summary>After a write, no <c>*.tmp</c> file is left behind in the folder.</summary>
    [Fact]
    public async Task Write_Atomic_LeavesNoTempFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "updated", content.Format, ct);

        Assert.NotNull(result.Value);
        Assert.DoesNotContain(Directory.EnumerateFiles(vault), f => f.EndsWith(".tmp", StringComparison.Ordinal));
    }

    /// <summary>A file that is not editable (view-only by kind) is refused at write time with the settled text.</summary>
    [Fact]
    public async Task Write_ViewOnlyDocument_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "data.json");
        File.WriteAllText(filePath, "{}");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "data.json");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "{}", content.Format, ct);

        Assert.Null(result.Value);
        Assert.Equal("This file can't be edited here.", result.Error);
        Assert.Equal("{}"u8.ToArray(), File.ReadAllBytes(filePath));
    }

    /// <summary>A <c>.md</c> read as editable, then overwritten on disk with bytes containing NUL before the
    /// write, is refused at write time by the re-check (corrections-B4 item 13).</summary>
    [Fact]
    public async Task Write_MarkdownGrewNulBytesOnDisk_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.True(content.Editable);
        byte[] nulBytes = [0x68, 0x00, 0x69];
        File.WriteAllBytes(filePath, nulBytes);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "hello", content.Format, ct);

        Assert.Null(result.Value);
        Assert.Equal("This file can't be edited here.", result.Error);
        Assert.Equal(nulBytes, File.ReadAllBytes(filePath));
    }

    /// <summary>A <c>.md</c> read as editable, then grown past <see cref="LibraryOptions.MaxEditableBytes"/> on
    /// disk before the write, is refused at write time by the re-check (corrections-B4 item 13).</summary>
    [Fact]
    public async Task Write_MarkdownGrewPastCap_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 16);
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "short");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.True(content.Editable);
        byte[] grownBytes = Encoding.UTF8.GetBytes("this text is now longer than the sixteen byte cap");
        File.WriteAllBytes(filePath, grownBytes);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "short", content.Format, ct);

        Assert.Null(result.Value);
        Assert.Equal("This file can't be edited here.", result.Error);
        Assert.Equal(grownBytes, File.ReadAllBytes(filePath));
    }

    /// <summary>An unknown <see cref="TextFileFormat.EncodingName"/> is refused, not thrown (corrections-B4 item 13).</summary>
    [Fact]
    public async Task Write_UnknownEncoding_RefusedNotThrown()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");
        TextFileFormat badFormat = new("x-no-such-encoding", false, LineEnding.Lf, false);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "hello", badFormat, ct);

        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal("hello"u8.ToArray(), File.ReadAllBytes(filePath));
    }

    /// <summary>Every public/internal declared-only method's first parameter is a <see cref="LibraryPath"/>,
    /// and no <see cref="string"/> parameter's name ends in "Path" (corrections-B4 item 16).</summary>
    [Fact]
    public void Write_OnlyLibraryPaths_EveryMethodTakesLibraryPathFirst()
    {
        MethodInfo[] methods = [.. typeof(LibraryFileService)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !(m.Name == nameof(IDisposable.Dispose) && m.GetParameters().Length == 0))];

        Assert.NotEmpty(methods);

        foreach (MethodInfo method in methods)
        {
            ParameterInfo[] parameters = method.GetParameters();
            Assert.True(
                parameters.Length > 0 && parameters[0].ParameterType == typeof(LibraryPath),
                $"{method.Name}'s first parameter must be a LibraryPath.");

            foreach (ParameterInfo parameter in parameters)
            {
                bool isStringPath = parameter.ParameterType == typeof(string)
                    && parameter.Name is not null
                    && parameter.Name.EndsWith("Path", StringComparison.Ordinal);
                Assert.False(isStringPath, $"{method.Name}'s parameter '{parameter.Name}' must not be a string path.");
            }
        }
    }

    /// <summary>Writing to a missing project folder creates it lazily (Spec §6.2).</summary>
    [Fact]
    public async Task Write_MissingProjectFolder_CreatesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        string marketing = Path.Combine(teamsRoot, "Marketing");
        Directory.CreateDirectory(marketing);
        fixture.Reload();
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsRootPath = fixture.ResolveTeams(string.Empty);
        Assert.True(fixture.Resolver.TryResolve(teamsRootPath.Root.Id, "Marketing/Launch Q4/new.md", out LibraryPath? target, out _));
        Assert.NotNull(target);
        TextFileFormat format = new("utf-8", false, LineEnding.Lf, false);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(target, "content", format, ct);

        Assert.NotNull(result.Value);
        string createdFolder = Path.Combine(marketing, "Launch Q4");
        Assert.True(Directory.Exists(createdFolder));
        Assert.Equal("content", File.ReadAllText(Path.Combine(createdFolder, "new.md")));
    }

    /// <summary>A missing parent that is not a Team or Project folder (a pinned root's own missing subfolder)
    /// is not created lazily: the write throws, and the subfolder stays absent (Plan 6.5.i; corrections-B4 item 17).</summary>
    [Fact]
    public async Task Write_MissingParent_NotTeamOrProjectFolder_Throws()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryFileService service = fixture.CreateService();
        LibraryPath vaultRoot = fixture.Resolve(vault, string.Empty);
        Assert.True(fixture.Resolver.TryResolve(vaultRoot.Root.Id, "a/b/new.md", out LibraryPath? target, out _));
        Assert.NotNull(target);
        TextFileFormat format = new("utf-8", false, LineEnding.Lf, false);

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.WriteTextAsync(target, "content", format, ct));

        Assert.False(Directory.Exists(Path.Combine(vault, "a")));
    }

    /// <summary>A missing Team folder is created lazily, its role resolved from the parent (Spec §6.2;
    /// corrections-B4 item 17).</summary>
    [Fact]
    public async Task Write_MissingTeamFolder_CreatesIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string teamsRoot = Path.Combine(fixture.DataDir, "Teams");
        LibraryFileService service = fixture.CreateService();
        LibraryPath teamsRootPath = fixture.ResolveTeams(string.Empty);
        Assert.True(fixture.Resolver.TryResolve(teamsRootPath.Root.Id, "NewTeam/new.md", out LibraryPath? target, out _));
        Assert.NotNull(target);
        TextFileFormat format = new("utf-8", false, LineEnding.Lf, false);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(target, "content", format, ct);

        Assert.NotNull(result.Value);
        string createdFolder = Path.Combine(teamsRoot, "NewTeam");
        Assert.True(Directory.Exists(createdFolder));
        Assert.Equal("content", File.ReadAllText(Path.Combine(createdFolder, "new.md")));
    }

    /// <summary>A hand-built <see cref="LibraryPath"/> whose relative path escapes its root is refused, since
    /// every operation re-resolves rather than trusting the caller's <c>FullPath</c> (corrections-B4 item 12).</summary>
    [Fact]
    public async Task Write_TamperedPath_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath valid = fixture.Resolve(vault, "note.md");
        string outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.md");
        LibraryPath tampered = new(valid.Root, "../outside.md", outsidePath, valid.Role);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(tampered, "hello", new TextFileFormat("utf-8", false, LineEnding.Lf, false), ct);

        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.False(File.Exists(outsidePath));
    }

    /// <summary>A hand-built <see cref="LibraryPath"/> with a VALID relative path but an outside <c>FullPath</c>
    /// is re-resolved and saved through the resolver's own path, never the caller's (corrections-B4 item 12).</summary>
    [Fact]
    public async Task Write_TamperedFullPath_ValidRelative_UsesReResolvedFullPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        LibraryFileService service = fixture.CreateService();
        LibraryPath valid = fixture.Resolve(vault, "note.md");
        string outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.md");
        LibraryPath tampered = new(valid.Root, "note.md", outsidePath, valid.Role);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(tampered, "new text", new TextFileFormat("utf-8", false, LineEnding.Lf, false), ct);

        Assert.NotNull(result.Value);
        Assert.False(File.Exists(outsidePath));
        Assert.Equal("new text", File.ReadAllText(Path.Combine(vault, "note.md")));
    }

    /// <summary>The result carries the fresh <see cref="LibraryEntry.Length"/> and <see cref="LibraryEntry.LastWriteUtc"/>
    /// so the save never reads back as an outside change (Spec §6.8, corrections-B4 item 14).</summary>
    [Fact]
    public async Task Write_Result_CarriesFreshLengthAndLastWriteUtc()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hi");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");
        TextFileFormat format = new("utf-8", false, LineEnding.Lf, false);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "hello world", format, ct);

        Assert.NotNull(result.Value);
        Assert.Equal("hello world"u8.Length, result.Value.Length);
        DateTimeOffset onDisk = new(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
        Assert.Equal(onDisk, result.Value.LastWriteUtc);
    }

    /// <summary>A target held open without <see cref="FileShare.Delete"/> fails the atomic move, giving the
    /// settled save-failure text and leaving no temp file behind (corrections-B4 item 15).</summary>
    [Fact]
    public async Task Write_TargetOpenWithoutFileShareDelete_Refused()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("FileShare.Delete semantics differ off Windows.");
            return;
        }

        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string filePath = Path.Combine(vault, "note.md");
        File.WriteAllText(filePath, "hi");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.Resolve(vault, "note.md");
        TextFileFormat format = new("utf-8", false, LineEnding.Lf, false);

        using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "hello world", format, ct);

            Assert.Null(result.Value);
            Assert.Equal("Couldn't save note.md: it's in use or read-only.", result.Error);
        }

        Assert.DoesNotContain(Directory.EnumerateFiles(vault), f => f.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.Equal("hi", File.ReadAllText(filePath));
    }

    /// <summary>Saving a Teammate definition with a changed frontmatter Name is refused (Spec §6.12; corrections-B4 item 18).</summary>
    [Fact]
    public async Task Write_TeammateDefinitionNameChanged_Refused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string originalText = fixture.CreateTeammate("ada", "Ada", "ada");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.ResolveTeammates("ada");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);
        string changedText = "---\nName: Beatrice\nTitle: Chief of Staff\nAlias: ada\n_builtin: chief-of-staff\n---\nbody";

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, changedText, content.Format, ct);

        Assert.Null(result.Value);
        Assert.Equal("Rename teammates on the Teammates page.", result.Error);
        Assert.Equal(originalText, File.ReadAllText(path.FullPath));
    }

    /// <summary>Saving a Teammate definition with the frontmatter Name unchanged succeeds (Spec §6.12; corrections-B4 item 18).</summary>
    [Fact]
    public async Task Write_TeammateDefinitionNameUnchanged_Succeeds()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreateTeammate("ada", "Ada", "ada");
        LibraryFileService service = fixture.CreateService();
        LibraryPath path = fixture.ResolveTeammates("ada");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);
        string sameNameText = "---\nName: Ada\nTitle: Chief of Staff Updated\nAlias: ada\n_builtin: chief-of-staff\n---\nbody";

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, sameNameText, content.Format, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(sameNameText, File.ReadAllText(path.FullPath));
    }

}

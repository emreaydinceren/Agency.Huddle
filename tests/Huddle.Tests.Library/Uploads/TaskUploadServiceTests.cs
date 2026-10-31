using Agency.Huddle.App;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>
/// <see cref="TaskUploadFolders"/> and <see cref="TaskUploadService"/> over a temp <c>DataDir</c>
/// (TaskAttachments spec 6.1 and 6.2): the folder a Task's files live in, and the refusals.
/// </summary>
public sealed class TaskUploadServiceTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static (TaskUploadService Service, TaskUploadFolders Folders, LibraryFileServiceFixture Fixture) Build(long? maxUploadBytes = null)
    {
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        TeamOptions teamOptions = new() { DataDir = fixture.DataDir };
        if (maxUploadBytes is long bytes)
        {
            teamOptions.Library.MaxUploadBytes = bytes;
        }

        FakeTaskReferenceResolver tasks = new();
        tasks.Add(new TaskReference(new TaskId("GROW", 2), "Write the plan", Closed: false));
        TaskUploadFolders folders = new(fixture.Resolver);
        TaskUploadService service = new(tasks, folders, fixture.CreateService(), Options.Create(teamOptions));
        return (service, folders, fixture);
    }

    /// <summary>A Task's folder is <c>uploads:/Tasks/&lt;ID&gt;</c> under the Uploads root.</summary>
    [Fact]
    public void For_Task_ResolvesUnderUploadsTasks()
    {
        (_, TaskUploadFolders folders, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;

        LibraryPath? folder = folders.For("GROW-0002");

        Assert.NotNull(folder);
        Assert.Equal("uploads", folder.Root.Id);
        Assert.Equal(Path.Combine(fixture.DataDir, "Uploads", "Tasks", "GROW-0002"), folder.FullPath);
    }

    /// <summary>An id that is not a safe path segment resolves to nothing.</summary>
    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    public void For_UnsafeId_IsNull(string id)
    {
        (_, TaskUploadFolders folders, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;

        Assert.Null(folders.For(id));
    }

    /// <summary>A file is saved into the Task's folder, with its kind and size.</summary>
    [Fact]
    public async Task SaveAsync_KnownTask_SavesUnderTaskFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, _, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;

        LibraryResult<LibraryUpload> result = await service.SaveAsync("GROW-0002", "brief.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(result.Value);
        string expected = Path.Combine(fixture.DataDir, "Uploads", "Tasks", "GROW-0002", "brief.png");
        Assert.Equal(expected, result.Value.File.FullPath);
        Assert.Equal(UploadKind.Image, result.Value.Kind);
        Assert.True(File.Exists(expected));
    }

    /// <summary>The same file attached twice gets a free name the second time.</summary>
    [Fact]
    public async Task SaveAsync_SameNameTwice_SecondGetsNumberedName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, _, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;
        _ = await service.SaveAsync("GROW-0002", "brief.png", new MemoryStream(PngHead), ct);

        LibraryResult<LibraryUpload> second = await service.SaveAsync("GROW-0002", "brief.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(second.Value);
        Assert.Equal("brief (2).png", Path.GetFileName(second.Value.File.FullPath));
    }

    /// <summary>A Task that does not exist is refused and no folder is created.</summary>
    [Fact]
    public async Task SaveAsync_UnknownTask_IsRefusedAndCreatesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, _, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;

        LibraryResult<LibraryUpload> result = await service.SaveAsync("GROW-0099", "brief.png", new MemoryStream(PngHead), ct);

        Assert.Null(result.Value);
        Assert.Equal("Couldn't attach brief.png: this Task no longer exists.", result.Error);
        Assert.False(Directory.Exists(Path.Combine(fixture.DataDir, "Uploads", "Tasks")));
    }

    /// <summary>The writer's own refusals pass through: an SVG and a file over the cap are not saved.</summary>
    [Fact]
    public async Task SaveAsync_SvgAndOversize_AreRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, _, LibraryFileServiceFixture fixture) = Build(maxUploadBytes: 8);
        using LibraryFileServiceFixture lifetime = fixture;

        LibraryResult<LibraryUpload> svg = await service.SaveAsync("GROW-0002", "logo.svg", new MemoryStream("<svg/>"u8.ToArray()), ct);
        LibraryResult<LibraryUpload> big = await service.SaveAsync("GROW-0002", "big.txt", new MemoryStream(PngHead.Concat(PngHead).ToArray()), ct);

        Assert.Equal("SVG files can't be attached.", svg.Error);
        Assert.Equal(LibraryFileService.UploadTooLargeMessage("big.txt", 8), big.Error);
        Assert.False(File.Exists(Path.Combine(fixture.DataDir, "Uploads", "Tasks", "GROW-0002", "big.txt")));
    }

    /// <summary>A program is refused by its magic bytes, whatever its name, with the chat upload's text, and nothing is saved.</summary>
    [Fact]
    public async Task SaveAsync_Executable_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, _, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;
        byte[] program = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        LibraryResult<LibraryUpload> result = await service.SaveAsync("GROW-0002", "setup.txt", new MemoryStream(program), ct);

        Assert.Equal("Programs can't be attached.", result.Error);
        Assert.False(File.Exists(Path.Combine(fixture.DataDir, "Uploads", "Tasks", "GROW-0002", "setup.txt")));
    }

    /// <summary>Deleting an unsent upload removes the file.</summary>
    [Fact]
    public async Task DeleteUnsentAsync_RemovesTheFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, _, LibraryFileServiceFixture fixture) = Build();
        using LibraryFileServiceFixture lifetime = fixture;
        LibraryResult<LibraryUpload> saved = await service.SaveAsync("GROW-0002", "brief.png", new MemoryStream(PngHead), ct);
        Assert.NotNull(saved.Value);

        await service.DeleteUnsentAsync(saved.Value.File, ct);

        Assert.False(File.Exists(saved.Value.File.FullPath));
    }
}

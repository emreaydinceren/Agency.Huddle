using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>
/// Every chat upload and Task attachment records a <see cref="RawBaseline"/> for the saved file
/// (Second Brain spec 6.8 and 7.6); a refused save records nothing.
/// </summary>
public sealed class RawBaselineOnUploadTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private static string RelativeToDataDir(string dataDir, string fullPath) =>
        Path.GetRelativePath(Path.GetFullPath(dataDir), fullPath).Replace('\\', '/');

    private static (TaskUploadService Service, RawBaselineStore Baselines, LibraryFileServiceFixture Fixture) BuildTaskService(long? maxUploadBytes = null)
    {
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        TeamOptions teamOptions = new() { DataDir = fixture.DataDir };
        if (maxUploadBytes is long bytes)
        {
            teamOptions.Library.MaxUploadBytes = bytes;
        }

        FakeTaskReferenceResolver tasks = new();
        tasks.Add(new TaskReference(new TaskId("HOME", 1), "Tidy the notes", Closed: false));
        IOptions<TeamOptions> options = Options.Create(teamOptions);
        _ = TestTaskStore.WriteTask(
            Path.Combine(fixture.DataDir, "Teams"),
            TestTaskStore.RelativePath("Home", null, false, "HOME-0001.md"),
            TestTasks.Make(id: "HOME-0001", location: new("Home", null, false)));
        PersonaStore personas = options.CreatePersonaStore();
        TaskStore store = new(options, personas, TimeProvider.System, NullLogger<TaskStore>.Instance);
        TaskUploadFolders folders = new(fixture.Resolver, store);
        RawBaselineStore baselines = new(options);
        TaskUploadService service = new(tasks, folders, fixture.CreateService(), options, baselines);
        return (service, baselines, fixture);
    }

    /// <summary>A saved chat upload has a baseline holding its size and last-write time, under a <c>/</c> path relative to DataDir.</summary>
    [Fact]
    public async Task SaveAsync_ChatUpload_RecordsSizeAndModifiedTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "photo.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(result.Value);
        string relative = RelativeToDataDir(harness.DataDir, result.Value.File.FullPath);
        Assert.Equal($"Raw/{room.Id}/photo.png", relative);
        Assert.True(harness.Baselines.TryGet(relative, out RawBaseline? baseline));
        Assert.Equal(PngHead.Length, baseline.SizeBytes);
        Assert.Equal(File.GetLastWriteTimeUtc(result.Value.File.FullPath), baseline.ModifiedUtc.UtcDateTime);
    }

    /// <summary>A refused chat upload (a program) records no baseline for any path.</summary>
    [Fact]
    public async Task SaveAsync_RefusedChatUpload_RecordsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);
        byte[] program = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "setup.txt", new MemoryStream(program), ct);

        Assert.Null(result.Value);
        Assert.False(harness.Baselines.TryGet($"Raw/{room.Id}/setup.txt", out _));
    }

    /// <summary>An oversize chat upload records no baseline.</summary>
    [Fact]
    public async Task SaveAsync_OversizeChatUpload_RecordsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using UploadServiceHarness harness = await UploadServiceHarness.BuildAsync(ct, maxUploadBytes: 8);
        Room room = await harness.Directory.CreateRoomAsync("Nova", [KnownIds.Human, harness.Nova.Id], ct);

        LibraryResult<LibraryUpload> result = await harness.Service.SaveAsync(room.Id, "big.txt", new MemoryStream(PngHead.Concat(PngHead).ToArray()), ct);

        Assert.Null(result.Value);
        Assert.False(harness.Baselines.TryGet($"Raw/{room.Id}/big.txt", out _));
    }

    /// <summary>A Task attachment in Team Home has a baseline under <c>Teams/Home/raw/Tasks/&lt;ID&gt;</c>.</summary>
    [Fact]
    public async Task SaveAsync_TaskAttachment_RecordsSizeAndModifiedTime()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, RawBaselineStore baselines, LibraryFileServiceFixture fixture) = BuildTaskService();
        using LibraryFileServiceFixture lifetime = fixture;

        LibraryResult<LibraryUpload> result = await service.SaveAsync("HOME-0001", "brief.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(result.Value);
        string relative = RelativeToDataDir(fixture.DataDir, result.Value.File.FullPath);
        Assert.Equal("Teams/Home/raw/Tasks/HOME-0001/brief.png", relative);
        Assert.True(baselines.TryGet(relative, out RawBaseline? baseline));
        Assert.Equal(PngHead.Length, baseline.SizeBytes);
        Assert.Equal(File.GetLastWriteTimeUtc(result.Value.File.FullPath), baseline.ModifiedUtc.UtcDateTime);
    }

    /// <summary>A refused Task attachment (a program) records no baseline.</summary>
    [Fact]
    public async Task SaveAsync_RefusedTaskAttachment_RecordsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (TaskUploadService service, RawBaselineStore baselines, LibraryFileServiceFixture fixture) = BuildTaskService();
        using LibraryFileServiceFixture lifetime = fixture;
        byte[] program = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        LibraryResult<LibraryUpload> result = await service.SaveAsync("HOME-0001", "setup.txt", new MemoryStream(program), ct);

        Assert.Null(result.Value);
        Assert.False(baselines.TryGet("Teams/Home/raw/Tasks/HOME-0001/setup.txt", out _));
    }
}

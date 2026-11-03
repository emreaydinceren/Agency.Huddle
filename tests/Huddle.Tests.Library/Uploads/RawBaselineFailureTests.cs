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
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>A baseline that cannot be recorded is logged and never fails the upload (plan 3.3.i).</summary>
public sealed class RawBaselineFailureTests
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    /// <summary>When <c>team.db</c> is replaced by a directory after the store opened it, the save still succeeds.</summary>
    [Fact]
    public async Task SaveAsync_BaselineStoreFails_StillReturnsTheUpload()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        TeamOptions teamOptions = new() { DataDir = fixture.DataDir };
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
        SqliteConnection.ClearAllPools();
        string db = Path.Combine(fixture.DataDir, "team.db");
        File.Delete(db);
        _ = Directory.CreateDirectory(db);

        LibraryResult<LibraryUpload> result = await service.SaveAsync("HOME-0001", "photo.png", new MemoryStream(PngHead), ct);

        Assert.NotNull(result.Value);
        Assert.True(File.Exists(result.Value.File.FullPath));
    }
}

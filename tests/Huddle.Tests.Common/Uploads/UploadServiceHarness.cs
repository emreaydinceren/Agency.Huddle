using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Library;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>
/// A real <see cref="ChatUploadService"/> over a temp <c>DataDir</c>: a SQLite directory, the Library
/// resolver and the byte writer. Shared by the upload service and pending-uploads tests.
/// </summary>
internal sealed class UploadServiceHarness : IDisposable
{
    private readonly TempDataDir dir;
    private readonly LibraryFileServiceFixture fixture;

    private UploadServiceHarness(TempDataDir dir, LibraryFileServiceFixture fixture, SqliteTeamDirectory directory, User nova, ChatUploadService service, RoomUploadFolders folders)
    {
        this.dir = dir;
        this.fixture = fixture;
        this.Directory = directory;
        this.Nova = nova;
        this.Service = service;
        this.Folders = folders;
    }

    /// <summary>The temp <c>DataDir</c>.</summary>
    public string DataDir => this.dir.Path;

    /// <summary>The directory the service loads Rooms from.</summary>
    public SqliteTeamDirectory Directory { get; }

    /// <summary>An Agent user to put in test Rooms.</summary>
    public User Nova { get; }

    /// <summary>The service under test.</summary>
    public ChatUploadService Service { get; }

    /// <summary>The fake Recycle Bin the Library service under this harness sends to.</summary>
    public RecordingRecycleBin RecycleBin => this.fixture.RecycleBin;

    /// <summary>The folder lookup the service uses.</summary>
    public RoomUploadFolders Folders { get; }

    /// <summary>The options a <see cref="PendingUploads"/> over this harness reads its cap from.</summary>
    public IOptions<TeamOptions> Options { get; private init; } = Microsoft.Extensions.Options.Options.Create(new TeamOptions());

    /// <summary>Builds the harness.</summary>
    /// <param name="ct">Cancels the directory setup.</param>
    /// <param name="maxUploadBytes">An override for <see cref="LibraryOptions.MaxUploadBytes"/>, or <see langword="null"/> for the default.</param>
    /// <param name="maxUploadsPerMessage">An override for <see cref="LibraryOptions.MaxUploadsPerMessage"/>, or <see langword="null"/> for the default.</param>
    /// <param name="clock">The clock for pasted-image names, or <see langword="null"/> for the system clock.</param>
    public static async Task<UploadServiceHarness> BuildAsync(CancellationToken ct, long? maxUploadBytes = null, int? maxUploadsPerMessage = null, TimeProvider? clock = null)
    {
        TempDataDir dir = new();
        LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Attach(dir.Path);
        TeamOptions teamOptions = new() { DataDir = dir.Path };
        if (maxUploadBytes is long bytes)
        {
            teamOptions.Library.MaxUploadBytes = bytes;
        }

        if (maxUploadsPerMessage is int count)
        {
            teamOptions.Library.MaxUploadsPerMessage = count;
        }

        IOptions<TeamOptions> options = Microsoft.Extensions.Options.Options.Create(teamOptions);
        SqliteTeamDirectory directory = new(options);
        await directory.InitializeAsync("You", ct);
        User? nova = await directory.UpsertAgentUserAsync("Nova", null, ct);
        Assert.NotNull(nova);
        LibraryFileService files = fixture.CreateService();
        RoomUploadFolders folders = new(fixture.Resolver, files: files);
        ChatUploadService service = new(directory, files, folders, options, clock);
        return new UploadServiceHarness(dir, fixture, directory, nova, service, folders) { Options = options };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.fixture.Dispose();
        this.dir.Dispose();
    }
}

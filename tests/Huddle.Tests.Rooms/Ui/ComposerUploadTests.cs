using System.IO.Pipelines;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the attachment side of <see cref="Composer"/> (ChatUploads spec 6.5 and 7): the paperclip, the
/// chips, Send while a file is uploading and the final text of a Message with attachments.
/// </summary>
public sealed class ComposerUploadTests
{
    /// <summary>The Library is on, so the paperclip is shown and the script is attached with the file input's host element.</summary>
    [Fact]
    public async Task Render_LibraryOn_ShowsPaperclipAndAttachesTheInputElement()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);

        IRenderedComponent<ContainerFragment> cut = rig.Render();

        Assert.Single(cut.FindAll("button[aria-label='Attach files']"));
        var invocation = rig.Context.JSInterop.VerifyInvoke("teamComposer.attach");
        Assert.IsType<ElementReference>(invocation.Arguments[3]);
    }

    /// <summary>With the Library off there is no paperclip, and the script is attached with no file input.</summary>
    [Fact]
    public async Task Render_LibraryOff_HidesPaperclipAndAttachesNoInput()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct, libraryEnabled: false);

        IRenderedComponent<ContainerFragment> cut = rig.Render();

        Assert.Empty(cut.FindAll("button[aria-label='Attach files']"));
        Assert.Empty(cut.FindAll("input[type='file']"));
        var invocation = rig.Context.JSInterop.VerifyInvoke("teamComposer.attach");
        Assert.Null(invocation.Arguments[3]);
    }

    /// <summary>A picked file becomes a chip that shows its saved name and its size.</summary>
    [Fact]
    public async Task Pick_File_RendersChipWithNameAndSize()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("notes.txt", cut.Find(".upload-chip .upload-chip-name").TextContent.Trim());
            Assert.Equal("5 B", cut.Find(".upload-chip .upload-chip-size").TextContent.Trim());
        });
        Assert.Equal(UploadState.Done, Assert.Single(rig.Pending.For(rig.RoomId)).State);
    }

    /// <summary>A refused file stays as a chip that shows the refusal copy from spec 7.</summary>
    [Fact]
    public async Task Pick_Svg_ShowsRefusalCopyOnTheChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", "logo.svg"));

        cut.WaitForAssertion(() => Assert.Equal("SVG files can't be attached.", cut.Find(".upload-chip .upload-chip-error").TextContent.Trim()));
    }

    /// <summary>A file picked in a Room of an archived Team stays as a chip that shows the T15 refusal.</summary>
    [Fact]
    public async Task Pick_InArchivedTeamRoom_ShowsT15OnTheChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct, archivedTeamRoom: true);
        IRenderedComponent<ContainerFragment> cut = rig.Render();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));

        cut.WaitForAssertion(() => Assert.Equal("This Team is archived, so files can't be added here.", cut.Find(".upload-chip .upload-chip-error").TextContent.Trim()));
    }

    /// <summary>A pick past the per-Message file cap leaves no chip and shows the cap copy from spec 7 as the status line.</summary>
    [Fact]
    public async Task Pick_PastTheCountCap_ShowsTheCapCopy()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct, maxUploadsPerMessage: 1);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("one", "a.txt"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".upload-chip-size")));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("two", "b.txt"));

        cut.WaitForAssertion(() => Assert.Equal("A Message can carry up to 1 file.", cut.Find(".composer-error").TextContent.Trim()));
        Assert.Single(rig.Pending.For(rig.RoomId));
    }

    /// <summary>A file larger than the cap is refused up front with the spec 7 copy as the status line, leaves no chip and saves nothing.</summary>
    [Fact]
    public async Task Pick_FileOverTheSizeCap_ShowsLargerThanCopyAndNoChip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct, maxUploadBytes: 1024 * 1024);
        IRenderedComponent<ContainerFragment> cut = rig.Render();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(new string('x', (1024 * 1024) + 10), "big.txt"));

        cut.WaitForAssertion(() => Assert.Equal("big.txt is larger than 1 MB.", cut.Find(".composer-error").TextContent.Trim()));
        Assert.Empty(rig.Pending.For(rig.RoomId));
        Assert.Empty(cut.FindAll(".upload-chip"));
    }

    /// <summary>A chip added while the submit is awaited is not part of the Message and survives the send, file included.</summary>
    [Fact]
    public async Task SendAsync_ChipAddedDuringSubmit_SurvivesTheSend()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        await rig.AddAsync("a.txt", "one", ct);
        Pipe pipe = new();
        Task<PendingUpload>? late = null;
        rig.OnPosted = () => late = rig.Pending.AddAsync(rig.RoomId, "late.txt", _ => pipe.Reader.AsStream(), ct);

        await cut.InvokeAsync(() => cut.FindComponent<Composer>().Instance.SendAsync("hi"));

        Assert.NotNull(late);
        PendingUpload survivor = Assert.Single(rig.Pending.For(rig.RoomId));
        Assert.Equal("late.txt", survivor.DisplayName);
        await pipe.Writer.WriteAsync("later"u8.ToArray(), ct);
        await pipe.Writer.CompleteAsync();
        PendingUpload done = await late;
        Assert.Equal(UploadState.Done, done.State);
        Assert.True(File.Exists(done.File?.FullPath));
        Assert.Equal(UploadState.Done, Assert.Single(rig.Pending.For(rig.RoomId)).State);
    }

    /// <summary>The remove button of a chip (aria-label "Remove {name}") drops the chip and its file.</summary>
    [Fact]
    public async Task RemoveButton_DropsTheChipAndDeletesTheFile()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button[aria-label='Remove notes.txt']")));
        string path = rig.PathOf(0);

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Remove notes.txt']").ClickAsync());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".upload-chip")));
        Assert.Empty(rig.Pending.For(rig.RoomId));
        Assert.False(File.Exists(path));
    }

    /// <summary>Text with two completed chips is posted as the text, a blank line, then one code-span path line per chip.</summary>
    [Fact]
    public async Task SendAsync_TextAndTwoChips_PostsTheExactFinalText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        await rig.AddAsync("a.txt", "one", ct);
        await rig.AddAsync("b.txt", "two", ct);
        string expected = $"look at these\n\n`{rig.PathOf(0)}`\n`{rig.PathOf(1)}`";

        await cut.InvokeAsync(() => cut.FindComponent<Composer>().Instance.SendAsync("  look at these  "));

        Assert.Equal(expected, Assert.Single(rig.Posted));
        Assert.Empty(rig.Pending.For(rig.RoomId));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".upload-chip")));
    }

    /// <summary>A Message with chips and no text is allowed and is just the path line.</summary>
    [Fact]
    public async Task SendAsync_ChipsOnly_PostsThePathLine()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        await rig.AddAsync("a.txt", "one", ct);
        string expected = $"`{rig.PathOf(0)}`";

        await cut.InvokeAsync(() => cut.FindComponent<Composer>().Instance.SendAsync(""));

        Assert.Equal(expected, Assert.Single(rig.Posted));
    }

    /// <summary>Empty text and no chips still posts nothing.</summary>
    [Fact]
    public async Task SendAsync_NoTextNoChips_PostsNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();

        bool sent = await cut.InvokeAsync(() => cut.FindComponent<Composer>().Instance.SendAsync("   "));

        Assert.True(sent);
        Assert.Empty(rig.Posted);
    }

    /// <summary>A refused chip is not sent: the Message carries only the text, and the failed chip is cleared with the rest.</summary>
    [Fact]
    public async Task SendAsync_FailedChip_IsLeftOutOfTheMessage()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("<svg></svg>", "logo.svg"));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".upload-chip-error")));

        await cut.InvokeAsync(() => cut.FindComponent<Composer>().Instance.SendAsync("just text"));

        Assert.Equal("just text", Assert.Single(rig.Posted));
    }

    /// <summary>While a chip is still uploading Send is disabled, with the tooltip from spec 7, and nothing is posted.</summary>
    [Fact]
    public async Task Send_WhileUploading_IsDisabledAndPostsNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        Pipe pipe = new();
        Task upload = rig.Pending.AddAsync(rig.RoomId, "slow.bin", _ => pipe.Reader.AsStream(), ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();

        cut.WaitForAssertion(() => Assert.True(cut.Find("button[aria-label='Send message']").HasAttribute("disabled")));
        Assert.Contains(cut.FindComponents<MudBlazor.MudTooltip>(), tooltip => tooltip.Instance.Text == "Waiting for uploads…");
        bool sent = await cut.InvokeAsync(() => cut.FindComponent<Composer>().Instance.SendAsync("hello"));
        Assert.False(sent);
        Assert.Empty(rig.Posted);

        await pipe.Writer.CompleteAsync();
        await upload;
        cut.WaitForAssertion(() => Assert.False(cut.Find("button[aria-label='Send message']").HasAttribute("disabled")));
    }

    /// <summary>
    /// Three files picked together all show a chip at once, but only one stream is open at a time, the files are
    /// read in the order given and all three are saved. Three together timed out in two manual runs (issue 387).
    /// </summary>
    [Fact]
    public async Task Pick_ThreeFiles_ShowAllChipsAtOnceAndReadOneAtATimeInOrder()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        OpenStreams probe = new();
        GatedFile[] files = [new("a.txt", probe, gated: true), new("b.txt", probe, gated: true), new("c.txt", probe, gated: true)];
        MudBlazor.MudFileUpload<IReadOnlyList<IBrowserFile>> picker = cut.FindComponent<MudBlazor.MudFileUpload<IReadOnlyList<IBrowserFile>>>().Instance;

        Task picked = cut.InvokeAsync(() => picker.FilesChanged.InvokeAsync(files));
        await files[0].Opened.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".upload-chip").Count));

        // The second file must not open while the first is still being read. A parallel run opens it at once, so a
        // short wait is enough to see it; the wait only costs time when the code is right.
        await Task.WhenAny(files[1].Opened.Task, Task.Delay(TimeSpan.FromSeconds(1), ct));
        Assert.False(files[1].Opened.Task.IsCompleted);

        files[0].Gate.SetResult();
        await files[1].Opened.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        files[1].Gate.SetResult();
        await files[2].Opened.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        files[2].Gate.SetResult();
        await picked.WaitAsync(TimeSpan.FromSeconds(10), ct);

        Assert.Equal(1, probe.MostOpen);
        Assert.Equal(["a.txt", "b.txt", "c.txt"], probe.Order);
        Assert.All(rig.Pending.For(rig.RoomId), chip => Assert.Equal(UploadState.Done, chip.State));
        Assert.Equal(3, rig.Pending.For(rig.RoomId).Count);
    }

    /// <summary>A file that cannot be opened ends as a failed chip and the files after it are still saved.</summary>
    [Fact]
    public async Task Pick_MiddleFileFails_LaterFileIsStillSaved()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using Rig rig = await Rig.BuildAsync(ct);
        IRenderedComponent<ContainerFragment> cut = rig.Render();
        OpenStreams probe = new();
        GatedFile[] files = [new("a.txt", probe, gated: false), new("b.txt", probe, gated: false, failsToOpen: true), new("c.txt", probe, gated: false)];
        MudBlazor.MudFileUpload<IReadOnlyList<IBrowserFile>> picker = cut.FindComponent<MudBlazor.MudFileUpload<IReadOnlyList<IBrowserFile>>>().Instance;

        await cut.InvokeAsync(() => picker.FilesChanged.InvokeAsync(files)).WaitAsync(TimeSpan.FromSeconds(10), ct);

        IReadOnlyList<PendingUpload> chips = rig.Pending.For(rig.RoomId);
        Assert.Equal(3, chips.Count);
        Assert.Equal([UploadState.Done, UploadState.Failed, UploadState.Done], chips.Select(static chip => chip.State));
    }

    /// <summary>Counts how many fake browser streams are open now and the most there have been together.</summary>
    private sealed class OpenStreams
    {
        private readonly Lock gate = new();
        private int open;

        public int MostOpen { get; private set; }

        public List<string> Order { get; } = [];

        public void Opened(string name)
        {
            lock (this.gate)
            {
                this.open++;
                this.MostOpen = Math.Max(this.MostOpen, this.open);
                this.Order.Add(name);
            }
        }

        public void Closed()
        {
            lock (this.gate)
            {
                this.open--;
            }
        }
    }

    /// <summary>A picked file whose stream can hold its first read until the test opens <see cref="Gate"/>.</summary>
    private sealed class GatedFile(string name, OpenStreams probe, bool gated, bool failsToOpen = false) : IBrowserFile
    {
        public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => name;

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public long Size => 5;

        public string ContentType => "text/plain";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (failsToOpen)
            {
                throw new IOException("The browser stream timed out.");
            }

            probe.Opened(name);
            this.Opened.TrySetResult();
            return new GatedStream(new MemoryStream("hello"u8.ToArray()), gated ? this.Gate.Task : Task.CompletedTask, probe);
        }
    }

    /// <summary>A read-only stream that waits for a gate before its first read and reports when it is closed.</summary>
    private sealed class GatedStream(MemoryStream inner, Task gate, OpenStreams probe) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            return await inner.ReadAsync(buffer, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                probe.Closed();
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>A real Room, upload stack and Composer context over one temp data directory.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly TaskToolHarness harness;
        private readonly LibraryFileServiceFixture fixture;

        public Rig(TaskToolHarness harness, LibraryFileServiceFixture fixture, PendingUploads pending, string roomId)
        {
            this.harness = harness;
            this.fixture = fixture;
            this.Pending = pending;
            this.RoomId = roomId;
            this.Context = new MudBunitContext();
            this.Context.Services.AddSingleton(harness.Chat);
            this.Context.Services.AddSingleton(harness.Store);
            this.Context.Services.AddSingleton(harness.Events);
            this.Context.Services.AddSingleton(harness.Options);
            this.Context.Services.AddSingleton(pending);
            harness.RoomEvents.MessagePosted += e =>
            {
                this.Posted.Add(e.Message.Text);
                this.OnPosted?.Invoke();
            };
        }

        public Action? OnPosted { get; set; }

        public MudBunitContext Context { get; }

        public PendingUploads Pending { get; }

        public string RoomId { get; }

        public List<string> Posted { get; } = [];

        public static async Task<Rig> BuildAsync(CancellationToken ct, bool libraryEnabled = true, int? maxUploadsPerMessage = null, long? maxUploadBytes = null, bool archivedTeamRoom = false)
        {
            TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
            if (maxUploadBytes is long bytes)
            {
                harness.Options.Value.Library.MaxUploadBytes = bytes;
            }

            if (maxUploadsPerMessage is int cap)
            {
                harness.Options.Value.Library.MaxUploadsPerMessage = cap;
            }

            harness.Options.Value.Library.Enabled = libraryEnabled;
            LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
            LibraryFileService files = fixture.CreateService();
            RoomUploadFolders folders = new(fixture.Resolver, files);
            ChatUploadService service = new(harness.Directory, files, folders, harness.Options, new Agency.Huddle.App.Data.RawBaselineStore(harness.Options));
            PendingUploads pending = new(service, harness.Options);
            Room room = await harness.Chat.CreateRoomForAsync([harness.NovaId!], ct);
            if (archivedTeamRoom)
            {
                room = await harness.Directory.CreateRoomAsync("Archived", [KnownIds.Human, harness.NovaId!], team: "Home", ct: ct);
                string archivedTeam = Path.Combine(harness.Options.Value.DataDir, "Archive", "Home");
                Directory.CreateDirectory(archivedTeam);
                await File.WriteAllTextAsync(Path.Combine(archivedTeam, "_team.md"), "---\n---\n", ct);
            }

            return new Rig(harness, fixture, pending, room.Id);
        }

        public IRenderedComponent<ContainerFragment> Render() =>
            this.Context.RenderWithPopovers(builder =>
            {
                builder.OpenComponent<Composer>(0);
                builder.AddAttribute(1, nameof(Composer.RoomId), this.RoomId);
                builder.CloseComponent();
            });

        public string PathOf(int index)
        {
            LibraryPath? file = this.Pending.For(this.RoomId)[index].File;
            Assert.NotNull(file);
            return file.FullPath;
        }

        public async Task AddAsync(string name, string text, CancellationToken ct)
        {
            PendingUpload chip = await this.Pending.AddAsync(this.RoomId, name, _ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)), ct);
            Assert.Equal(UploadState.Done, chip.State);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Context.DisposeAsync();
            this.fixture.Dispose();
            this.harness.Dispose();
        }
    }
}

using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Uploads;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// The Description header's two attach buttons, the pending path lines, and what Save and Revert do with
/// the files uploaded during an edit (TaskAttachments spec 6.5 and 6.6).
/// </summary>
public sealed class TaskDetailAttachmentTests
{
    /// <summary>With the Library on, both buttons are in the Description header and Attach is enabled for a saved Task.</summary>
    [Fact]
    public async Task Render_LibraryOn_ShowsBothButtons_AttachEnabled()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);

        Assert.Single(cut.FindAll("button[aria-label='Link a Library file']"));
        Assert.False(cut.Find("button[aria-label='Attach a file']").HasAttribute("disabled"));
    }

    /// <summary>With the Library off, neither button is shown.</summary>
    [Fact]
    public async Task Render_LibraryOff_HidesBothButtons()
    {
        using TaskToolHarness harness = new();
        harness.Options.Value.Library.Enabled = false;
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);

        Assert.Empty(cut.FindAll("button[aria-label='Link a Library file']"));
        Assert.Empty(cut.FindAll("button[aria-label='Attach a file']"));
    }

    /// <summary>A Task that is not saved yet cannot take an upload: Attach is disabled and says why, while Link still works (A14).</summary>
    [Fact]
    public async Task Render_NewTaskDraft_AttachDisabledWithTooltip_LinkEnabled()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Draft), new TaskDraft("T", "Platform", null));
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
            builder.CloseComponent();
        });

        Assert.True(cut.Find("button[aria-label='Attach a file']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[aria-label='Link a Library file']").HasAttribute("disabled"));
        Assert.Contains(cut.FindComponents<MudTooltip>(), tip => tip.Instance.Text == "Save the Task first to attach files.");
    }

    /// <summary>A picked file is saved in the Task's folder and its path line lands in the pending description.</summary>
    [Fact]
    public async Task Attach_File_SavesIntoTaskFolder_AndAppendsPathLine()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        string expected = UploadPath(harness, task, "notes.txt");

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));

        cut.WaitForAssertion(() =>
        {
            Assert.True(File.Exists(expected));
            Assert.Contains(expected, DescriptionText(cut), StringComparison.Ordinal);
        });
    }

    /// <summary>A Link pick appends the file's path line to the description and shows the Edit view (A1).</summary>
    [Fact]
    public async Task Link_PickingALibraryFile_AppendsItsPathLine()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        string plan = Path.Combine(harness.Options.Value.DataDir, "Teams", "Growth", "plan.md");
        Directory.CreateDirectory(Path.GetDirectoryName(plan) ?? string.Empty);
        File.WriteAllText(plan, "plan");
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        ctx.Services.AddSingleton(library.RootStore);
        ctx.Services.AddSingleton(library.Resolver);
        ctx.Services.AddSingleton(library.CreateService());
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);

        // The click handler awaits the dialog's result, so the click only completes once a file is picked.
        Task linkClick = cut.InvokeAsync(() => cut.Find("button[aria-label='Link a Library file']").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".picker-row")));
        await cut.InvokeAsync(() => PickerRow(cut, "Teams").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotNull(PickerRow(cut, "Growth")));
        await cut.InvokeAsync(() => PickerRow(cut, "Growth").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotNull(PickerRow(cut, "plan.md")));
        await cut.InvokeAsync(() => PickerRow(cut, "plan.md").ClickAsync());
        await linkClick;

        cut.WaitForAssertion(() => Assert.Contains($"`{plan}`", DescriptionText(cut), StringComparison.Ordinal));
    }

    /// <summary>Revert deletes the files uploaded since the last Save and restores the description.</summary>
    [Fact]
    public async Task Revert_AfterAttach_DeletesTheUploadedFile_AndRestoresTheDescription()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        string uploaded = UploadPath(harness, task, "notes.txt");
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));
        cut.WaitForAssertion(() => Assert.True(File.Exists(uploaded)));

        await cut.InvokeAsync(() => cut.FindButton("Revert").ClickAsync());

        cut.WaitForAssertion(() => Assert.False(File.Exists(uploaded)));
        Assert.DoesNotContain(uploaded, DescriptionText(cut), StringComparison.Ordinal);
    }

    /// <summary>Revert deletes only the files of the current edit: one attached and saved earlier survives.</summary>
    [Fact]
    public async Task Revert_AfterSavedAttachment_KeepsTheOlderFile_DeletesTheNewOne()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        string older = UploadPath(harness, task, "older.txt");
        string newer = UploadPath(harness, task, "newer.txt");
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("one", "older.txt"));
        cut.WaitForAssertion(() => Assert.True(File.Exists(older)));
        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());
        cut.WaitForAssertion(() => Assert.Contains(older, harness.Store.Get(task.Id)?.Description, StringComparison.Ordinal));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("two", "newer.txt"));
        cut.WaitForAssertion(() => Assert.True(File.Exists(newer)));

        await cut.InvokeAsync(() => cut.FindButton("Revert").ClickAsync());

        cut.WaitForAssertion(() => Assert.False(File.Exists(newer)));
        Assert.True(File.Exists(older));
    }

    /// <summary>Save keeps the uploaded file and writes its path line into the Task's description.</summary>
    [Fact]
    public async Task Save_AfterAttach_KeepsTheFile_AndStoresThePathLine()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        string uploaded = UploadPath(harness, task, "notes.txt");
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));
        cut.WaitForAssertion(() => Assert.True(File.Exists(uploaded)));

        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());

        cut.WaitForAssertion(() => Assert.Contains($"`{uploaded}`", harness.Store.Get(task.Id)?.Description, StringComparison.Ordinal));
        Assert.True(File.Exists(uploaded));
    }

    /// <summary>Past the per-edit cap a pick is refused with the spec's text and no file is saved.</summary>
    [Fact]
    public async Task Attach_PastTheCap_ShowsTheCapMessage_AndSavesNothing()
    {
        using TaskToolHarness harness = new();
        harness.Options.Value.Library.MaxUploadsPerMessage = 2;
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("one", "first.txt"), InputFileContent.CreateFromText("uno", "also.txt"));
        cut.WaitForAssertion(() => Assert.True(File.Exists(UploadPath(harness, task, "also.txt"))));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("two", "second.txt"));

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.WaitForAssertion(() => Assert.Contains(snackbar.ShownSnackbars, shown => shown.Message == "A Task edit can attach up to 2 files. Save, then attach more."));
        Assert.False(File.Exists(UploadPath(harness, task, "second.txt")));
    }

    /// <summary>One pick of more files than the cap allows saves up to the cap and then shows the spec's cap text, not the file input's own.</summary>
    [Fact]
    public async Task Attach_OnePickPastTheCap_SavesUpToTheCap_ThenShowsTheCapMessage()
    {
        using TaskToolHarness harness = new();
        harness.Options.Value.Library.MaxUploadsPerMessage = 2;
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"), InputFileContent.CreateFromText("2", "b.txt"), InputFileContent.CreateFromText("3", "c.txt"));

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.WaitForAssertion(() => Assert.Contains(snackbar.ShownSnackbars, shown => shown.Message == "A Task edit can attach up to 2 files. Save, then attach more."));
        Assert.True(File.Exists(UploadPath(harness, task, "a.txt")));
        Assert.True(File.Exists(UploadPath(harness, task, "b.txt")));
        Assert.False(File.Exists(UploadPath(harness, task, "c.txt")));
    }

    /// <summary>
    /// Taking "theirs" for a conflicting Description drops the Human's pending description, so the files uploaded for it are deleted
    /// rather than left in the Task's folder with no line pointing at them.
    /// </summary>
    [Fact]
    public async Task Conflict_TakeTheirsForDescription_DeletesTheFilesUploadedDuringTheEdit()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        string uploaded = UploadPath(harness, task, "notes.txt");
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "notes.txt"));
        cut.WaitForAssertion(() => Assert.True(File.Exists(uploaded)));
        harness.Service.Update(task.Id, new TaskPatch { Description = "Theirs: a different plan." }, baseVersion: null, new TaskActor(TaskActorKind.Agent, "Nova", null));
        await cut.InvokeAsync(() => cut.FindButton("Save").ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".task-detail-conflict-row")));

        IRenderedComponent<MudRadioGroup<ConflictChoice?>> group = cut.FindComponent<MudRadioGroup<ConflictChoice?>>();
        await cut.InvokeAsync(() => group.Instance.ValueChanged.InvokeAsync(ConflictChoice.Theirs));

        cut.WaitForAssertion(() => Assert.False(File.Exists(uploaded)));
    }

    /// <summary>A refused file shows the writer's text in a snackbar and appends no line (A6).</summary>
    [Fact]
    public async Task Attach_SvgFile_ShowsRefusal_AndAppendsNothing()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        using LibraryFileServiceFixture library = LibraryFileServiceFixture.Attach(harness.Options.Value.DataDir);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        RegisterUploader(ctx, harness, library);
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", "logo.svg"));

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        cut.WaitForAssertion(() => Assert.NotEmpty(snackbar.ShownSnackbars));
        Assert.DoesNotContain("logo.svg", DescriptionText(cut), StringComparison.Ordinal);
        Assert.False(File.Exists(UploadPath(harness, task, "logo.svg")));
    }

    /// <summary>A path line in the description renders as a Library link in the Preview when the Library resolver is registered.</summary>
    [Fact]
    public async Task Preview_PathLine_RendersAsLibraryReference()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        string path = UploadPath(harness, task, "notes.txt");
        IRenderedComponent<ContainerFragment> cut = await PreviewAsync(ctx, task, path, exists: true);

        Assert.NotEmpty(cut.FindAll(".task-detail-description-preview .library-ref"));
        Assert.Empty(cut.FindAll(".task-detail-description-preview .library-ref-missing"));
    }

    /// <summary>A path whose file is gone renders dimmed with <c>library-ref-missing</c>.</summary>
    [Fact]
    public async Task Preview_MissingFile_RendersAsMissingReference()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        string path = UploadPath(harness, task, "notes.txt");
        IRenderedComponent<ContainerFragment> cut = await PreviewAsync(ctx, task, path, exists: false);

        Assert.NotEmpty(cut.FindAll(".task-detail-description-preview .library-ref-missing"));
    }

    /// <summary>With the Library off the Preview renders as before: the path stays plain code, never a link.</summary>
    [Fact]
    public async Task Preview_LibraryOff_RendersNoReference()
    {
        using TaskToolHarness harness = new();
        harness.Options.Value.Library.Enabled = false;
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        string path = UploadPath(harness, task, "notes.txt");
        IRenderedComponent<ContainerFragment> cut = await PreviewAsync(ctx, task, path, exists: true);

        Assert.Empty(cut.FindAll(".task-detail-description-preview .library-ref"));
        Assert.NotEmpty(cut.FindAll(".task-detail-description-preview code"));
    }

    /// <summary>Renders the panel, puts a path line in the description and switches to Preview.</summary>
    private static async Task<IRenderedComponent<ContainerFragment>> PreviewAsync(MudBunitContext ctx, TaskItem task, string path, bool exists)
    {
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver(resolvePath: p => string.Equals(p, path, StringComparison.Ordinal)
            ? new LibraryReference("uploads", $"Tasks/{task.Id}/notes.txt", exists)
            : null));
        IRenderedComponent<ContainerFragment> cut = RenderPanel(ctx, task.Id);
        IRenderedComponent<MudTextField<string>> field = cut.FindComponents<MudTextField<string>>().Single(m => m.Instance.Class?.Contains("task-detail-description-field", StringComparison.Ordinal) is true);
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync($"See\n\n`{path}`"));
        await cut.InvokeAsync(() => cut.FindButton("Preview").ClickAsync());
        return cut;
    }

    /// <summary>The text in the Description field, which is where a path line lands.</summary>
    private static string DescriptionText(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(".task-detail-description-field textarea").TextContent;

    /// <summary>The picker row whose text contains <paramref name="text"/>.</summary>
    private static AngleSharp.Dom.IElement PickerRow(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindAll(".picker-row").First(row => row.TextContent.Contains(text, StringComparison.Ordinal));

    /// <summary>Where an upload named <paramref name="name"/> lands for <paramref name="task"/>.</summary>
    private static string UploadPath(TaskToolHarness harness, TaskItem task, string name) =>
        Path.Combine(harness.Options.Value.DataDir, "Uploads", "Tasks", task.Id.ToString(), name);

    /// <summary>Registers a real <see cref="TaskUploadService"/> over the harness's data directory.</summary>
    private static void RegisterUploader(MudBunitContext ctx, TaskToolHarness harness, LibraryFileServiceFixture library)
    {
        TaskUploadFolders folders = new(library.Resolver);
        ctx.Services.AddSingleton(new TaskUploadService(harness.Store, folders, library.CreateService(), harness.Options));
    }

    /// <summary>Creates a Task with no assignee through the harness's service.</summary>
    private static TaskItem CreateTask(TaskToolHarness harness)
    {
        TaskResult result = harness.Service.Create(new TaskDraft("T", "Platform", null), TaskActors.Human(harness.Options.Value));
        return Assert.IsType<TaskResult.Saved>(result).Task;
    }

    /// <summary>Renders <c>TaskDetail</c> in Panel mode for <paramref name="id"/>.</summary>
    private static IRenderedComponent<ContainerFragment> RenderPanel(MudBunitContext ctx, TaskId id)
    {
        return ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<TaskDetail>(0);
            builder.AddAttribute(1, nameof(TaskDetail.Id), (TaskId?)id);
            builder.AddAttribute(2, nameof(TaskDetail.Mode), TaskDetailMode.Panel);
            builder.CloseComponent();
        });
    }
}

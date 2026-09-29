using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;
using Agency.Huddle.Tests.Tasks;
using Agency.Huddle.Tests.Teams;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins <see cref="LibraryFileOps"/> (Task 12.5.t), the dialogs it owns (<c>RenameDialog</c>,
/// <c>MoveDialog</c>) and the new <see cref="LibraryChange"/> shape, over a REAL
/// <see cref="LibraryFileService"/> on a temp tree (Spec §6.4, §6.5, §6.2, §8; corrections-B6 items
/// 18, 21 and 26). Tests tagged <c>[12.5a]</c> cover rename and move (preview, partial failure, the
/// <c>LinksNotUpdated</c> text, scope-limited Move, <c>BeforeChangeAsync</c>); tests tagged
/// <c>[12.5b]</c> cover delete, and New note/folder/Project (corrections-B6 item 31's split, kept in
/// one file per the task's Deliverable).
/// </summary>
public sealed class LibraryFileOpsTests : IDisposable
{
    private readonly OpsFixture fixture = OpsFixture.Build();

    /// <summary>Disposes the underlying temp <c>DataDir</c> and stores.</summary>
    public void Dispose() => this.fixture.Dispose();

    /// <summary>[12.5a] With links pointing at the renamed note, the dialog shows the exact settled count
    /// text and its primary button reads "Rename and update" (Spec §6.5; Settled texts).</summary>
    [Fact]
    public async Task Rename_WithLinks_ShowsCountAndPrimaryText()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Target.md"), "target body");
        File.WriteAllText(Path.Combine(root, "One.md"), "See [[Target]] and [[Target|alias]] and ![[Target]].");
        File.WriteAllText(Path.Combine(root, "Two.md"), "Also [[Target]].");
        File.WriteAllText(Path.Combine(root, "Three.md"), "And [[Target#heading]].");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Target.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Rename note", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(
            "5 links in 3 notes point here and will be updated.",
            cut.Find(".library-file-ops-link-count").TextContent.Trim());
        Assert.Equal(
            ["Rename and update", "Cancel"],
            cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());
    }

    /// <summary>[12.5a] With no links pointing at it, the primary button reads plain "Rename" and no count line is shown.</summary>
    [Fact]
    public async Task Rename_NoLinks_ShowsPlainRenameButton()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Lonely.md"), "no links point here");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Lonely.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal(
            ["Rename", "Cancel"],
            cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());
        Assert.Empty(cut.FindAll(".library-file-ops-link-count"));
    }

    /// <summary>[12.5a] Confirming Rename calls the service, renames on disk and raises <c>OnChanged</c> with the
    /// old and new <see cref="LibraryPath"/>s and the rewritten-notes list (corrections-B6 item 21).</summary>
    [Fact]
    public async Task Rename_Confirm_CallsServiceAndRaisesOnChanged()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Target.md"), "target body");
        File.WriteAllText(Path.Combine(root, "Linker.md"), "See [[Target]].");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Target.md");
        List<LibraryChange> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx, onChanged: EventCallback.Factory.Create<LibraryChange>(this, c => changes.Add(c)));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Renamed.md"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());
        cut.WaitForAssertion(() => Assert.Single(changes), TimeSpan.FromSeconds(5));

        Assert.False(File.Exists(Path.Combine(root, "Target.md")));
        Assert.True(File.Exists(Path.Combine(root, "Renamed.md")));
        Assert.Contains("[[Renamed]]", File.ReadAllText(Path.Combine(root, "Linker.md")), StringComparison.Ordinal); // contains-ok: disk file content, not markup; "See [[Renamed]]." is the exact rewritten sentence
        LibraryChange change = Assert.Single(changes);
        Assert.Equal("Target.md", change.OldPath?.RelativePath);
        Assert.Equal("Renamed.md", change.NewPath?.RelativePath);
        Assert.Equal(["Linker.md"], change.RewrittenNotes);
    }

    /// <summary>[12.5a] An invalid new name is refused inline in the dialog (the settled name-refusal text), the
    /// dialog stays open, and nothing on disk changes.</summary>
    [Fact]
    public async Task Rename_InvalidName_ShowsRefusalInline()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Target.md"), "target body");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Target.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("CON.md"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());

        Assert.Equal("\"CON.md\" is reserved by Windows.", cut.Find(".library-file-ops-error").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".mud-dialog-title"));
        Assert.True(File.Exists(Path.Combine(root, "Target.md")));
        Assert.False(File.Exists(Path.Combine(root, "CON.md")));
    }

    /// <summary>[12.5a] When one linking note can't be rewritten (locked on disk during the rewrite), the
    /// result dialog lists it under the settled partial-failure text (Spec §6.5 item 4).</summary>
    [Fact]
    public async Task Rename_PartialFailure_ShowsList()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Target.md"), "target body");
        File.WriteAllText(Path.Combine(root, "Locked.md"), "See [[Target]].");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Target.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Renamed.md"));

        using (FileStream hold = new(Path.Combine(root, "Locked.md"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".library-file-ops-partial-failure")), TimeSpan.FromSeconds(5));
        }

        Assert.Equal(
            "Renamed. 1 notes couldn't be updated:",
            cut.Find(".library-file-ops-partial-failure-heading").TextContent.Trim());
        Assert.Equal(["Locked.md"], cut.FindAll(".library-file-ops-partial-failure-item").Select(e => e.TextContent.Trim()));
        Assert.True(File.Exists(Path.Combine(root, "Renamed.md")));
    }

    /// <summary>[12.5a] When the root's index isn't available (over <c>MaxIndexedFiles</c>), the move still
    /// happens but the dialog shows the settled "too many files to index" text instead of a count, and no
    /// rewrite is attempted (Spec §6.5; corrections-B5 item 8).</summary>
    [Fact]
    public async Task Rename_IndexUnavailable_ShowsLinksNotUpdatedText()
    {
        using OpsFixture indexLimitedFixture = OpsFixture.Build(options => options.Library.MaxIndexedFiles = 1);
        string root = indexLimitedFixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "A.md"), "a");
        File.WriteAllText(Path.Combine(root, "Target.md"), "target body");
        LibraryPath target = indexLimitedFixture.LibraryFixture.Resolve(root, "Target.md");

        await using MudBunitContext ctx = indexLimitedFixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal(
            "Links weren't updated: this root has too many files to index.",
            cut.Find(".library-file-ops-links-not-updated").TextContent.Trim());
        Assert.Equal(["Rename", "Cancel"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());
    }

    /// <summary>[12.5a] The Move dialog's destination list holds only folders inside the item's own Library
    /// Root - a folder under a second pinned root, and the Teams root, never appear (Spec §6.4 "within one
    /// root only").</summary>
    [Fact]
    public async Task Move_ListsOnlyFoldersInSameRoot()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "A.md"), "a");
        string otherRoot = this.fixture.LibraryFixture.CreatePinnedRoot("Other");
        Directory.CreateDirectory(Path.Combine(otherRoot, "OtherSub"));
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "A.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Move, item, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Move A.md", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(
            ["Notes", "Sub"],
            cut.FindAll(".library-file-ops-move-folder").Select(e => e.TextContent.Trim()));
    }

    /// <summary>[12.5a] In a scoped explorer (a <see cref="LibraryFileOps.Scope"/>), Move lists only folders
    /// inside that scope, not sibling folders of the same root that fall outside it (corrections-B6 item 26).</summary>
    [Fact]
    public async Task Move_Scoped_ListsOnlyFoldersInsideScope()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Outside"));
        Directory.CreateDirectory(Path.Combine(root, "Inside"));
        Directory.CreateDirectory(Path.Combine(root, "Inside", "Deeper"));
        File.WriteAllText(Path.Combine(root, "Inside", "A.md"), "a");
        LibraryPath scope = this.fixture.LibraryFixture.Resolve(root, "Inside");
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "Inside/A.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx, scope: scope);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Move, item, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal(
            ["Inside", "Deeper"],
            cut.FindAll(".library-file-ops-move-folder").Select(e => e.TextContent.Trim()));
    }

    /// <summary>[12.5a] When <see cref="LibraryFileOps.BeforeChangeAsync"/> returns <see langword="false"/>,
    /// confirming Rename leaves the source untouched on disk and never raises <c>OnChanged</c>
    /// (corrections-B6 item 21: "stale save resurrects files").</summary>
    [Fact]
    public async Task Rename_BeforeChangeAsyncReturnsFalse_AbortsNothingTouched()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Target.md"), "target body");
        LibraryPath target = this.fixture.LibraryFixture.Resolve(root, "Target.md");
        List<LibraryChange> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(
            ctx,
            onChanged: EventCallback.Factory.Create<LibraryChange>(this, c => changes.Add(c)),
            beforeChangeAsync: _ => Task.FromResult(false));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Rename, target, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Renamed.md"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());

        Assert.True(File.Exists(Path.Combine(root, "Target.md")));
        Assert.False(File.Exists(Path.Combine(root, "Renamed.md")));
        Assert.Empty(changes);
    }

    /// <summary>[12.5a] The same abort applies to Move: <c>BeforeChangeAsync</c> returning
    /// <see langword="false"/> leaves the item in its original folder and raises no <c>OnChanged</c>.</summary>
    [Fact]
    public async Task Move_BeforeChangeAsyncReturnsFalse_AbortsNothingTouched()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        Directory.CreateDirectory(Path.Combine(root, "Sub"));
        File.WriteAllText(Path.Combine(root, "A.md"), "a");
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "A.md");
        LibraryPath destination = this.fixture.LibraryFixture.Resolve(root, "Sub");
        List<LibraryChange> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(
            ctx,
            onChanged: EventCallback.Factory.Create<LibraryChange>(this, c => changes.Add(c)),
            beforeChangeAsync: _ => Task.FromResult(false));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Move, item, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.Find($".library-file-ops-move-folder[data-path='{destination.RelativePath}']").Click());
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());

        Assert.True(File.Exists(Path.Combine(root, "A.md")));
        Assert.False(File.Exists(Path.Combine(root, "Sub", "A.md")));
        Assert.Empty(changes);
    }

    /// <summary>[12.5b] Delete asks the settled confirm text through <c>ShowMessageBoxAsync</c>, and confirming
    /// sends the item to the Recycle Bin through the real service (Spec §6.4 <c>RecycleAsync</c>; Settled texts).</summary>
    [Fact]
    public async Task Delete_Confirm_Recycles()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Gone.md"), "body");
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "Gone.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Delete, item, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Delete Gone.md?", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("It goes to the Recycle Bin.", cut.Find(".mud-dialog-content").TextContent.Trim());
        Assert.Equal(
            ["Cancel", "Delete"],
            cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).Click());
        cut.WaitForAssertion(() => Assert.NotEmpty(this.fixture.LibraryFixture.RecycleBin.Sent), TimeSpan.FromSeconds(5));
        Assert.Equal([item.FullPath], this.fixture.LibraryFixture.RecycleBin.Sent);
    }

    /// <summary>[12.5b] When the Recycle Bin is unavailable, the refusal shows through <c>ISnackbar</c> with the
    /// exact settled text, and the confirm dialog closes without touching the item (Settled texts).</summary>
    [Fact]
    public async Task Delete_Unavailable_ShowsSnackbarText()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Stuck.md"), "body");
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "Stuck.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;
        this.fixture.LibraryFixture.RecycleBin.FailureError = "Couldn't delete: the Recycle Bin isn't available here.";
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Delete, item, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").Single(b => string.Equals(b.TextContent.Trim(), "Delete", StringComparison.Ordinal)).Click());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Assert.Equal("Couldn't delete: the Recycle Bin isn't available here.", snackbar.ShownSnackbars.Single().Message);
        Assert.True(File.Exists(Path.Combine(root, "Stuck.md")));
    }

    /// <summary>[12.5b] Deleting the open document asks the host through <c>BeforeChangeAsync</c> first; a
    /// <see langword="false"/> answer aborts before the confirm dialog even sends anything to the Recycle Bin
    /// (corrections-B6 item 21).</summary>
    [Fact]
    public async Task Delete_BeforeChangeAsyncReturnsFalse_AbortsNothingTouched()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(root, "Open.md"), "body");
        LibraryPath item = this.fixture.LibraryFixture.Resolve(root, "Open.md");

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx, beforeChangeAsync: _ => Task.FromResult(false));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        await cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.Delete, item, null)));

        Assert.Empty(cut.FindAll(".mud-dialog-title"));
        Assert.True(File.Exists(Path.Combine(root, "Open.md")));
        Assert.Empty(this.fixture.LibraryFixture.RecycleBin.Sent);
    }

    /// <summary>[12.5b] New Project on a Team node opens the settled prompt (title, label, buttons), and
    /// confirming creates the folder through <c>TeamFolderProvisioner.EnsureProject</c> and raises
    /// <c>OnChanged</c> with only <c>NewPath</c> set (Spec §6.2; corrections-B6 item 31).</summary>
    [Fact]
    public async Task NewProject_OnTeam_CreatesFolder()
    {
        string teamsPath = this.fixture.LibraryFixture.DataDir;
        Directory.CreateDirectory(Path.Combine(teamsPath, "Teams", "Acme"));
        this.fixture.LibraryFixture.Reload();
        LibraryPath teamFolder = this.fixture.LibraryFixture.ResolveTeams("Acme");
        List<LibraryChange> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx, onChanged: EventCallback.Factory.Create<LibraryChange>(this, c => changes.Add(c)));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.NewProject, teamFolder, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("New project", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Name", cut.Find(".library-file-ops-name-field label").TextContent.Trim());
        Assert.Equal(["Create", "Cancel"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Launch Q4"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());
        cut.WaitForAssertion(() => Assert.Single(changes), TimeSpan.FromSeconds(5));

        Assert.True(Directory.Exists(Path.Combine(teamsPath, "Teams", "Acme", "Launch Q4")));
        LibraryChange change = Assert.Single(changes);
        Assert.Null(change.OldPath);
        Assert.Equal("Acme/Launch Q4", change.NewPath?.RelativePath);
    }

    /// <summary>[12.5b] New note opens the settled prompt, and confirming creates the file and raises
    /// <c>OnChanged</c> so the host opens it (Spec §6.4 create row; corrections-B6 item 31).</summary>
    [Fact]
    public async Task NewNote_CreatesAndOpens()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath folder = this.fixture.LibraryFixture.Resolve(root, string.Empty);
        List<LibraryChange> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx, onChanged: EventCallback.Factory.Create<LibraryChange>(this, c => changes.Add(c)));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.NewNote, folder, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal("Name", cut.Find(".library-file-ops-name-field label").TextContent.Trim());
        Assert.Equal(["Create", "Cancel"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Meeting notes"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());
        cut.WaitForAssertion(() => Assert.Single(changes), TimeSpan.FromSeconds(5));

        Assert.True(File.Exists(Path.Combine(root, "Meeting notes.md")));
        LibraryChange change = Assert.Single(changes);
        Assert.Null(change.OldPath);
        Assert.Equal("Meeting notes.md", change.NewPath?.RelativePath);
    }

    /// <summary>[12.5b] New folder opens the settled prompt with its own title, and confirming creates a
    /// folder rather than a <c>.md</c> file (Spec §6.4 create row; corrections-B6 item 31).</summary>
    [Fact]
    public async Task NewFolder_CreatesFolder()
    {
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath folder = this.fixture.LibraryFixture.Resolve(root, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        _ = cut.InvokeAsync(() => ops.HandleAsync(new LibraryTreeAction(LibraryTreeActionKind.NewFolder, folder, null)));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("New folder", cut.Find(".mud-dialog-title").TextContent.Trim());

        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Archive"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());
        cut.WaitForAssertion(() => Assert.True(Directory.Exists(Path.Combine(root, "Archive"))), TimeSpan.FromSeconds(5));

        Assert.False(File.Exists(Path.Combine(root, "Archive.md")));
    }

    /// <summary>[6.6] <see cref="LibraryFileOps.NewNoteAsync"/> opens the same settled New note prompt, and
    /// confirming creates the file and returns the created note's whole path (Spec §6.8).</summary>
    [Fact]
    public async Task NewNoteAsync_Confirmed_ReturnsTheCreatedPath()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath folder = this.fixture.LibraryFixture.Resolve(root, string.Empty);

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx);
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;

        LibraryPath? created = null;
        Task pending = cut.InvokeAsync(async () => created = await ops.NewNoteAsync(folder));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim());
        Assert.Equal(["Create", "Cancel"], cut.FindAll(".mud-dialog-actions button").Select(b => b.TextContent.Trim()).ToList());

        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Meeting notes"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button")[0].Click());
        await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.True(File.Exists(Path.Combine(root, "Meeting notes.md")));
        Assert.NotNull(created);
        Assert.Equal(this.fixture.LibraryFixture.Resolve(root, "Meeting notes.md"), created);
        Assert.Equal("Meeting notes.md", created.RelativePath);
    }

    /// <summary>[6.6] Cancelling the prompt returns <see langword="null"/>, creates nothing and raises no
    /// <c>OnChanged</c>.</summary>
    [Fact]
    public async Task NewNoteAsync_WhenCancelled_ReturnsNull_AndCreatesNothing()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        string root = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        LibraryPath folder = this.fixture.LibraryFixture.Resolve(root, string.Empty);
        List<LibraryChange> changes = [];

        await using MudBunitContext ctx = this.fixture.NewContext();
        IRenderedComponent<ContainerFragment> cut = RenderOps(ctx, onChanged: EventCallback.Factory.Create<LibraryChange>(this, c => changes.Add(c)));
        LibraryFileOps ops = cut.FindComponent<LibraryFileOps>().Instance;
        List<string> before = [.. Directory.GetFileSystemEntries(root)];

        LibraryPath? created = null;
        bool completed = false;
        Task pending = cut.InvokeAsync(async () =>
        {
            created = await ops.NewNoteAsync(folder);
            completed = true;
        });
        cut.WaitForAssertion(() => Assert.Equal("New note", cut.Find(".mud-dialog-title").TextContent.Trim()));
        await cut.InvokeAsync(() => cut.Find(".library-file-ops-name-field input").Change("Meeting notes"));
        await cut.InvokeAsync(() => cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Trim() == "Cancel").Click());
        await pending.WaitAsync(TimeSpan.FromSeconds(5), ct);

        Assert.True(completed);
        Assert.Null(created);
        List<string> after = [.. Directory.GetFileSystemEntries(root)];
        Assert.Equal(before, after);
        Assert.Empty(changes);
    }

    private static IRenderedComponent<ContainerFragment> RenderOps(
        MudBunitContext ctx,
        LibraryPath? scope = null,
        EventCallback<LibraryChange> onChanged = default,
        Func<LibraryPath, Task<bool>>? beforeChangeAsync = null) => ctx.RenderWithPopovers(builder =>
    {
        builder.OpenComponent<LibraryFileOps>(0);
        builder.AddAttribute(1, nameof(LibraryFileOps.Scope), scope);
        builder.AddAttribute(2, nameof(LibraryFileOps.OnChanged), onChanged);
        builder.AddAttribute(3, nameof(LibraryFileOps.BeforeChangeAsync), beforeChangeAsync);
        builder.CloseComponent();
    });

    private sealed class OpsFixture : IDisposable
    {
        private readonly TempDataDir dir;
        private readonly PersonaStore personas;

        private OpsFixture(TempDataDir dir, LibraryFileServiceFixture libraryFixture, PersonaStore personas)
        {
            this.dir = dir;
            this.LibraryFixture = libraryFixture;
            this.personas = personas;
        }

        /// <summary>The real Library stack (resolver, root store, file service) over this fixture's temp <c>DataDir</c>.</summary>
        public LibraryFileServiceFixture LibraryFixture { get; }

        /// <summary>Builds a fixture with the standard Teams/Teammates layout, optionally customising
        /// <see cref="TeamOptions"/> first (e.g. <see cref="LibraryOptions.MaxIndexedFiles"/>).</summary>
        /// <param name="configure">An optional callback to customise the bound <see cref="TeamOptions"/> before use.</param>
        public static OpsFixture Build(Action<TeamOptions>? configure = null)
        {
            TempDataDir dir = new();
            LibraryFileServiceFixture libraryFixture = LibraryFileServiceFixture.Attach(dir.Path, configure);
            PersonaStore personas = TestTaskStore.CreatePersonaStore(dir);
            return new OpsFixture(dir, libraryFixture, personas);
        }

        /// <summary>A <see cref="MudBunitContext"/> with this fixture's real <see cref="LibraryFileService"/>,
        /// <see cref="LibraryPathResolver"/>, <see cref="LibraryRootStore"/> and a real
        /// <see cref="TeamFolderProvisioner"/> registered.</summary>
        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.LibraryFixture.Resolver);
            ctx.Services.AddSingleton(this.LibraryFixture.RootStore);
            ctx.Services.AddSingleton(this.LibraryFixture.CreateService());
            ctx.Services.AddSingleton(new TeamFolderProvisioner(
                this.personas, this.LibraryFixture.RootStore, this.LibraryFixture.Resolver, NullLogger<TeamFolderProvisioner>.Instance, catalog: new FakeTeamCatalog()));
            return ctx;
        }

        /// <summary>Disposes the underlying stores and temp <c>DataDir</c>.</summary>
        public void Dispose()
        {
            this.personas.Dispose();
            this.LibraryFixture.Dispose();
            this.dir.Dispose();
        }
    }
}

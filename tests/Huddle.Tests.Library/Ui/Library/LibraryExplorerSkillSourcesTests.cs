using Agency.Huddle.App.Library;
using Bunit;
using Bunit.Rendering;
using ExplorerFixture = Agency.Huddle.Tests.Ui.Library.LibraryExplorerTests.ExplorerFixture;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>Pins what the Skill Sources root shows first (Skill Packs spec §6.11.4): SKILLS.md when nothing else is open, a
/// remembered selection winning over it, other roots unchanged, and a tree menu with no enabled write verb.</summary>
public sealed class LibraryExplorerSkillSourcesTests : IDisposable
{
    private readonly ExplorerFixture fixture = ExplorerFixture.Build();

    /// <summary>Writes SKILLS.md and one other note into the Skill Sources folder.</summary>
    public LibraryExplorerSkillSourcesTests()
    {
        string folder = Path.Combine(this.fixture.LibraryFixture.DataDir, "SkillSources");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILLS.md"), "skills body");
        File.WriteAllText(Path.Combine(folder, "notes.md"), "notes body");
    }

    /// <summary>Disposes the temp data folder.</summary>
    public void Dispose()
    {
        this.fixture.Dispose();
    }

    /// <summary>Opening the root with nothing remembered shows SKILLS.md.</summary>
    [Fact]
    public async Task OpenRoot_NoSelection_ShowsSkillsMd()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.Module(JsModule.Storage).Setup<string?>("get", _ => true).SetResult(null);

        IRenderedComponent<ContainerFragment> cut = LibraryExplorerTests.RenderExplorer(ctx, scopes: [new LibraryLocation(LibraryRootStore.SkillSourcesRootId, string.Empty)]);

        cut.WaitForAssertion(() => Assert.Equal("skills body", cut.Find(".library-rendered").TextContent.Trim()));
    }

    /// <summary>A remembered selection in the root wins over SKILLS.md.</summary>
    [Fact]
    public async Task OpenRoot_RememberedSelection_Wins()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.Module(JsModule.Storage).Setup<string?>("get", invocation => Equals(invocation.Arguments[0], "library:test:open")).SetResult("skill-sources/notes.md");
        ctx.Module(JsModule.Storage).Setup<string?>("get", invocation => !Equals(invocation.Arguments[0], "library:test:open")).SetResult(null);

        IRenderedComponent<ContainerFragment> cut = LibraryExplorerTests.RenderExplorer(ctx, scopes: [new LibraryLocation(LibraryRootStore.SkillSourcesRootId, string.Empty)]);

        cut.WaitForAssertion(() => Assert.Equal("notes body", cut.Find(".library-rendered").TextContent.Trim()));
    }

    /// <summary>Another root opens nothing by itself, and the whole Library (every root) does not jump to SKILLS.md.</summary>
    [Fact]
    public async Task OtherRootOrWholeLibrary_OpensNothing()
    {
        string notes = this.fixture.LibraryFixture.CreatePinnedRoot("Notes");
        File.WriteAllText(Path.Combine(notes, "SKILLS.md"), "pinned body");
        await using MudBunitContext pinnedContext = this.fixture.NewContext();
        await using MudBunitContext wholeContext = this.fixture.NewContext();
        pinnedContext.Module(JsModule.Storage).Setup<string?>("get", _ => true).SetResult(null);
        wholeContext.Module(JsModule.Storage).Setup<string?>("get", _ => true).SetResult(null);

        IRenderedComponent<ContainerFragment> pinned = LibraryExplorerTests.RenderExplorer(pinnedContext, scopes: [new LibraryLocation("notes", string.Empty)]);
        IRenderedComponent<ContainerFragment> whole = LibraryExplorerTests.RenderExplorer(wholeContext, scopes: null);

        pinned.WaitForAssertion(() => Assert.NotEmpty(pinned.FindAll(".library-tree-node-name")));
        whole.WaitForAssertion(() => Assert.NotEmpty(whole.FindAll(".library-tree-node-name")));
        Assert.Empty(pinned.FindAll(".library-rendered"));
        Assert.Empty(whole.FindAll(".library-rendered"));
    }

    /// <summary>The tree menu on SKILLS.md offers nothing that writes (no Rename, Move, Delete, New note or New folder),
    /// and says why.</summary>
    [Fact]
    public async Task TreeMenu_OnSkillsMd_HasNoWriteVerb()
    {
        await using MudBunitContext ctx = this.fixture.NewContext();
        ctx.Module(JsModule.Storage).Setup<string?>("get", _ => true).SetResult(null);
        IRenderedComponent<ContainerFragment> cut = LibraryExplorerTests.RenderExplorer(ctx, scopes: [new LibraryLocation(LibraryRootStore.SkillSourcesRootId, string.Empty)]);
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll(".library-tree-node-name"), node => node.TextContent.Trim() == "SKILLS.md"));

        AngleSharp.Dom.IElement node = cut.FindAll("div.library-tree-node").First(element => element.QuerySelector(".library-tree-node-name")?.TextContent.Trim() == "SKILLS.md");
        await cut.InvokeAsync(() => node.ContextMenuAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-menu-item")));

        IReadOnlyList<string> labels = [.. cut.FindAll(".library-tree-menu-label").Select(element => element.TextContent.Trim())];
        Assert.DoesNotContain("Rename…", labels);
        Assert.DoesNotContain("Move…", labels);
        Assert.DoesNotContain("Delete…", labels);
        Assert.DoesNotContain("New note…", labels);
        Assert.DoesNotContain("New folder…", labels);
        Assert.Contains("Copy path", labels);
        Assert.Equal(LibraryPath.ReadOnlyReason, cut.Find(".library-tree-menu-note").TextContent.Trim());
    }
}

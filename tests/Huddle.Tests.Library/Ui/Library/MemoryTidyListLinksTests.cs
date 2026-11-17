using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;
using static Agency.Huddle.Tests.Ui.Library.LibraryDocumentTestSupport;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>
/// Pins the links of <c>changes.md</c> (Memory Tidy spec §6.13 and §8.1, "The list's links") in the document viewer, over the real Library stack and the real
/// reference resolver on a temp data folder: a wikilink to a copy opens the copy in the Memory backups root, and the live file, which is in another root,
/// opens through a link that names its root. A live file written as its absolute path in a code span is not linked by the viewer, and the last test pins that.
/// </summary>
public sealed class MemoryTidyListLinksTests : IDisposable
{
    private const string PassId = "2026-11-22";

    private readonly LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();

    /// <summary>Disposes the temp data folder.</summary>
    public void Dispose() => this.fixture.Dispose();

    private string AgentMemoryFolder => Path.Combine(this.fixture.DataDir, "Agents", "Nova", "memory");

    private string PassFolder => Path.Combine(this.fixture.DataDir, "MemoryBackups", PassId);

    private static void WriteFile(string path, string text)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path) ?? path);
        File.WriteAllText(path, text);
    }

    /// <summary>Writes the live file, its two copies and a journal of one applied merge, then the list through the product's own writer.</summary>
    /// <returns>The list's path in the Library.</returns>
    private LibraryPath WriteMergedPass()
    {
        WriteFile(Path.Combine(this.AgentMemoryFolder, "pricing.md"), "merged text");
        WriteFile(Path.Combine(this.PassFolder, "Agents", "Nova", "pricing.md"), "old pricing");
        WriteFile(Path.Combine(this.PassFolder, "Agents", "Nova", "price-list.md"), "old price list");
        MemoryTidyJournal journal = new(this.PassFolder);
        journal.Append(new MemoryTidyEntry.PassStarted(PassId, new DateTimeOffset(2026, 11, 22, 3, 0, 0, TimeSpan.Zero), MemoryTidyStart.Timetable));
        journal.Append(new MemoryTidyEntry.StepStarted(1, "agent:Nova", "merge", [new("pricing.md", "v1"), new("price-list.md", "v1")], "the same price list in two files"));
        journal.Append(new MemoryTidyEntry.StepApplied(1, "agent:Nova", ["pricing.md"], ["price-list.md"], ["Agents/Nova/pricing.md", "Agents/Nova/price-list.md"], [], [], false, null));
        _ = MemoryTidyList.Write(
            this.PassFolder,
            14,
            key => string.Equals(key, "agent:Nova", StringComparison.Ordinal) ? this.AgentMemoryFolder : null,
            absolute => MemoryTidyList.LibraryHref(this.fixture.Resolver, absolute));
        Assert.True(this.fixture.Resolver.TryResolve("memory-backups", PassId + "/changes.md", out LibraryPath? list, out _));
        return list;
    }

    private MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        LibraryReferenceResolver references = new(this.fixture.Resolver, this.fixture.Index, this.fixture.BoundOptions);
        ctx.Services.AddSingleton(this.fixture.Resolver);
        ctx.Services.AddSingleton(this.fixture.RootStore);
        ctx.Services.AddSingleton(this.fixture.CreateService());
        ctx.Services.AddSingleton(this.fixture.Index);
        ctx.Services.AddSingleton<ILibraryNoteResolver>(references);
        ctx.Services.AddDocHistoryServices();
        ctx.Services.AddSingleton<Agency.Huddle.App.Tasks.ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        return ctx;
    }

    /// <summary>The wikilink to a copy resolves, from the list inside the backup root, to the copy in that root and opens it.</summary>
    [Fact]
    public async Task WikiLinkToACopy_OpensTheCopyInTheBackupRoot()
    {
        LibraryPath list = this.WriteMergedPass();
        LibraryPath? navigated = null;
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, list, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, p => navigated = p));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("a.library-ref")));
        AngleSharp.Dom.IElement link = cut.FindAll("a.library-ref").Single(static anchor => anchor.TextContent.Contains("pricing.md", StringComparison.Ordinal) && !anchor.TextContent.Contains("price-list", StringComparison.Ordinal));
        Assert.Equal("?library=memory-backups/" + PassId + "/Agents/Nova/pricing.md", link.GetAttribute("href"));
        Assert.DoesNotContain("library-ref-missing", link.ClassName ?? string.Empty, StringComparison.Ordinal);
        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(link.GetAttribute("href") ?? string.Empty));
        Assert.NotNull(navigated);
        Assert.Equal("memory-backups", navigated.Root.Id);
        Assert.Equal(PassId + "/Agents/Nova/pricing.md", navigated.RelativePath);
    }

    /// <summary>The live file, in the Agents root, is a link that names its own root, and a click on it opens the live file.</summary>
    [Fact]
    public async Task LiveFileLink_OpensTheLiveFileInItsOwnRoot()
    {
        LibraryPath list = this.WriteMergedPass();
        string live = Path.Combine(this.AgentMemoryFolder, "pricing.md");
        LibraryPath? navigated = null;
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, list, onNavigate: EventCallback.Factory.Create<LibraryPath>(this, p => navigated = p));

        string expected = MemoryTidyList.LibraryHref(this.fixture.Resolver, live) ?? string.Empty;
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("a[href^='?library=']")));
        AngleSharp.Dom.IElement link = cut.FindAll("a[href^='?library=']").Single(anchor => string.Equals(anchor.GetAttribute("href"), expected, StringComparison.Ordinal));
        Assert.DoesNotContain("memory-backups", expected, StringComparison.Ordinal);
        await cut.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo(link.GetAttribute("href") ?? string.Empty));
        Assert.NotNull(navigated);
        Assert.Equal(Path.GetFullPath(live), Path.GetFullPath(navigated.FullPath));
        Assert.NotEqual("memory-backups", navigated.Root.Id);
    }

    /// <summary>
    /// The finding behind the chosen form: the document viewer renders an absolute path in a code span as plain code, with no link, so the list does not use it
    /// for the live file. If this test fails, the viewer links paths in documents and the list's form can go back to the one the spec first wrote.
    /// </summary>
    [Fact]
    public async Task AbsolutePathInACodeSpan_IsNotLinkedInTheDocumentViewer()
    {
        string live = Path.Combine(this.AgentMemoryFolder, "pricing.md");
        WriteFile(live, "merged text");
        WriteFile(Path.Combine(this.PassFolder, "changes.md"), "Now: `" + live + "`");
        Assert.True(this.fixture.Resolver.TryResolve("memory-backups", PassId + "/changes.md", out LibraryPath? list, out _));
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = RenderDoc(ctx, list);

        cut.WaitForAssertion(() => Assert.Equal(live, cut.Find(".library-rendered code").TextContent));
        Assert.Empty(cut.FindAll(".library-rendered a"));
    }
}

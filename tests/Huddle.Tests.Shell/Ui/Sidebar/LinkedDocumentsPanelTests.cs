using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="LinkedDocumentsPanel"/>: the note's outgoing wikilinks, resolved or not (Spec <i>Linked documents</i>, D-8).</summary>
public sealed class LinkedDocumentsPanelTests
{
    private const string UnavailableText = "Links aren't available for this folder: it has too many files.";

    /// <summary>Renders the panel for <paramref name="note"/> over <paramref name="fixture"/>'s resolver and index.</summary>
    private static IRenderedComponent<CascadingValue<SidebarPanelContext>> Render(
        MudBunitContext ctx,
        LibraryFileServiceFixture fixture,
        SidebarPanelContext panelCtx,
        LibraryPath note,
        string? text = null)
    {
        ctx.Services.AddSingleton(fixture.Resolver);
        ctx.Services.AddSingleton(fixture.Index);
        DocumentSubject subject = new(note, LibraryFileKind.Markdown, text);
        return ctx.Render<CascadingValue<SidebarPanelContext>>(p => p
            .Add(c => c.Value, panelCtx)
            .Add(c => c.IsFixed, true)
            .AddChildContent<LinkedDocumentsPanel>(s => s
                .Add(c => c.Subject, subject)
                .Add(c => c.TabId, "t1")));
    }

    /// <summary>Creates a pinned root holding <c>a.md</c> with the given body and the listed other notes.</summary>
    private static (string Root, LibraryPath Note) Seed(LibraryFileServiceFixture fixture, string body, params string[] others)
    {
        string root = fixture.CreatePinnedRoot("root");
        foreach (string other in others)
        {
            File.WriteAllText(Path.Combine(root, other), "# " + other + "\n");
        }

        File.WriteAllText(Path.Combine(root, "a.md"), body);
        fixture.Reload();
        return (root, fixture.Resolve(root, "a.md"));
    }

    /// <summary>A link with an alias is labelled by the alias; one without is labelled by the target file's name without <c>.md</c>.</summary>
    [Fact]
    public async Task Linked_ListsLinksWithAliasOrName()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (_, LibraryPath note) = Seed(fixture, "[[b|Bee]] and [[c]]\n", "b.md", "c.md");
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, panelCtx, note);

        cut.WaitForAssertion(() => Assert.Equal(["Bee", "c"], cut.FindAll(".linked-row").Select(static r => r.TextContent.Trim())));
        Assert.Equal(2, panelCtx.Count);
    }

    /// <summary>A link to a note that does not exist is plain secondary text with a "(not found)" suffix, not a link.</summary>
    [Fact]
    public async Task Linked_Unresolved_IsDisabledAndSaysNotFound()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (_, LibraryPath note) = Seed(fixture, "[[ghost]]\n");

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), note);

        cut.WaitForAssertion(() => Assert.Equal("ghost (not found)", cut.Find(".linked-row").TextContent.Trim()));
        Assert.Empty(cut.FindAll(".linked-row a"));
    }

    /// <summary>A resolved row is a link to the target note's <c>/library?root=...&amp;path=...</c> page.</summary>
    [Fact]
    public async Task Linked_Click_NavigatesToTheLibraryPage()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (string root, LibraryPath note) = Seed(fixture, "[[b]]\n", "b.md");
        LibraryPath target = fixture.Resolve(root, "b.md");

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), note);

        cut.WaitForAssertion(() => Assert.Equal(LibraryLinkQuery.BuildPageUri(target), cut.Find(".linked-row a").GetAttribute("href")));
    }

    /// <summary>A note with no wikilinks shows the empty line and reports a count of zero.</summary>
    [Fact]
    public async Task Linked_None_ShowsEmptyLine()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (_, LibraryPath note) = Seed(fixture, "no links here\n");
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, panelCtx, note);

        cut.WaitForAssertion(() => Assert.Equal("This note links to no other notes.", cut.Find("[role=status]").TextContent.Trim()));
        Assert.Equal(0, panelCtx.Count);
    }

    /// <summary>A root with too many files to index says so and reports no count.</summary>
    [Fact]
    public async Task Linked_Unavailable_SaysSo()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(o => o.Library.MaxIndexedFiles = 1);
        (_, LibraryPath note) = Seed(fixture, "[[b]]\n", "b.md", "c.md");
        SidebarPanelContext panelCtx = new();

        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, panelCtx, note);

        cut.WaitForAssertion(() => Assert.Equal(UnavailableText, cut.Find(".mud-text-secondary").TextContent.Trim()));
        Assert.Null(panelCtx.Count);
    }

    /// <summary>An invalidation of the note's own root reloads the list from the changed file.</summary>
    [Fact]
    public async Task Linked_InvalidatedForItsRoot_Reloads()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (string root, LibraryPath note) = Seed(fixture, "[[b]]\n", "b.md", "c.md");
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), note);
        cut.WaitForAssertion(() => Assert.Equal(["b"], cut.FindAll(".linked-row").Select(static r => r.TextContent.Trim())));

        File.WriteAllText(Path.Combine(root, "a.md"), "[[b]] [[c]]\n");
        fixture.Index.Invalidate(note.Root.Id);

        cut.WaitForAssertion(() => Assert.Equal(["b", "c"], cut.FindAll(".linked-row").Select(static r => r.TextContent.Trim())));
    }

    /// <summary>An invalidation of a different root leaves the list as it was.</summary>
    [Fact]
    public async Task Linked_InvalidatedForAnotherRoot_DoesNotReload()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (string root, LibraryPath note) = Seed(fixture, "[[b]]\n", "b.md", "c.md");
        fixture.CreatePinnedRoot("other");
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), note);
        cut.WaitForAssertion(() => Assert.Equal(["b"], cut.FindAll(".linked-row").Select(static r => r.TextContent.Trim())));

        IRenderedComponent<LinkedDocumentsPanel> panel = cut.FindComponent<LinkedDocumentsPanel>();
        FieldInfo? loadField = typeof(LinkedDocumentsPanel).GetField("load", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(loadField);
        object? latestLoad = loadField.GetValue(panel.Instance);
        Assert.NotNull(latestLoad);
        FieldInfo? loadSource = typeof(LatestLoad).GetField("source", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(loadSource);
        object? sourceBefore = loadSource.GetValue(latestLoad);

        // Any load, however short, replaces the panel's cancellation source, and the dispatcher runs the
        // handler's work in order, so after this flush a reload for "other" would already have started.
        fixture.Index.Invalidate("other");
        await panel.InvokeAsync(static () => { });
        Assert.Same(sourceBefore, loadSource.GetValue(latestLoad));

        File.WriteAllText(Path.Combine(root, "a.md"), "[[b]] [[c]]\n");
        fixture.Index.Invalidate(note.Root.Id);

        cut.WaitForAssertion(() => Assert.Equal(["b", "c"], cut.FindAll(".linked-row").Select(static r => r.TextContent.Trim())));
        Assert.NotSame(sourceBefore, loadSource.GetValue(latestLoad));
    }

    /// <summary>Disposing the panel takes its handler off <c>WikiLinkIndex.Invalidated</c>.</summary>
    [Fact]
    public async Task Linked_Disposed_Unsubscribes()
    {
        await using MudBunitContext ctx = new();
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        (_, LibraryPath note) = Seed(fixture, "[[b]]\n", "b.md");
        FieldInfo? field = typeof(WikiLinkIndex).GetField(nameof(WikiLinkIndex.Invalidated), BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        IRenderedComponent<CascadingValue<SidebarPanelContext>> cut = Render(ctx, fixture, new SidebarPanelContext(), note);
        cut.WaitForAssertion(() => Assert.NotNull(field.GetValue(fixture.Index)));

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Null(field.GetValue(fixture.Index));
    }
}

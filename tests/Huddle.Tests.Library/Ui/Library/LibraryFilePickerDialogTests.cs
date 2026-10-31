using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>The Library file picker (TaskAttachments spec 6.3): search by name, browse, pick a file, never a folder.</summary>
public sealed class LibraryFilePickerDialogTests
{
    private const string TeamsRoot = "Teams";

    /// <summary>With the box empty the Human browses: the Roots first, then a folder; a hidden folder is not listed.</summary>
    [Fact]
    public async Task Browse_OpensRootThenFolder_AndHidesUnderscoreFolders()
    {
        using LibraryFileServiceFixture library = Seed();
        await using MudBunitContext ctx = NewContext(library);
        (IRenderedComponent<MudDialogProvider> provider, _) = await ShowAsync(ctx);

        await provider.InvokeAsync(() => Row(provider, TeamsRoot).ClickAsync());
        await provider.InvokeAsync(() => Row(provider, "Growth").ClickAsync());

        provider.WaitForAssertion(() =>
        {
            Assert.NotNull(Row(provider, "plan.md"));
            Assert.DoesNotContain(provider.FindAll(".picker-row"), row => row.TextContent.Contains("_tasks", StringComparison.Ordinal));
        });
    }

    /// <summary>Clicking a folder only opens it: the dialog stays open and returns nothing.</summary>
    [Fact]
    public async Task Browse_ClickingAFolder_DoesNotCloseTheDialog()
    {
        using LibraryFileServiceFixture library = Seed();
        await using MudBunitContext ctx = NewContext(library);
        (IRenderedComponent<MudDialogProvider> provider, IDialogReference reference) = await ShowAsync(ctx);

        await provider.InvokeAsync(() => Row(provider, TeamsRoot).ClickAsync());

        provider.WaitForAssertion(() => Assert.NotNull(Row(provider, "Growth")));
        Assert.False(reference.Result.IsCompleted);
    }

    /// <summary>Typing a name lists matching files with their path, and picking one closes the dialog with its LibraryPath.</summary>
    [Fact]
    public async Task Search_PickingAMatch_ReturnsItsPath()
    {
        using LibraryFileServiceFixture library = Seed();
        await using MudBunitContext ctx = NewContext(library);
        (IRenderedComponent<MudDialogProvider> provider, IDialogReference reference) = await ShowAsync(ctx);

        await provider.InvokeAsync(() => provider.Find("input").InputAsync(new ChangeEventArgs { Value = "pla" }));
        provider.WaitForAssertion(() => Assert.NotNull(Row(provider, "plan.md")), TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(provider.FindAll(".picker-row"), row => row.TextContent.Contains("notes.md", StringComparison.Ordinal));
        await provider.InvokeAsync(() => Row(provider, "plan.md").ClickAsync());

        DialogResult? result = await reference.Result;
        LibraryPath picked = Assert.IsType<LibraryPath>(result?.Data);
        Assert.Equal(Path.Combine(library.DataDir, "Teams", "Growth", "plan.md"), picked.FullPath);
    }

    /// <summary>The Up row goes back to the folder above, and from a Root back to the list of Roots.</summary>
    [Fact]
    public async Task Browse_Up_GoesBackToTheParentThenToTheRoots()
    {
        using LibraryFileServiceFixture library = Seed();
        await using MudBunitContext ctx = NewContext(library);
        (IRenderedComponent<MudDialogProvider> provider, _) = await ShowAsync(ctx);
        await provider.InvokeAsync(() => Row(provider, TeamsRoot).ClickAsync());
        await provider.InvokeAsync(() => Row(provider, "Growth").ClickAsync());
        provider.WaitForAssertion(() => Assert.NotNull(Row(provider, "plan.md")));

        await provider.InvokeAsync(() => Row(provider, "Up").ClickAsync());
        provider.WaitForAssertion(() => Assert.NotNull(Row(provider, "Growth")));
        await provider.InvokeAsync(() => Row(provider, "Up").ClickAsync());

        provider.WaitForAssertion(() =>
        {
            Assert.NotNull(Row(provider, TeamsRoot));
            Assert.DoesNotContain(provider.FindAll(".picker-row"), row => string.Equals(row.TextContent.Trim(), "Up", StringComparison.Ordinal));
        });
    }

    /// <summary>Folders whose names match the search do not use up the 20-hit budget: a matching file past more than 20 matching folders is still listed.</summary>
    [Fact]
    public async Task Search_ManyMatchingFolders_StillListsTheMatchingFile()
    {
        using LibraryFileServiceFixture library = Seed();
        for (int index = 0; index < 25; index++)
        {
            Directory.CreateDirectory(Path.Combine(library.DataDir, "Teams", $"plan-folder-{index:00}"));
        }

        await using MudBunitContext ctx = NewContext(library);
        (IRenderedComponent<MudDialogProvider> provider, _) = await ShowAsync(ctx);

        await provider.InvokeAsync(() => provider.Find("input").InputAsync(new ChangeEventArgs { Value = "plan" }));

        provider.WaitForAssertion(() => Assert.NotNull(Row(provider, "plan.md")), TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(provider.FindAll(".picker-row"), row => row.TextContent.Contains("plan-folder", StringComparison.Ordinal));
    }

    /// <summary>A search hit shows its relative path as dimmed text under the file name.</summary>
    [Fact]
    public async Task Search_Hit_ShowsTheRelativePathAsDimmedText()
    {
        using LibraryFileServiceFixture library = Seed();
        await using MudBunitContext ctx = NewContext(library);
        (IRenderedComponent<MudDialogProvider> provider, _) = await ShowAsync(ctx);

        await provider.InvokeAsync(() => provider.Find("input").InputAsync(new ChangeEventArgs { Value = "pla" }));

        provider.WaitForAssertion(() => Assert.Equal("Teams/Growth/plan.md", provider.Find(".picker-row .picker-row-path").TextContent.Trim()), TimeSpan.FromSeconds(5));
    }

    private static LibraryFileServiceFixture Seed()
    {
        LibraryFileServiceFixture library = LibraryFileServiceFixture.Build();
        string growth = Path.Combine(library.DataDir, "Teams", "Growth");
        Directory.CreateDirectory(Path.Combine(growth, "_tasks"));
        File.WriteAllText(Path.Combine(growth, "plan.md"), "plan");
        File.WriteAllText(Path.Combine(growth, "notes.md"), "notes");
        return library;
    }

    private static MudBunitContext NewContext(LibraryFileServiceFixture library)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(library.RootStore);
        ctx.Services.AddSingleton(library.Resolver);
        ctx.Services.AddSingleton(library.CreateService());
        return ctx;
    }

    private static async Task<(IRenderedComponent<MudDialogProvider> Provider, IDialogReference Reference)> ShowAsync(MudBunitContext ctx)
    {
        IRenderedComponent<MudDialogProvider> provider = ctx.Render<MudDialogProvider>();
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        IDialogReference reference = await provider.InvokeAsync(() => dialogs.ShowAsync<LibraryFilePickerDialog>("Link a Library file", LibraryFilePickerDialog.Options));
        provider.WaitForAssertion(() => Assert.NotEmpty(provider.FindAll(".picker-row")));
        return (provider, reference);
    }

    private static AngleSharp.Dom.IElement Row(IRenderedComponent<MudDialogProvider> provider, string text) =>
        provider.FindAll(".picker-row").First(row => row.TextContent.Contains(text, StringComparison.Ordinal));
}

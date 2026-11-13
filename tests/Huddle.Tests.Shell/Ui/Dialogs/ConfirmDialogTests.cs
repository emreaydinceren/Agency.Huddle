using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Shared.Dialogs;

namespace Agency.Huddle.Tests.Ui.Dialogs;

/// <summary>
/// Pins the <see cref="ConfirmDialog"/> contract and the <c>ConfirmAsync</c> extension (design-system
/// "Confirming", UX-026, UX-R12): a title that is a question ending in "?", one sentence of body,
/// Cancel then a Filled, Small button repeating the verb, <c>Color.Error</c> when the confirm destroys,
/// and a <see cref="bool"/> answer.
/// </summary>
public sealed class ConfirmDialogTests
{
    private const string Actions = ".mud-dialog-actions button";

    /// <summary>The title is the caller's question, ending in "?", and the body is the one sentence passed.</summary>
    [Fact]
    public async Task Shows_QuestionTitle_AndOneSentenceBody()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "Delete 'Notes'?", "The note and its links are lost.", "Delete", true);

        string title = cut.Find(".mud-dialog-title").TextContent.Trim();
        Assert.Equal("Delete 'Notes'?", title);
        Assert.True(title.EndsWith('?'));
        Assert.Equal("The note and its links are lost.", cut.Find(".mud-dialog-content").TextContent.Trim());
    }

    /// <summary>Cancel comes first as a Small Text button, then the Filled Small confirm repeating the verb.</summary>
    [Fact]
    public async Task Actions_AreCancelThenTheVerb()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "Reset 'Nova'?", "Its settings return to the defaults.", "Reset", false);

        IReadOnlyList<IElement> buttons = cut.FindAll(Actions);
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Cancel", buttons[0].TextContent.Trim());
        Assert.Contains("mud-button-text-size-small", buttons[0].ClassList);
        Assert.Equal("Reset", buttons[1].TextContent.Trim());
        Assert.Contains("mud-button-filled", buttons[1].ClassList);
        Assert.Contains("mud-button-filled-size-small", buttons[1].ClassList);
    }

    /// <summary>A dangerous confirm is <c>Color.Error</c>.</summary>
    [Fact]
    public async Task Danger_MakesTheConfirmError()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "Delete 'Notes'?", "The note is lost.", "Delete", true);

        IElement confirm = cut.FindAll(Actions)[1];
        Assert.Contains("mud-button-filled-error", confirm.ClassList);
        Assert.DoesNotContain("mud-button-filled-primary", confirm.ClassList);
    }

    /// <summary>A non-dangerous confirm is <c>Color.Primary</c>.</summary>
    [Fact]
    public async Task NotDanger_MakesTheConfirmPrimary()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "Reset 'Nova'?", "Its settings return to the defaults.", "Reset", false);

        IElement confirm = cut.FindAll(Actions)[1];
        Assert.Contains("mud-button-filled-primary", confirm.ClassList);
        Assert.DoesNotContain("mud-button-filled-error", confirm.ClassList);
    }

    /// <summary>The confirm button closes the dialog with an OK result.</summary>
    [Fact]
    public async Task Confirm_ClosesWithOk()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "Delete 'Notes'?", "The note is lost.", "Delete", true);

        await cut.InvokeAsync(() => cut.FindAll(Actions)[1].ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
    }

    /// <summary>Cancel closes the dialog with a cancelled result.</summary>
    [Fact]
    public async Task Cancel_ReturnsCancelled()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "Delete 'Notes'?", "The note is lost.", "Delete", true);

        await cut.InvokeAsync(() => cut.FindAll(Actions)[0].ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.True(result.Canceled);
    }

    /// <summary><c>ConfirmAsync</c> answers <see langword="true"/> when the confirm button is pressed.</summary>
    [Fact]
    public async Task ConfirmAsync_ConfirmPressed_ReturnsTrue()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();

        Task<bool> answer = cut.InvokeAsync(() => dialogService.ConfirmAsync("Delete 'Notes'?", "The note is lost.", "Delete", true, ct));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.FindAll(Actions)[1].ClickAsync());

        Assert.True(await answer.WaitAsync(TimeSpan.FromSeconds(10), ct));
    }

    /// <summary><c>ConfirmAsync</c> answers <see langword="false"/> when the user cancels.</summary>
    [Fact]
    public async Task ConfirmAsync_CancelPressed_ReturnsFalse()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();

        Task<bool> answer = cut.InvokeAsync(() => dialogService.ConfirmAsync("Delete 'Notes'?", "The note is lost.", "Delete", true, ct));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        await cut.InvokeAsync(() => cut.FindAll(Actions)[0].ClickAsync());

        Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(10), ct));
    }

    /// <summary>Points render as one list item each, in order, with the lead in bold and the text straight after it, under the message.</summary>
    [Fact]
    public async Task Points_RenderEachLeadBoldWithItsText_InOrder()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IReadOnlyList<ConfirmPoint> points = [new("First lead.", " First text."), new("Second lead,", " second text."), new("Third lead", " third text.")];

        _ = await OpenWithPointsAsync(ctx, cut, "Rename A to B?", "Things can go wrong:", points);

        IReadOnlyList<IElement> items = cut.FindAll(".mud-dialog-content li");
        Assert.Equal(["First lead. First text.", "Second lead, second text.", "Third lead third text."], [.. items.Select(static item => item.TextContent.Trim())]);
        Assert.Equal(["First lead.", "Second lead,", "Third lead"], [.. items.Select(static item => item.QuerySelector("strong, b")?.TextContent)]);
        Assert.Equal("Things can go wrong:", cut.Find(".mud-dialog-content p").TextContent.Trim());
    }

    /// <summary>With no points, or an empty list, the dialog draws no list: its content is the message alone, as before.</summary>
    [Fact]
    public async Task NoPoints_DrawsNoList()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenWithPointsAsync(ctx, cut, "Delete 'Notes'?", "The note is lost.", []);

        Assert.Empty(cut.FindAll(".mud-dialog-content li"));
        Assert.Equal("The note is lost.", cut.Find(".mud-dialog-content").TextContent.Trim());
    }

    /// <summary><c>ConfirmAsync</c> passes its points on to the dialog.</summary>
    [Fact]
    public async Task ConfirmAsync_WithPoints_ShowsThem()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();

        Task<bool> answer = cut.InvokeAsync(() => dialogService.ConfirmAsync("Rename A to B?", "Careful:", [new ConfirmPoint("Lead.", " Text.")], "Rename", true, ct));
        cut.WaitForAssertion(() => Assert.Equal("Lead. Text.", Assert.Single(cut.FindAll(".mud-dialog-content li")).TextContent.Trim()));
        await cut.InvokeAsync(() => cut.FindAll(Actions)[0].ClickAsync());

        Assert.False(await answer.WaitAsync(TimeSpan.FromSeconds(10), ct));
    }

    /// <summary>Shows a <see cref="ConfirmDialog"/> with <paramref name="points"/> and waits until it is on screen.</summary>
    private static async Task<IDialogReference> OpenWithPointsAsync(MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, string title, string message, IReadOnlyList<ConfirmPoint> points)
    {
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { x => x.Message, message },
            { x => x.ConfirmText, "Rename" },
            { x => x.Danger, true },
            { x => x.Points, points },
        };

        IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<ConfirmDialog>(title, parameters));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        return reference;
    }

    /// <summary>Shows a <see cref="ConfirmDialog"/> through <see cref="IDialogService"/> and waits until it is on screen.</summary>
    private static async Task<IDialogReference> OpenAsync(MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, string title, string message, string confirmText, bool danger)
    {
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { x => x.Message, message },
            { x => x.ConfirmText, confirmText },
            { x => x.Danger, danger },
        };

        IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<ConfirmDialog>(title, parameters));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        return reference;
    }
}

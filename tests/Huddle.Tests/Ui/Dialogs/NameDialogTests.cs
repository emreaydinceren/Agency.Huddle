using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Agency.Huddle.App.Components.Shared.Dialogs;

namespace Agency.Huddle.Tests.Ui.Dialogs;

/// <summary>
/// Pins the <see cref="NameDialog"/> contract (design-system "Dialog anatomy", UX-024, UX-025): a
/// labelled, pre-filled, auto-focused field; Enter submits; the feature's validator drives
/// <c>Error</c> and disables the primary; an error returned by the action shows as a Dense
/// <c>MudAlert role="alert"</c> and keeps the dialog open; the actions are Cancel (Text) then a
/// Filled, Small primary.
/// </summary>
public sealed class NameDialogTests
{
    private const string Input = ".mud-dialog-content input";

    private const string Actions = ".mud-dialog-actions button";

    /// <summary>The field shows the label and the initial value the caller passed.</summary>
    [Fact]
    public async Task Field_ShowsLabelAndInitialValue()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "Rename note", "Name", "Create", "Notes.md", null, static _ => Task.FromResult<string?>(null));

        Assert.Equal("Name", cut.Find(".mud-dialog-content label").TextContent.Trim());
        Assert.Equal("Notes.md", cut.Find(Input).GetAttribute("value"));
    }

    /// <summary>The field asks for focus when the dialog opens (UX-024).</summary>
    [Fact]
    public async Task Field_RequestsFocusOnOpen()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "New team", "Team name", "Create", string.Empty, null, static _ => Task.FromResult<string?>(null));

        Assert.Single(cut.FindAll(Input));
        cut.WaitForAssertion(() => Assert.Equal(
            2,
            ctx.JSInterop.Invocations.Count(static i => string.Equals(i.Identifier, "Blazor._internal.domWrapper.focus", StringComparison.Ordinal))));
    }

    /// <summary>Cancel comes first as a Text button, then the primary as a Filled, Small, Primary button with the caller's verb.</summary>
    [Fact]
    public async Task Actions_AreCancelThenFilledSmallPrimary()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "New team", "Team name", "Create", "Research", null, static _ => Task.FromResult<string?>(null));

        IReadOnlyList<IElement> buttons = cut.FindAll(Actions);
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Cancel", buttons[0].TextContent.Trim());
        Assert.Contains("mud-button-text", buttons[0].ClassList);
        Assert.Contains("mud-button-text-size-small", buttons[0].ClassList);
        Assert.Equal("Create", buttons[1].TextContent.Trim());
        Assert.Contains("mud-button-filled", buttons[1].ClassList);
        Assert.Contains("mud-button-filled-primary", buttons[1].ClassList);
        Assert.Contains("mud-button-filled-size-small", buttons[1].ClassList);
    }

    /// <summary>A name the validator refuses shows its text on the field and disables the primary.</summary>
    [Fact]
    public async Task Validate_Refusal_ShowsErrorAndDisablesPrimary()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New team", "Team name", "Create", string.Empty, static text => text == "Sales" ? "A Team named \"Sales\" already exists." : null, static _ => Task.FromResult<string?>(null));

        await TypeAsync(cut, "Sales");

        Assert.Equal("A Team named \"Sales\" already exists.", cut.Find(".mud-input-helper-text").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".mud-input-error"));
        Assert.True(cut.FindAll(Actions)[1].HasAttribute("disabled"));
    }

    /// <summary>A name the validator accepts clears the error and enables the primary.</summary>
    [Fact]
    public async Task Validate_Acceptance_EnablesPrimaryWithNoError()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        _ = await OpenAsync(ctx, cut, "New team", "Team name", "Create", string.Empty, static text => text == "Sales" ? "taken" : null, static _ => Task.FromResult<string?>(null));
        await TypeAsync(cut, "Sales");

        await TypeAsync(cut, "Research");

        Assert.Empty(cut.FindAll(".mud-input-error"));
        Assert.False(cut.FindAll(Actions)[1].HasAttribute("disabled"));
    }

    /// <summary>Enter submits the trimmed text and the dialog closes with an OK result.</summary>
    [Fact]
    public async Task Enter_Submits_AndClosesWithTheTrimmedText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        List<string> submitted = [];
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", "Team name", "Create", string.Empty, null, text =>
        {
            submitted.Add(text);
            return Task.FromResult<string?>(null);
        });
        await TypeAsync(cut, "  Research  ");

        await cut.InvokeAsync(() => cut.Find(Input).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
        Assert.Equal("Research", Assert.Single(submitted));
    }

    /// <summary>Enter on a refused name submits nothing and leaves the dialog open.</summary>
    [Fact]
    public async Task Enter_WhenInvalid_DoesNotSubmit()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        int calls = 0;
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", "Team name", "Create", string.Empty, static _ => "nope", _ =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        });
        await TypeAsync(cut, "Sales");

        await cut.InvokeAsync(() => cut.Find(Input).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Equal(0, calls);
        Assert.False(reference.Result.IsCompleted);
    }

    /// <summary>An error returned by the action shows as a Dense error alert with role "alert" and the dialog stays open.</summary>
    [Fact]
    public async Task SubmitError_ShowsDenseAlert_AndStaysOpen()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "Rename note", "Name", "Rename", string.Empty, null, static _ => Task.FromResult<string?>("That name is taken."));
        await TypeAsync(cut, "Notes");

        await cut.InvokeAsync(() => cut.FindAll(Actions)[1].ClickAsync());

        IElement alert = cut.Find(".mud-dialog .mud-alert");
        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Contains("mud-dense", alert.ClassList);
        Assert.Contains("mud-alert-text-error", alert.ClassList);
        Assert.Equal("That name is taken.", alert.TextContent.Trim());
        Assert.False(reference.Result.IsCompleted);
    }

    /// <summary>No alert is present until the action returns an error.</summary>
    [Fact]
    public async Task NoAlert_BeforeAnySubmit()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });

        _ = await OpenAsync(ctx, cut, "New team", "Team name", "Create", "Research", null, static _ => Task.FromResult<string?>(null));

        Assert.Empty(cut.FindAll(".mud-dialog .mud-alert"));
    }

    /// <summary>The primary button submits and a successful action closes the dialog with an OK result.</summary>
    [Fact]
    public async Task Primary_Success_ClosesWithOk()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", "Team name", "Create", "Research", null, static _ => Task.FromResult<string?>(null));

        await cut.InvokeAsync(() => cut.FindAll(Actions)[1].ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.False(result.Canceled);
    }

    /// <summary>Cancel closes the dialog with a cancelled result and never calls the action.</summary>
    [Fact]
    public async Task Cancel_ReturnsCancelled_WithoutSubmitting()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        int calls = 0;
        IDialogReference reference = await OpenAsync(ctx, cut, "New team", "Team name", "Create", "Research", null, _ =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        });

        await cut.InvokeAsync(() => cut.FindAll(Actions)[0].ClickAsync());

        DialogResult? result = await reference.Result.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.NotNull(result);
        Assert.True(result.Canceled);
        Assert.Equal(0, calls);
    }

    /// <summary>Optional child content renders inside the dialog body.</summary>
    [Fact]
    public async Task ChildContent_RendersInTheBody()
    {
        await using MudBunitContext ctx = new();
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(static _ => { });
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<NameDialog> parameters = new()
        {
            { x => x.Label, "Name" },
            { x => x.ConfirmText, "Create" },
            { x => x.SubmitAsync, static _ => Task.FromResult<string?>(null) },
            { x => x.ChildContent, (RenderFragment)(static b => b.AddMarkupContent(0, "<p class=\"extra\">Extra note</p>")) },
        };

        _ = await cut.InvokeAsync(() => dialogService.ShowAsync<NameDialog>("New team", parameters));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));

        Assert.Equal("Extra note", cut.Find(".mud-dialog-content .extra").TextContent);
    }

    /// <summary>Types into the field the way a user does, so each keystroke re-validates.</summary>
    private static async Task TypeAsync(IRenderedComponent<ContainerFragment> cut, string text)
        => await cut.InvokeAsync(() => cut.Find(Input).InputAsync(new ChangeEventArgs { Value = text }));

    /// <summary>Shows a <see cref="NameDialog"/> through <see cref="IDialogService"/> and waits until it is on screen.</summary>
    private static async Task<IDialogReference> OpenAsync(
        MudBunitContext ctx,
        IRenderedComponent<ContainerFragment> cut,
        string title,
        string label,
        string confirmText,
        string initialValue,
        Func<string, string?>? validate,
        Func<string, Task<string?>> submit)
    {
        IDialogService dialogService = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<NameDialog> parameters = new()
        {
            { x => x.Label, label },
            { x => x.ConfirmText, confirmText },
            { x => x.InitialValue, initialValue },
            { x => x.Validate, validate },
            { x => x.SubmitAsync, submit },
        };

        IDialogReference reference = await cut.InvokeAsync(() => dialogService.ShowAsync<NameDialog>(title, parameters));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".mud-dialog-title")));
        return reference;
    }
}

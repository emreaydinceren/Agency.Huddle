using Agency.Huddle.App.Data;
using Agency.Huddle.App.References;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Components.Shared.RoomLinkCopier.CopyAsync"/>, the one copy-link path both entry points share (UX-R03):
/// it copies <see cref="RoomLinkText.For"/> through <c>copy</c> in <c>clipboard.js</c> and reports the spec's
/// wording through the snackbar under the <c>copy</c> key.
/// </summary>
public sealed class RoomLinkCopierTests
{
    /// <summary>The clipboard helper the copy calls.</summary>
    private const string CopyHelper = "copy";

    /// <summary>A copy that the clipboard accepts says so with the Room's name; one it refuses says it could not.</summary>
    /// <param name="copied">What the clipboard helper answers.</param>
    /// <param name="message">The snackbar text the helper must show.</param>
    /// <param name="severity">The snackbar severity the helper must use.</param>
    [Theory]
    [InlineData(true, "Copied link to Nova", Severity.Success)]
    [InlineData(false, "Couldn't copy the link.", Severity.Warning)]
    public async Task CopyAsync_ReportsTheOutcomeThroughTheSnackbar(bool copied, string message, Severity severity)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        Room room = new("room-1", "Nova", DateTimeOffset.UnixEpoch);
        string expectedText = RoomLinkText.For(room);
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Module(JsModule.Clipboard).Setup<bool>(RoomLinkCopierTests.CopyHelper, expectedText).SetResult(copied);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IJSRuntime js = ctx.Services.GetRequiredService<IJSRuntime>();

        await Agency.Huddle.App.Components.Shared.RoomLinkCopier.CopyAsync(js, snackbar, room, ct);

        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal(message, shown.Message);
        Assert.Equal(severity, shown.Severity);
    }

    /// <summary>The text put on the clipboard is the Room's reference, the same text the picker inserts.</summary>
    [Fact]
    public async Task CopyAsync_CopiesTheRoomReferenceText()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        Room room = new("room-2", "Marketing", DateTimeOffset.UnixEpoch);
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Module(JsModule.Clipboard).Setup<bool>(RoomLinkCopierTests.CopyHelper, $"[Marketing](/rooms/{room.Id})").SetResult(true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IJSRuntime js = ctx.Services.GetRequiredService<IJSRuntime>();

        await Agency.Huddle.App.Components.Shared.RoomLinkCopier.CopyAsync(js, snackbar, room, ct);

        JSRuntimeInvocation invocation = ctx.Module(JsModule.Clipboard).VerifyInvoke(RoomLinkCopierTests.CopyHelper);
        Assert.Equal($"[Marketing](/rooms/{room.Id})", Assert.Single(invocation.Arguments));
    }
}

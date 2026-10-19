using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

namespace Agency.Huddle.Tests.Ui.Tasks;

/// <summary>
/// Pins <see cref="TaskClipboard"/>, the one copy path every Task surface shares (UX-R04): Copy id says
/// "Copied {id}", Copy link says "Copied link to {id}", and each failure has its own warning, all under
/// the <c>copy</c> key.
/// </summary>
public sealed class TaskClipboardTests
{
    /// <summary>The clipboard helper the copy calls.</summary>
    private const string CopyHelper = "huddleClipboard.copy";

    /// <summary>The id copies as its own text and says "Copied {id}" or the Ctrl+C warning.</summary>
    /// <param name="copied">What the clipboard helper answers.</param>
    /// <param name="success">Whether the snackbar must be the success one.</param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CopyIdAsync_ReportsTheOutcome(bool copied, bool success)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        TaskId id = new("T", 7);
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Setup<bool>(CopyHelper, id.ToString()).SetResult(copied);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await TaskClipboard.CopyIdAsync(ctx.Services.GetRequiredService<IJSRuntime>(), snackbar, id, ct);

        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal(success ? $"Copied {id}" : "Couldn't copy. The id is selected; press Ctrl+C.", shown.Message);
        Assert.Equal(success ? Severity.Success : Severity.Warning, shown.Severity);
    }

    /// <summary>The link is the base address plus the item route, and says "Copied link to {id}" or that the link could not be copied.</summary>
    /// <param name="copied">What the clipboard helper answers.</param>
    /// <param name="success">Whether the snackbar must be the success one.</param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CopyLinkAsync_ReportsTheOutcome(bool copied, bool success)
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        TaskId id = new("T", 7);
        await using MudBunitContext ctx = new();
        ctx.JSInterop.Setup<bool>(CopyHelper, $"http://localhost:1111/tasks/item/{id}").SetResult(copied);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();

        await TaskClipboard.CopyLinkAsync(ctx.Services.GetRequiredService<IJSRuntime>(), snackbar, "http://localhost:1111/", id, ct);

        Snackbar shown = Assert.Single(snackbar.ShownSnackbars);
        Assert.Equal(success ? $"Copied link to {id}" : "Couldn't copy the link.", shown.Message);
        Assert.Equal(success ? Severity.Success : Severity.Warning, shown.Severity);
    }

    /// <summary>The detail's Copy link item goes through the shared path: the toast reads "Copied link to {id}", not "Copied {id}".</summary>
    [Fact]
    public async Task DetailCopyLink_SaysCopiedLinkTo()
    {
        using TaskToolHarness harness = new();
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = UiTestHost.Create().WithHarness(harness);
        string link = $"{ctx.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().BaseUri}tasks/item/{task.Id}";
        ctx.JSInterop.Setup<bool>(CopyHelper, link).SetResult(true);
        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        IRenderedComponent<ContainerFragment> cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);

        await cut.InvokeAsync(() => cut.Find($"button[aria-label=\"Actions for {task.Id}\"]").ClickAsync());
        IElement item = cut.FindAll(".mud-menu-item").Single(element => string.Equals(element.TextContent.Trim(), "Copy link", StringComparison.Ordinal));
        await cut.InvokeAsync(() => item.ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        Assert.Equal($"Copied link to {task.Id}", snackbar.ShownSnackbars.Single().Message);
    }
}

using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the Blazor side of the <see cref="Composer"/> that is not the Reference picker: the draft key the
/// script is attached with and the first typing that keeps the Room's Tab. The picker (the <c>@</c> and
/// <c>#</c> queries, the keys, the combobox roles) is pinned by <c>ComposerReferencePickerTests</c>; the
/// <c>#</c> picker's former tests there keep their asserted text (<c>No task matches '#zzz'</c>).
/// </summary>
public sealed class ComposerTests
{
    /// <summary>The script is attached with a draft key naming the Room, so unsent text follows its Room across Tabs.</summary>
    [Fact]
    public async Task Attach_PassesTheRoomsDraftKey()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = ComposerTests.NewContext(harness);

        _ = ComposerTests.RenderComposer(ctx);

        var invocation = ctx.JSInterop.VerifyInvoke("teamComposer.attach");
        Assert.Equal("huddle:draft:any-room", invocation.Arguments[2]);
    }

    /// <summary>The first typing in a Composer makes the Room's Tab permanent.</summary>
    [Fact]
    public async Task TypedAsync_PinsTheRoomsTab()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = ComposerTests.NewContext(harness);
        var tabs = new Agency.Huddle.App.Tabs.TabService();
        ctx.Services.AddSingleton(tabs);
        tabs.Open("/rooms/any-room");
        IRenderedComponent<ContainerFragment> cut = ComposerTests.RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.TypedAsync());

        Assert.False(Assert.Single(tabs.Tabs).IsPreview);
    }

    /// <summary>Renders a bare <see cref="Composer"/> the same way <c>ChatPageTests.Composer_StatusLine_DoesNotFollowARoomSwitch</c> does, through <see cref="MudBunitContext.RenderWithPopovers"/> so a floating list renders in the same subtree (corrections-B6 "D15.3" item 5).</summary>
    private static IRenderedComponent<ContainerFragment> RenderComposer(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Composer>(0);
            builder.AddAttribute(1, nameof(Composer.RoomId), "any-room");
            builder.CloseComponent();
        });

    /// <summary>
    /// A fresh <see cref="MudBunitContext"/> with the harness's <c>ChatService</c> registered - the bare
    /// <c>Composer</c> context corrections-B6 "D15.1" item 1 describes.
    /// </summary>
    /// <param name="harness">Supplies <c>ChatService</c> and <c>TaskStore</c> (Composer's dependencies).</param>
    private static MudBunitContext NewContext(TaskToolHarness harness)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(harness.Chat);
        ctx.Services.AddSingleton(harness.Store);
        ctx.Services.AddSingleton(harness.Events);
        TeamOptions options = new() { Tasks = new TasksOptions { Enabled = true } };
        ctx.Services.AddSingleton<IOptions<TeamOptions>>(Options.Create(options));
        return ctx;
    }
}

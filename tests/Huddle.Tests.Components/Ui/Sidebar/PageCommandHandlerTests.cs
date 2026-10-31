using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins the page-side handler of Spec D-6: it hears its own Tab's commands and nothing after unmount.</summary>
public sealed class PageCommandHandlerTests
{
    /// <summary>A command sent to the cascaded Tab reaches the page's OnCommand callback.</summary>
    [Fact]
    public async Task Handler_ReceivesItsTabsCommand()
    {
        await using MudBunitContext ctx = new();
        PageCommandBus bus = AddBus(ctx);
        List<PageCommand> received = [];
        ctx.Render<PageCommandHandler>(parameters => parameters
            .AddCascadingValue(new TabPageContext("/x", "t1"))
            .Add(handler => handler.OnCommand, command => received.Add(command)));
        ScrollToHeading sent = new("h", 3);

        await bus.SendAsync("t1", sent);
        await bus.SendAsync("t2", new ScrollToHeading("other", 9));

        Assert.Equal([sent], received);
    }

    /// <summary>Disposing the component removes its subscription, so later commands are lost by design.</summary>
    [Fact]
    public async Task Handler_Disposed_Unsubscribes()
    {
        await using MudBunitContext ctx = new();
        PageCommandBus bus = AddBus(ctx);
        List<PageCommand> received = [];
        IRenderedComponent<PageCommandHandler> cut = ctx.Render<PageCommandHandler>(parameters => parameters
            .AddCascadingValue(new TabPageContext("/x", "t1"))
            .Add(handler => handler.OnCommand, command => received.Add(command)));

        await ctx.DisposeComponentsOnDispatcherAsync();
        await bus.SendAsync("t1", new ScrollToHeading("h", 3));

        Assert.Empty(received);
        Assert.NotNull(cut);
    }

    /// <summary>Outside a Tab there is no address to listen on, so nothing is received.</summary>
    [Fact]
    public async Task Handler_NoTab_ReceivesNothing()
    {
        await using MudBunitContext ctx = new();
        PageCommandBus bus = AddBus(ctx);
        List<PageCommand> received = [];
        ctx.Render<PageCommandHandler>(parameters => parameters
            .Add(handler => handler.OnCommand, command => received.Add(command)));

        await bus.SendAsync("t1", new ScrollToHeading("h", 3));

        Assert.Empty(received);
    }

    /// <summary>Returns the bus the context constructor already registered as a singleton.</summary>
    private static PageCommandBus AddBus(MudBunitContext ctx)
    {
        return ctx.Services.GetRequiredService<PageCommandBus>();
    }
}

using Agency.Huddle.App.Sidebar;

namespace Agency.Huddle.Tests.Sidebar;

/// <summary>Pins the Page command bus of Spec D-6: a command reaches only its Tab and is never stored.</summary>
public sealed class PageCommandBusTests
{
    /// <summary>A command nobody handles is a no-op, never an error.</summary>
    [Fact]
    public async Task SendAsync_NoHandler_DoesNothing()
    {
        PageCommandBus bus = new();

        Exception? thrown = await Record.ExceptionAsync(() => bus.SendAsync("t1", new ScrollToHeading("h", 3)));

        Assert.Null(thrown);
    }

    /// <summary>A command is delivered to the handlers of the addressed Tab and to no other Tab's.</summary>
    [Fact]
    public async Task SendAsync_ReachesOnlyItsTab()
    {
        PageCommandBus bus = new();
        List<string> received = [];
        using IDisposable first = bus.Subscribe("t1", command => Note(received,"t1", command));
        using IDisposable second = bus.Subscribe("t2", command => Note(received,"t2", command));

        await bus.SendAsync("t1", new ScrollToHeading("h", 3));

        Assert.Equal(["t1:h"], received);
    }

    /// <summary>Disposing a subscription stops its handler receiving commands.</summary>
    [Fact]
    public async Task Subscription_Disposed_StopsReceiving()
    {
        PageCommandBus bus = new();
        List<string> received = [];
        IDisposable subscription = bus.Subscribe("t1", command => Note(received,"t1", command));
        await bus.SendAsync("t1", new ScrollToHeading("a", 1));

        subscription.Dispose();
        await bus.SendAsync("t1", new ScrollToHeading("b", 2));

        Assert.Equal(["t1:a"], received);
    }

    /// <summary>Notes a delivered scroll command as "tab:heading".</summary>
    private static Task Note(List<string> received, string tab, PageCommand command)
    {
        received.Add($"{tab}:{((ScrollToHeading)command).HeadingId}");
        return Task.CompletedTask;
    }
}

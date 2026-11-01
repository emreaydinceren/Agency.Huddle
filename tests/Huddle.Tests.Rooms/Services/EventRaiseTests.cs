using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests.Services;

public sealed class EventRaiseTests
{
    /// <summary>A handler that throws is reported once and does not stop the handler after it.</summary>
    [Fact]
    public void Isolated_FirstHandlerThrows_SecondStillRunsAndErrorReportedOnce()
    {
        InvalidOperationException boom = new("boom");
        bool secondRan = false;
        List<Exception> reported = [];
        Action? handlers = null;
        handlers += () => throw boom;
        handlers += () => secondRan = true;

        EventRaise.Isolated(handlers, reported.Add);

        Assert.True(secondRan);
        Assert.Same(boom, Assert.Single(reported));
    }

    /// <summary>An event with no subscribers is a no-op and never reports an error.</summary>
    [Fact]
    public void Isolated_NullHandlers_DoesNothing()
    {
        int reports = 0;

        EventRaise.Isolated(null, _ => reports++);
        EventRaise.Isolated<string>(null, "x", _ => reports++);

        Assert.Equal(0, reports);
    }

    /// <summary>The generic overload hands its argument to every handler, including those after a throwing one.</summary>
    [Fact]
    public void IsolatedOfT_PassesArgumentToEveryHandler()
    {
        List<string> seen = [];
        List<Exception> reported = [];
        Action<string>? handlers = null;
        handlers += seen.Add;
        handlers += _ => throw new InvalidOperationException("boom");
        handlers += argument => seen.Add(argument + "!");

        EventRaise.Isolated(handlers, "room1", reported.Add);

        Assert.Equal(["room1", "room1!"], seen);
        Assert.Single(reported);
    }
}

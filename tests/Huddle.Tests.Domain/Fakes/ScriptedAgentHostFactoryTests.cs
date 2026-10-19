using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Fakes;

/// <summary>Tests for <see cref="ScriptedAgentHostFactory"/>, the failing-start wrapper over an agent host factory.</summary>
public sealed class ScriptedAgentHostFactoryTests
{
    /// <summary>FailFor throws for the named Persona on every start and passes every other Persona to the inner factory.</summary>
    [Fact]
    public async Task FailFor_ThrowsForNamedPersonaEveryTime_OthersDelegate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentHostFactory inner = new();
        InvalidOperationException failure = new("boom");
        ScriptedAgentHostFactory factory = ScriptedAgentHostFactory.FailFor(inner, "bad", failure);

        InvalidOperationException first = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.StartAsync(new Persona("bad", "t"), "a1", ct));
        InvalidOperationException second = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.StartAsync(new Persona("bad", "t"), "a1", ct));
        IPersonaHost host = await factory.StartAsync(new Persona("good", "t"), "a2", ct);

        Assert.Same(failure, first);
        Assert.Same(failure, second);
        Assert.NotNull(host);
        Assert.Single(inner.Calls);
    }

    /// <summary>FailOnceFor throws on the first start of the named Persona only; later starts reach the inner factory.</summary>
    [Fact]
    public async Task FailOnceFor_ThrowsOnFirstStartOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        FakeAgentHostFactory inner = new();
        ScriptedAgentHostFactory factory = ScriptedAgentHostFactory.FailOnceFor(inner, "nova", new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.StartAsync(new Persona("nova", "t"), "a1", ct));
        IPersonaHost host = await factory.StartAsync(new Persona("nova", "t"), "a1", ct);

        Assert.NotNull(host);
        Assert.Single(inner.Calls);
    }

    /// <summary>Under concurrent starts of the same Persona exactly one start fails.</summary>
    [Fact]
    public async Task FailOnceFor_ConcurrentStarts_FailExactlyOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ScriptedAgentHostFactory factory = ScriptedAgentHostFactory.FailOnceFor(new FakeAgentHostFactory(), "nova", new InvalidOperationException("boom"));

        async Task<bool> StartOnceAsync()
        {
            await Task.Yield();
            try
            {
                _ = await factory.StartAsync(new Persona("nova", "t"), "a1", ct);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        Task<bool>[] starts = [.. Enumerable.Range(0, 32).Select(_ => StartOnceAsync())];
        bool[] failed = await Task.WhenAll(starts);

        Assert.Single(failed, static f => f);
    }

    /// <summary>The failure callback sees the Persona and the 1-based attempt number for that Persona.</summary>
    [Fact]
    public async Task Constructor_FailureCallback_ReceivesPersonaAndAttempt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<(string Name, int Attempt)> seen = [];
        ScriptedAgentHostFactory factory = new(new FakeAgentHostFactory(), (persona, attempt) =>
        {
            seen.Add((persona.Name, attempt));
            return null;
        });

        _ = await factory.StartAsync(new Persona("a", "t"), "x", ct);
        _ = await factory.StartAsync(new Persona("a", "t"), "x", ct);
        _ = await factory.StartAsync(new Persona("b", "t"), "y", ct);

        Assert.Equal([("a", 1), ("a", 2), ("b", 1)], seen);
    }
}

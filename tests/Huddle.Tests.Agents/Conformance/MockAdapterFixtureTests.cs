using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>Tests for the request-reading members of <see cref="MockAdapterFixture"/>.</summary>
public sealed class MockAdapterFixtureTests
{
    /// <summary>SessionNewAsync returns the <c>session/new</c> request the real host sent the peer.</summary>
    [Fact]
    public async Task SessionNewAsync_StartedPersona_ReturnsTheSessionNewRequest()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", "You are Nova."), cancellationToken: ct);

        JsonObject request = await fixture.SessionNewAsync(ct);

        Assert.Equal("session/new", (string?)request["method"]);
    }

    /// <summary>AppendedSystemPromptAsync returns the text the host appended to the system prompt.</summary>
    [Fact]
    public async Task AppendedSystemPromptAsync_StartedPersona_ReturnsTheAppendedPrompt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(new Persona("nova", "You are Nova."), cancellationToken: ct);

        string appended = await fixture.AppendedSystemPromptAsync(ct);

        Assert.Contains("You are Nova.", appended, StringComparison.Ordinal);
    }

    /// <summary>SessionNewMeta returns <c>params._meta</c> and <see langword="null"/> when any step is missing.</summary>
    [Fact]
    public void SessionNewMeta_RequestWithAndWithoutMeta_ReturnsMetaOrNull()
    {
        JsonObject withMeta = new() { ["params"] = new JsonObject { ["_meta"] = new JsonObject { ["k"] = "v" } } };
        JsonObject withoutMeta = new() { ["params"] = new JsonObject() };

        Assert.Equal("v", (string?)MockAdapterFixture.SessionNewMeta(withMeta)?["k"]);
        Assert.Null(MockAdapterFixture.SessionNewMeta(withoutMeta));
        Assert.Null(MockAdapterFixture.SessionNewMeta([]));
    }
}

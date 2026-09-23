namespace Agency.Huddle.Acp.Tests.Abstractions;

using System;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

/// <summary>Covers <see cref="AgentSessionNotFoundException"/> (RS §6.4 A-2): a typed, sealed <see cref="AgentException"/> for a resource-not-found resume.</summary>
public sealed class AgentSessionNotFoundExceptionTests
{
    /// <summary>The type is sealed, derives from <see cref="AgentException"/>, and carries the id that was not found.</summary>
    [Fact]
    public void Ctor_SessionId_IsSealedAgentExceptionCarryingTheId()
    {
        AgentSessionNotFoundException exception = new AgentSessionNotFoundException("sess-1");

        Assert.True(typeof(AgentSessionNotFoundException).IsSealed);
        Assert.IsAssignableFrom<AgentException>(exception);
        Assert.Equal("sess-1", exception.SessionId);
    }

    /// <summary>The inner-exception constructor preserves both the id and the cause.</summary>
    [Fact]
    public void Ctor_SessionIdAndInner_PreservesBoth()
    {
        InvalidOperationException inner = new InvalidOperationException("resource not found");

        AgentSessionNotFoundException exception = new AgentSessionNotFoundException("sess-2", inner);

        Assert.Equal("sess-2", exception.SessionId);
        Assert.Same(inner, exception.InnerException);
    }
}

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Fakes;

/// <summary>
/// An <see cref="IAgentHostFactory"/> that asks a callback, before each start, whether that start fails, and
/// otherwise delegates to the wrapped factory. The callback gets the Persona and its 1-based attempt number
/// (how many times this factory has been asked to start that Persona, this call included). The attempt
/// counter is atomic because the supervisor starts hosts concurrently.
/// </summary>
/// <param name="inner">The factory every non-failing start goes to.</param>
/// <param name="failure">The exception to throw for a start, or <see langword="null"/> to let it through.</param>
internal sealed class ScriptedAgentHostFactory(IAgentHostFactory inner, Func<Persona, int, Exception?> failure) : IAgentHostFactory
{
    private readonly ConcurrentDictionary<string, StrongBox<int>> attempts = new(StringComparer.Ordinal);

    /// <summary>Fails every start of the named Persona with <paramref name="exception"/>; other Personas pass through.</summary>
    /// <param name="inner">The factory non-failing starts go to.</param>
    /// <param name="personaName">The Persona whose starts fail.</param>
    /// <param name="exception">What each failing start throws.</param>
    /// <returns>The scripted factory.</returns>
    public static ScriptedAgentHostFactory FailFor(IAgentHostFactory inner, string personaName, Exception exception)
    {
        return new(inner, (persona, _) => string.Equals(persona.Name, personaName, StringComparison.Ordinal) ? exception : null);
    }

    /// <summary>Fails only the first start of the named Persona with <paramref name="exception"/>; every later start passes through.</summary>
    /// <param name="inner">The factory non-failing starts go to.</param>
    /// <param name="personaName">The Persona whose first start fails.</param>
    /// <param name="exception">What the failing start throws.</param>
    /// <returns>The scripted factory.</returns>
    public static ScriptedAgentHostFactory FailOnceFor(IAgentHostFactory inner, string personaName, Exception exception)
    {
        return new(inner, (persona, attempt) => attempt == 1 && string.Equals(persona.Name, personaName, StringComparison.Ordinal) ? exception : null);
    }

    /// <inheritdoc/>
    public Task<IPersonaHost> StartAsync(Persona persona, string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);

        StrongBox<int> counter = this.attempts.GetOrAdd(persona.Name, static _ => new StrongBox<int>());
        int attempt = Interlocked.Increment(ref counter.Value);
        Exception? failed = failure(persona, attempt);
        if (failed is not null)
        {
            throw failed;
        }

        return inner.StartAsync(persona, agentId, cancellationToken);
    }
}

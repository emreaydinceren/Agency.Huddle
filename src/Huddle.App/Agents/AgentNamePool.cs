using System.Globalization;
using System.Text.Json;

namespace Agency.Huddle.App.Agents;

/// <summary>
/// The pool of first names a new Agent's Name box starts with. The names ship embedded in the assembly and
/// satisfy the Agent name rules, so a suggestion can be saved as it stands.
/// </summary>
internal static class AgentNamePool
{
    private const string ResourceName = "Agents/AgentNames.json";

    /// <summary>Every name of the pool, in file order.</summary>
    internal static IReadOnlyList<string> Names { get; } = AgentNamePool.ReadBuiltIn();

    /// <summary>
    /// Picks a name at random. Names in <paramref name="taken"/> are skipped, ignoring case, and so is
    /// <paramref name="current"/> while another name is free, so asking again always shows a different name. When
    /// every name is taken, a pool name with a number after it is returned instead. The result depends on nothing
    /// but the arguments, so a caller that is tested hands in a seeded <see cref="Random"/>.
    /// </summary>
    /// <param name="random">The source of the choice.</param>
    /// <param name="taken">The names of the Agents that already exist.</param>
    /// <param name="current">The name now showing, or <see langword="null"/> when none is.</param>
    /// <returns>A name no existing Agent has.</returns>
    internal static string Pick(Random random, IEnumerable<string> taken, string? current = null)
    {
        HashSet<string> used = new(taken, StringComparer.OrdinalIgnoreCase);
        List<string> free = [.. AgentNamePool.Names.Where(name => !used.Contains(name))];

        if (free.Count > 1 && current is not null)
        {
            _ = free.RemoveAll(name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
        }

        if (free.Count > 0)
        {
            return free[random.Next(free.Count)];
        }

        string baseName = AgentNamePool.Names[random.Next(AgentNamePool.Names.Count)];
        for (int suffix = 2; ; suffix++)
        {
            string candidate = string.Create(CultureInfo.InvariantCulture, $"{baseName} {suffix}");
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>Reads the embedded name list.</summary>
    /// <returns>The names.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing or does not parse.</exception>
    private static List<string> ReadBuiltIn()
    {
        using Stream stream = typeof(AgentNamePool).Assembly.GetManifestResourceStream(AgentNamePool.ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{AgentNamePool.ResourceName}' is missing.");
        return JsonSerializer.Deserialize<List<string>>(stream)
            ?? throw new InvalidOperationException($"Embedded resource '{AgentNamePool.ResourceName}' is malformed.");
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>Builds the Turn hook pipeline and memory paths a started Persona gets, for a test whose container holds a File Changes tracker.</summary>
public static class TurnHookTestFactory
{
    /// <summary>Builds File Changes switched on for <paramref name="persona"/>, with its declared Watched Folders read as the supervisor reads them.</summary>
    /// <param name="services">The container holding the tracker, the prompts and the logger factory.</param>
    /// <param name="persona">The Persona the pipeline serves.</param>
    /// <param name="includeAgentsMd">When true, the container's AGENTS.md hook goes first and File Changes leaves out the in-scope AGENTS.md, as <c>PersonaSupervisor</c> builds it.</param>
    /// <returns>The pipeline, and the tracker's memory paths (null for a hand-built tracker).</returns>
    internal static (TurnHookPipeline Hooks, MemoryPaths? MemoryPaths) ForPersona(IServiceProvider services, Persona persona, bool includeAgentsMd = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(persona);

        FileChangeTracker tracker = services.GetRequiredService<FileChangeTracker>();
        IPromptSource prompts = services.GetRequiredService<IPromptSource>();
        ILoggerFactory loggerFactory = services.GetRequiredService<ILoggerFactory>();
        IReadOnlyList<string> watches = PersonaFrontmatter.TryReadIdentity(persona.Text, out PersonaIdentity? identity, out _)
            ? identity.Watches ?? []
            : [];

        FileChangesTurnHook hook = new(tracker, prompts, loggerFactory.CreateLogger<FileChangesTurnHook>(), includeAgentsMd ? services.GetRequiredService<AgentsMdScopes>() : null);
        AgentsMdTurnHook? agentsMd = includeAgentsMd ? services.GetRequiredService<AgentsMdTurnHook>() : null;
        TurnHookScope scope = new(persona.Name, ReadsFiles: true, FileChangesOn: true, watches);
        return (TurnHookPipeline.ForPersona(scope, agentsMd, hook, loggerFactory.CreateLogger<TurnHookPipeline>()), tracker.MemoryPaths);
    }
}

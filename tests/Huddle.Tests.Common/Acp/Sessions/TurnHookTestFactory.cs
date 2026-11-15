using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.References;
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
        return (TurnHookPipeline.ForPersona(scope, [.. new ITurnHook?[] { agentsMd, hook }.OfType<ITurnHook>()], loggerFactory.CreateLogger<TurnHookPipeline>()), tracker.MemoryPaths);
    }

    /// <summary>Builds the Turn enricher pipeline a hand-built <c>RoomSession</c> needs for Library documents and Referenced items, as <c>PersonaSupervisor</c> builds it.</summary>
    /// <param name="libraryDocs">The Library collector, or <see langword="null"/> for none.</param>
    /// <param name="readsFiles">Whether the Adapter can read files.</param>
    /// <param name="references">The Referenced items expander, or <see langword="null"/> for none.</param>
    /// <param name="logger">Receives the Library enricher's log lines, or <see langword="null"/> for none.</param>
    /// <param name="isChiefOfStaff">Whether a Persona, by Name, is the Chief of Staff for the Referenced items hint, or <see langword="null"/> for no Persona being it.</param>
    /// <returns>The pipeline, or <see langword="null"/> when neither collaborator is given.</returns>
    internal static TurnEnricherPipeline? Enrichers(LibraryDocumentCollector? libraryDocs = null, bool readsFiles = true, ReferenceExpander? references = null, ILogger? logger = null, Func<string, bool>? isChiefOfStaff = null)
    {
        List<ITurnEnricher> enrichers = [];
        if (libraryDocs is not null)
        {
            enrichers.Add(new LibraryDocumentsEnricher(libraryDocs, readsFiles, logger ?? NullLogger.Instance));
        }

        if (references is not null)
        {
            enrichers.Add(new ReferencedItemsEnricher(references, isChiefOfStaff ?? (static _ => false)));
        }

        return enrichers.Count == 0 ? null : TurnEnricherPipeline.ForPersona(enrichers, NullLogger.Instance);
    }

    /// <summary>Builds the hook list a hand-built <c>PersonaSupervisor</c> needs for File Changes: the File Changes hook over <paramref name="tracker"/>, or none when there is no tracker.</summary>
    /// <param name="tracker">The tracker the supervisor was given, or <see langword="null"/>.</param>
    /// <param name="prompts">Supplies the block's wording.</param>
    /// <returns>The hooks to pass as the supervisor's <c>turnHooks</c>.</returns>
    internal static IReadOnlyList<ITurnHook> FileChangesHooks(FileChangeTracker? tracker, IPromptSource prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        return tracker is null
            ? []
            : [new FileChangesTurnHook(tracker, prompts, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileChangesTurnHook>.Instance)];
    }
}

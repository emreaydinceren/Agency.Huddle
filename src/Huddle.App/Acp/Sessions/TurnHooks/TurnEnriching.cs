namespace Agency.Huddle.App.Acp.Sessions.TurnHooks;

/// <summary>A Message Turn about to be enriched, after the Transcript Catch-up and before it is published.</summary>
/// <param name="Item">The item as the previous enricher left it.</param>
/// <param name="PersonaName">The Persona the Turn is for, for log lines.</param>
/// <param name="Host">The Persona's running Adapter host, or <see langword="null"/> for a caller that predates resume: nothing was advertised then.</param>
/// <param name="Clean">Whether the Turn is a goldfish possession's: Referenced items keep only Tasks.</param>
internal sealed record TurnEnriching(WorkItem Item, string PersonaName, IPersonaHost? Host, bool Clean = false);

namespace Agency.Huddle.App.Acp;

/// <summary>One Persona's Agent as it stands, and since when.</summary>
/// <param name="State">What is currently known about this Agent.</param>
/// <param name="Reason">
/// Why the Agent is in <paramref name="State"/>, when one is known; <see langword="null"/> when it is
/// not. This text is read by the Human — in a tooltip or a Room banner — not by a model, so it is
/// interface copy: <c>docs/engineering/language.md</c> is binding for it. "Adapter", "Model", "Turn",
/// "Budget", "Persona" and "Agent" are its defined words; "bot", "backend", "LLM", "sandbox", "rate
/// limit", "cap" and "quota" are on its avoid list. A Reason is deliberately not a Prompt: roadmap item
/// 13 moved model-facing text into <c>PromptCatalog</c>/<c>prompts.json</c>, but this text is read by the
/// Human, never by a model, so it stays in code and must never be routed through
/// <c>PromptCatalog</c>.
/// </param>
/// <param name="Since">
/// When <paramref name="State"/> — or, with the State unchanged, <paramref name="Reason"/> — last
/// changed for this Agent.
/// </param>
public sealed record PersonaStatus(PersonaState State, string? Reason, DateTimeOffset Since);

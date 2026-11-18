using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.App.Acp;

/// <summary>What <see cref="SessionPromptInputs.Build"/> needs to build the <see cref="PlaybookSnapshot"/>, beside the AGENTS.md reader.</summary>
/// <param name="Prompts">Resolves the default Playbook texts.</param>
/// <param name="Options">The size limits, bound from <c>Team:Playbooks</c>.</param>
/// <param name="Logger">Told about a Playbook file that cannot be read.</param>
internal sealed record PlaybookInputs(IPromptSource Prompts, PlaybookOptions Options, ILogger Logger);

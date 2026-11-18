namespace Agency.Huddle.App.Acp;

/// <summary>One Playbook text an Agent receives, for one or more Teams (Team Playbooks spec §6.1).</summary>
/// <param name="Kind">Whether it is the Member or the Manager Playbook.</param>
/// <param name="Teams">The Teams it is for, spelled as the catalog spells them and in the Persona's <c>teams</c> order.</param>
/// <param name="Text">The Playbook text: the effective default, or the body of a Team's own file.</param>
/// <param name="Source">A <see cref="PromptPartSource.Prompt"/> for the default, a <see cref="PromptPartSource.File"/> for a Team's own.</param>
internal sealed record PlaybookSection(PlaybookKind Kind, IReadOnlyList<string> Teams, string Text, PromptPartSource Source);

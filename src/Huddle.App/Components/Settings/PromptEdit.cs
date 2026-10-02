namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// One field's edited text, raised by <c>PromptsPanel</c>'s <c>ValueChanged</c> callback when the user
/// types into a prompt's textarea. A record rather than two separate <see langword="string"/> parameters
/// (or a tuple): every <c>PromptsPanel</c> parameter bound from Razor markup is deliberately non-string,
/// the same defence <c>docs/engineering/rules.md</c> documents for <c>TeammateCard</c> — a
/// <see langword="string"/> parameter bound as <c>Foo="this.bar"</c> compiles cleanly and silently
/// passes the literal text instead of the field's value, and a non-string parameter cannot be
/// mis-bound that way at all.
/// </summary>
/// <param name="Key">The edited prompt's <see cref="Agency.Huddle.App.Prompts.PromptDefinition.Key"/>.</param>
/// <param name="Text">The field's new pending text, as typed so far.</param>
public sealed record PromptEdit(string Key, string Text);

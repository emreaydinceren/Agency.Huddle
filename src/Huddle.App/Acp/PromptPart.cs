namespace Agency.Huddle.App.Acp;

/// <summary>One labelled part of a composed system prompt (Spec §6.4, §8.1), with where its text came from.</summary>
/// <param name="Label">The part's name from Spec §8.1's slot list, e.g. <c>Orientation</c>.</param>
/// <param name="Source">What produced the text.</param>
/// <param name="Text">The part's text; joined with a blank line, the parts are the composed prompt.</param>
internal sealed record PromptPart(string Label, PromptPartSource Source, string Text);

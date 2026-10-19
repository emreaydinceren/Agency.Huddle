namespace Agency.Huddle.App.Elicitation;

/// <summary>
/// What the Human's answer to a form comes to: the text of the Transcript Message and the content the
/// wire carries back to the agent.
/// </summary>
/// <param name="Text">The Message text: each answered field's label quoted, a blank line, then the answer.</param>
/// <param name="Content">
/// The answers keyed by field, filled keys only, each a plain CLR value of the JSON type the server
/// expects: <see cref="string"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/> or <c>string[]</c>.
/// </param>
internal sealed record ElicitationAnswer(string Text, IReadOnlyDictionary<string, object> Content);

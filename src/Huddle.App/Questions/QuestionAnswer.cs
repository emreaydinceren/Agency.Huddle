namespace Agency.Huddle.App.Questions;

/// <summary>The Human's answer to one Question: option indexes, in the order that matters.</summary>
/// <param name="Chosen">The chosen option indexes. For a ranking, every index, most important first.</param>
public sealed record QuestionAnswer(IReadOnlyList<int> Chosen);

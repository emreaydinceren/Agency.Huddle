namespace Agency.Huddle.App.Questions;

/// <summary>One multiple-choice Question an Agent puts to the Human.</summary>
/// <param name="Text">The question, as the Agent wrote it.</param>
/// <param name="Options">Two to four short options.</param>
/// <param name="Kind">How the Human answers it.</param>
public sealed record Question(string Text, IReadOnlyList<string> Options, QuestionKind Kind);

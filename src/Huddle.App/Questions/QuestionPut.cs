namespace Agency.Huddle.App.Questions;

/// <summary>What <see cref="QuestionStore.TryPut"/> did, and, only when refused, the card already waiting.</summary>
/// <param name="Result">Which of the three outcomes occurred.</param>
/// <param name="Existing">The card already waiting when <paramref name="Result"/> is <see cref="QuestionPutResult.Refused"/>; otherwise <see langword="null"/>.</param>
internal sealed record QuestionPut(QuestionPutResult Result, PendingQuestions? Existing);

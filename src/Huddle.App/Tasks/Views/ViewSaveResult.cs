namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The result of saving or deleting a View.</summary>
public sealed record ViewSaveResult(bool Saved, IReadOnlyList<string> Problems);

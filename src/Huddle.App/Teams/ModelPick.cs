namespace Agency.Huddle.App.Teams;

/// <summary>The adapter and model chosen for a tier.</summary>
/// <param name="AdapterId">The chosen adapter id.</param>
/// <param name="Model">The model id, or <see langword="null"/> for the adapter's default model.</param>
internal sealed record ModelPick(string AdapterId, string? Model);

namespace Agency.Huddle.App.Acp;

/// <summary>
/// The display names <see cref="IModelCatalog.PeekRuntime"/> could resolve from cached probe data.
/// </summary>
/// <param name="Model">The model's display name, or <see langword="null"/> when it is not cached.</param>
/// <param name="Effort">The effort level's display name, or <see langword="null"/> when it is not cached or names no level.</param>
public readonly record struct RuntimeNames(string? Model, string? Effort);

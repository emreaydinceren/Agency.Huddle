namespace Agency.Huddle.App.Components;

/// <summary>
/// What an Agent runs on, as <see cref="Shared.ProfileCard"/> reads it out: three display strings. Model
/// and Effort are display names when the model catalog had them cached (a default reads "Name (default)"),
/// else the raw id, else the Agent card's own default wording, so the two surfaces never disagree.
/// </summary>
/// <param name="Adapter">The Adapter's display name: the one the Agent actually runs on, which is the default Adapter when the Persona names none or names one that is not configured.</param>
/// <param name="Model">The Model's display name (or id when not cached); "{name} (default)" when the Persona chose none and the Adapter's default is known; else "Agent default".</param>
/// <param name="Effort">The Effort's display name (or id when not cached); "{name} (default)" when the Persona chose none and the default is known; else "Model default".</param>
public sealed record ProfileRuntime(string Adapter, string Model, string Effort);

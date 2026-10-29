namespace Agency.Huddle.App.Components;

/// <summary>
/// What an Agent runs on, as <see cref="Shared.ProfileCard"/> reads it out: three display strings, with a
/// default spelled the way the Agent card spells it, so the two surfaces never disagree.
/// </summary>
/// <param name="Adapter">The Adapter's display name: the one the Agent actually runs on, which is the default Adapter when the Persona names none or names one that is not configured.</param>
/// <param name="Model">The Model id, or "Agent default" when the Persona chose none.</param>
/// <param name="Effort">The Effort id, or "Model default" when the Persona chose none.</param>
public sealed record ProfileRuntime(string Adapter, string Model, string Effort);

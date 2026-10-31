namespace Agency.Huddle.App.Profile;

/// <summary>
/// Who the Human is: the Name every Room, Task and Mention knows them by, an optional Alias that
/// also Mentions them, and the Title and Description their profile card shows. The Human's
/// counterpart of <see cref="Acp.PersonaIdentity"/>, stored in <c>{DataDir}/profile.json</c> by
/// <see cref="HumanProfileStore"/>.
/// </summary>
/// <param name="Name">The Human's Name, valid under <see cref="Contracts.NameRules.IsValidAgentName(string?)"/>.</param>
/// <param name="Alias">A second Name that Mentions the Human, or <see langword="null"/> for none.</param>
/// <param name="Title">The line under the Name on the profile card, or <see langword="null"/> for none.</param>
/// <param name="Description">A short description shown on the profile card, or <see langword="null"/> for none.</param>
public sealed record HumanProfile(string Name, string? Alias, string? Title, string? Description);

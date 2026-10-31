namespace Agency.Huddle.App.Profile;

/// <summary>
/// The old and new Name of a Human rename, raised by <see cref="HumanProfileStore.HumanRenamed"/>.
/// </summary>
/// <param name="OldName">The Name the Human had before.</param>
/// <param name="NewName">The Name the Human has now.</param>
internal sealed record HumanRenamed(string OldName, string NewName);

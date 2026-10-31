namespace Agency.Huddle.App.SkillSources;

/// <summary>Why <see cref="SkillSourcesSettings.SetDir"/> refused a folder.</summary>
/// <param name="Changed">True when the folder was set.</param>
/// <param name="Problem">One sentence for the Human when it was refused; null when set.</param>
internal sealed record SetDirResult(bool Changed, string? Problem);

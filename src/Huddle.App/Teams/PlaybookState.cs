namespace Agency.Huddle.App.Teams;

/// <summary>The state of one Playbook for one Team.</summary>
/// <param name="Status">Which state it is in.</param>
/// <param name="Text">The file's body, or the effective default for <see cref="PlaybookStatus.Default"/> and <see cref="PlaybookStatus.Unreadable"/>.</param>
/// <param name="Path">The full path of the Playbook file, whether or not it exists.</param>
/// <param name="Diff">The unified diff from the default the file started from to the current default; only for <see cref="PlaybookStatus.DefaultUpdated"/> when Huddle still has the earlier default.</param>
/// <param name="OverLimitLength">The body's length when it is over <c>Team:Playbooks:MaxChars</c>, else 0.</param>
internal sealed record PlaybookState(PlaybookStatus Status, string Text, string Path, string? Diff, int OverLimitLength);

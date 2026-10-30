namespace Agency.Huddle.App.Desktop;

/// <summary>The outcome of <see cref="DesktopHandOff"/>.</summary>
/// <param name="Lock">The instance lock when the caller became the owner; otherwise <see langword="null"/>.</param>
/// <param name="Opened">Whether the running instance's URL was opened.</param>
internal sealed record HandOffResult(InstanceLock? Lock, bool Opened);

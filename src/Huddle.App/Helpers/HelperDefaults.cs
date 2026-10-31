namespace Agency.Huddle.App.Helpers;

/// <summary>The code defaults of one Helper, before appsettings and the Human's choice.</summary>
/// <param name="Enabled">Whether the Helper runs.</param>
/// <param name="Adapter">The Adapter id, or <see langword="null"/> for the default Adapter.</param>
/// <param name="Model">The Model id, or <see langword="null"/> for the Adapter's default.</param>
/// <param name="Effort">The Effort, or <see langword="null"/> for the Model's default.</param>
/// <param name="DailyCap">Calls a day before it pauses; at least 1.</param>
/// <param name="ContinueGrant">Calls the Continue button adds for today.</param>
/// <param name="WaitSeconds">How long to wait for further saves of the same key before judging.</param>
/// <param name="TimeoutSeconds">How long one call may take.</param>
internal sealed record HelperDefaults(
    bool Enabled,
    string? Adapter,
    string? Model,
    string? Effort,
    int DailyCap,
    int ContinueGrant = 10,
    int WaitSeconds = 10,
    int TimeoutSeconds = 60);

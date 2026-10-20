namespace Agency.Huddle.App.Helpers;

/// <summary>The Human's choice for one Helper, stored whole: with a row, every column is theirs.</summary>
/// <param name="Enabled">Whether the Helper runs.</param>
/// <param name="Adapter">The Adapter id; <see langword="null"/> is the default Adapter.</param>
/// <param name="Model">The Model id; <see langword="null"/> is the Adapter's own default, never the configured Model.</param>
/// <param name="Effort">The Effort; <see langword="null"/> is the Model's default.</param>
/// <param name="DailyCap">Calls a day before it pauses; at least 1.</param>
internal sealed record HelperSettingsRow(bool Enabled, string? Adapter, string? Model, string? Effort, int DailyCap);

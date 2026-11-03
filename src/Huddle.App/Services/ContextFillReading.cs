namespace Agency.Huddle.App.Services;

/// <summary>One reading of how full an Agent's context window is.</summary>
/// <param name="Used">How full the window is now, in tokens.</param>
/// <param name="Size">The size of the window in tokens; zero when the Adapter did not say.</param>
/// <param name="Shared">Whether the reading comes from the Agent's shared-mode session rather than a session of this Room.</param>
/// <param name="Model">The model the Adapter said answered last, or <see langword="null"/> when it has not said.</param>
internal sealed record ContextFillReading(long Used, long Size, bool Shared, string? Model = null)
{
    /// <summary>The fill as a whole percent, floored and capped at 100; <see langword="null"/> when <see cref="Size"/> is not positive.</summary>
    public int? Percent => this.Size <= 0 ? null : (int)Math.Min(100, this.Used * 100 / this.Size);
}

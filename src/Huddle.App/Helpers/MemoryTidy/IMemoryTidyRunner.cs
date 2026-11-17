namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What the timetable asks of the runner: a pass now, or a refusal. A seam so the timetable's tests need no real pass.</summary>
internal interface IMemoryTidyRunner
{
    /// <summary>Raised when a pass starts or ends, so the card can repaint. May be raised on any thread.</summary>
    event Action? Changed;

    /// <summary>Whether a pass is running now.</summary>
    bool IsRunning { get; }

    /// <summary>Asks for a pass now.</summary>
    /// <param name="start">What is asking.</param>
    /// <returns>A task that ends with how the pass ended, or <see langword="null"/> when a pass is already running or the host is stopping.</returns>
    Task<MemoryTidyPassEnd>? TryStart(MemoryTidyStart start);
}

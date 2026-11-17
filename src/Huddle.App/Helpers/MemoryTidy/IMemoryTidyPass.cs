namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>One Memory Tidy pass, so the runner that owns it can be tested without the disk.</summary>
internal interface IMemoryTidyPass
{
    /// <summary>Runs one pass to its end.</summary>
    /// <param name="start">What started it.</param>
    /// <param name="cancellationToken">The runner's stopping token.</param>
    /// <returns>How the pass ended.</returns>
    Task<MemoryTidyPassEnd> RunAsync(MemoryTidyStart start, CancellationToken cancellationToken);
}

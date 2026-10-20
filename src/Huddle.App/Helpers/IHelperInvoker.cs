namespace Agency.Huddle.App.Helpers;

/// <summary>
/// Runs one Helper without the dispatcher knowing its input or Verdict types: it checks the cache, calls the
/// <see cref="HelperRunner"/>, and applies the Verdict.
/// </summary>
internal interface IHelperInvoker
{
    /// <summary>The Helper this invoker runs.</summary>
    IHelperAgent Helper { get; }

    /// <summary>Judges <paramref name="input"/> and applies the Verdict; never throws for a failed call.</summary>
    /// <param name="key">What the judgement is about, such as a Task id.</param>
    /// <param name="input">The input; it must be the Helper's input type.</param>
    /// <param name="settings">The Helper's effective settings for this call.</param>
    /// <param name="cancellationToken">Cancels the judgement.</param>
    Task<HelperRunReport> ExecuteAsync(string key, object input, HelperSettings settings, CancellationToken cancellationToken);
}

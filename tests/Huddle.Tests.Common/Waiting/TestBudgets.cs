namespace Agency.Huddle.Tests.Waiting;

/// <summary>
/// Named wait budgets. A migrated call keeps the value it had and is written as the budget of the same
/// value; a value with no named budget stays a literal. There is deliberately no single default.
/// </summary>
internal static class TestBudgets
{
    /// <summary>Two seconds: a single in-process hand-off.</summary>
    internal static readonly TimeSpan Short = TimeSpan.FromSeconds(2);

    /// <summary>Five seconds: an ordinary asynchronous step.</summary>
    internal static readonly TimeSpan Standard = TimeSpan.FromSeconds(5);

    /// <summary>Ten seconds: a step that involves a process, a pipe or the file system.</summary>
    internal static readonly TimeSpan Slow = TimeSpan.FromSeconds(10);

    /// <summary>Thirty seconds: a whole scenario on a loaded machine.</summary>
    internal static readonly TimeSpan Slowest = TimeSpan.FromSeconds(30);
}

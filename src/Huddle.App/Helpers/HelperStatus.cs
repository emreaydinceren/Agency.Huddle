namespace Agency.Huddle.App.Helpers;

/// <summary>What the Helper's card shows about its last run, kept across a restart.</summary>
/// <param name="LastRunAt">When it last ran to a result; a failure does not move it.</param>
/// <param name="LastResult">What the last good run did, such as <c>Turned the gate on for RSH-0208</c>.</param>
/// <param name="LastError">The last failure.</param>
/// <param name="LastErrorAt">When it failed.</param>
/// <param name="ConsecutiveFailures">Failures since the last good run.</param>
internal sealed record HelperStatus(DateTimeOffset? LastRunAt, string? LastResult, string? LastError, DateTimeOffset? LastErrorAt, int ConsecutiveFailures)
{
    /// <summary>A Helper that has never run.</summary>
    public static HelperStatus None { get; } = new(null, null, null, null, 0);
}

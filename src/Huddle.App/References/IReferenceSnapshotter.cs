using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>Builds the reference snapshot a Message carries (spec §8.2). Public because <c>ChatService</c>'s constructor is.</summary>
public interface IReferenceSnapshotter
{
    /// <summary>Scans <paramref name="text"/>, looks each reference up now and records what resolved.</summary>
    /// <param name="text">The Message's text, as sent.</param>
    /// <param name="ct">Cancels the lookups.</param>
    /// <returns>The references that resolved, or null when there are none or the lookup failed.</returns>
    Task<IReadOnlyList<MessageReference>?> SnapshotAsync(string text, CancellationToken ct);
}

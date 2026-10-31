using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.References;

/// <summary>A reference whose target no longer exists, or never did.</summary>
/// <param name="Kind">What it named.</param>
/// <param name="Key">The key it named.</param>
internal sealed record GoneReference(ReferenceKind Kind, string Key) : ResolvedReference;

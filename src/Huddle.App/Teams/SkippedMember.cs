namespace Agency.Huddle.App.Teams;

/// <summary>A source member that was not copied, with the plain reason.</summary>
/// <param name="Name">The source member's name.</param>
/// <param name="Reason">Why the member was left out.</param>
internal sealed record SkippedMember(string Name, string Reason);

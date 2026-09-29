namespace Agency.Huddle.App.Components.Teams;

/// <summary>One Teammate the Add member dialog offers: a snapshot of the Persona's Name, Title and Alias.</summary>
/// <param name="Name">The Persona's Name, which is what gets added to the Team.</param>
/// <param name="Title">The Persona's Title, searched and shown beside the Name.</param>
/// <param name="Alias">The Persona's Alias, searched but not shown.</param>
public sealed record TeammateChoice(string Name, string Title, string Alias);

namespace Agency.Huddle.App.Components;

/// <summary>
/// Which face a <c>AgentProfileCard</c> is showing. The three modes share one layout on purpose: a
/// Agent looks like the same person whether you are reading about them, changing them, or
/// writing them down for the first time.
/// </summary>
public enum AgentProfileCardMode
{
    /// <summary>Read-only details for an existing Agent.</summary>
    View,

    /// <summary>An existing Agent's Persona text, open for changes. The Name is fixed.</summary>
    Edit,

    /// <summary>An Agent that does not exist yet. Both the Name and the text are open.</summary>
    Create,
}
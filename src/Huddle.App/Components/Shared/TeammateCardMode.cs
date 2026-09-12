namespace Agency.Huddle.App.Components;

/// <summary>
/// Which face a <c>TeammateCard</c> is showing. The three modes share one layout on purpose: a
/// Teammate looks like the same person whether you are reading about them, changing them, or
/// writing them down for the first time.
/// </summary>
public enum TeammateCardMode
{
    /// <summary>Read-only details for an existing Teammate.</summary>
    View,

    /// <summary>An existing Teammate's Persona text, open for changes. The Name is fixed.</summary>
    Edit,

    /// <summary>A Teammate that does not exist yet. Both the Name and the text are open.</summary>
    Create,
}
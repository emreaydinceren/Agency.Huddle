namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// Which pane the Settings page's tab rail is showing. Public for the same reason
/// <c>TeammateCardMode</c> is: Razor generates <c>Settings.razor</c>'s component as a public type, and
/// a route parameter or field of an <c>internal</c> enum type would not compile there.
/// </summary>
public enum SettingsTab
{
    /// <summary>The read-only view of every model-facing prompt, grouped by area.</summary>
    Prompts,

    /// <summary>Choosing a theme, and where to override its tokens.</summary>
    Appearance,

    /// <summary>Where the Persona files and the SQLite database live on disk.</summary>
    Personas,
}

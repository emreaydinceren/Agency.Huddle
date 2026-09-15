namespace Agency.Huddle.App.Appearance;

/// <summary>
/// The Human's chosen light/dark mode, stored in <c>appearance.json</c> and read back by
/// <see cref="Components.Layout.MainLayout"/> to drive <c>MudThemeProvider.IsDarkMode</c>. Public
/// because it reaches a <c>[Parameter]</c> on the Appearance tab's control — Razor generates
/// component classes as <c>public</c>, so a parameter of an <c>internal</c> type fails with
/// <c>CS0053</c>.
/// </summary>
public enum DarkModePreference
{
    /// <summary>Follow the operating system's own light/dark preference, and keep following it if it changes.</summary>
    System,

    /// <summary>Always the light palette, regardless of the operating system.</summary>
    Light,

    /// <summary>Always the dark palette, regardless of the operating system.</summary>
    Dark,
}

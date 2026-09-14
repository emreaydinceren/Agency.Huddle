namespace Agency.Huddle.App.Themes;

/// <summary>
/// The complete set of CSS custom property names that <c>wwwroot/theme.css</c> declares. This is the
/// set a theme override key must belong to (D16), validated at runtime in C#. Parsing
/// <c>theme.css</c> itself at startup to validate a config file would be odd, and would make the
/// validator's correctness depend on a web asset path resolving at run time; keeping the same 39
/// names here instead means the check runs in-process with no file access. Drift between this list
/// and <c>theme.css</c> is a build failure — <c>ThemeFileTests</c> pins the two together, the same
/// way it pins <see cref="ThemeCatalog.BuiltIn"/> against the files on disk.
/// </summary>
/// <remarks>
/// Roadmap item 7 needs exactly this key set for its VSCode colour-theme mapping, so this list is a
/// prerequisite arriving early rather than a duplicate.
/// </remarks>
internal static class ThemeTokens
{
    /// <summary>Every token name <c>theme.css</c> declares, grouped and ordered to match that file.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            // ---- Surfaces ----
            "--surface-base",
            "--surface-sidebar",
            "--surface-raised",
            "--surface-sunken",
            "--surface-muted",
            "--surface-hover",
            "--surface-selected",

            // ---- Borders ----
            "--border-default",
            "--border-subtle",
            "--border-control",
            "--border-emphasis",

            // ---- Text ----
            "--text-primary",
            "--text-secondary",
            "--text-muted",
            "--text-faint",
            "--text-disabled",
            "--text-on-accent",

            // ---- Accent ----
            "--accent",
            "--accent-hover",
            "--accent-indicator",

            // ---- Status: the Teammate dot. Names match PersonaState's four values. ----
            "--status-online",
            "--status-offline",
            "--status-degraded",
            "--status-starting",

            // ---- Feedback ----
            "--danger-fg",
            "--danger-bg",
            "--danger-border",
            "--warning-fg",
            "--warning-bg",
            "--info-fg",
            "--info-bg",
            "--success-fg",

            // ---- Depth ----
            "--shadow-soft",
            "--shadow-strong",
            "--scrim",

            // ---- Typography ----
            "--font-ui",
            "--font-chat",
            "--font-mono",
            "--font-size-base",
        ],
        StringComparer.Ordinal);
}

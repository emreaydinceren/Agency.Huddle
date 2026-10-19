namespace Agency.Huddle.App.Themes.Skins;

/// <summary>
/// The two font stacks a skin sets, already formatted as CSS <c>font-family</c> values (family names quoted with
/// <c>"</c>). Either may be <see langword="null"/> to leave the matching variable to <c>app-vars.css</c>.
/// </summary>
/// <param name="Read">The reading face, written as <c>--font-read</c>.</param>
/// <param name="Mono">The monospace face, written as <c>--font-mono</c>.</param>
internal sealed record SkinFacesCss(string? Read, string? Mono);

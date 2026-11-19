namespace Agency.Huddle.App.Components.Shared;

/// <summary>
/// What <c>TextField</c> and <c>SelectField</c> share: every field has an accessible name (ComponentCatalog D5), and the caller's
/// pass-through attributes reach the input with <c>aria-label</c> added.
/// </summary>
internal static class FieldAttributes
{
    /// <summary>Throws when a field has neither a visible label nor an accessible name, so a nameless field fails its first render.</summary>
    /// <param name="component">The component's name, for the message.</param>
    /// <param name="label">The visible label.</param>
    /// <param name="ariaLabel">The accessible name for a field with no visible label.</param>
    /// <param name="user">The caller's pass-through attributes, which may name the field with <c>aria-label</c> or <c>aria-labelledby</c>.</param>
    public static void RequireName(string component, string? label, string? ariaLabel, IReadOnlyDictionary<string, object>? user)
    {
        if (!string.IsNullOrWhiteSpace(label) || !string.IsNullOrWhiteSpace(ariaLabel) || HasNamingAttribute(user))
        {
            return;
        }

        throw new InvalidOperationException(
            $"A {component} needs a name: set Label, or AriaLabel when the field has no visible label (ComponentCatalog D5).");
    }

    /// <summary>The attributes for the input: the caller's, plus <c>aria-label</c> when one is given.</summary>
    /// <param name="user">The caller's pass-through attributes.</param>
    /// <param name="ariaLabel">The accessible name, or null.</param>
    /// <returns>A new mutable dictionary, which is what MudBlazor's <c>UserAttributes</c> takes.</returns>
    public static Dictionary<string, object> Merge(IReadOnlyDictionary<string, object>? user, string? ariaLabel)
    {
        Dictionary<string, object> attributes = user is null ? new(StringComparer.Ordinal) : new(user, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(ariaLabel))
        {
            attributes["aria-label"] = ariaLabel;
        }

        return attributes;
    }

    private static bool HasNamingAttribute(IReadOnlyDictionary<string, object>? user) =>
        user is not null
        && ((user.TryGetValue("aria-label", out object? name) && !string.IsNullOrWhiteSpace(name?.ToString()))
            || (user.TryGetValue("aria-labelledby", out object? by) && !string.IsNullOrWhiteSpace(by?.ToString())));
}

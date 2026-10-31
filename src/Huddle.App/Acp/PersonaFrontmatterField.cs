namespace Agency.Huddle.App.Acp;

/// <summary>One top-level frontmatter field parsed from a Persona file, in file order.</summary>
/// <param name="Key">The field's YAML key, exactly as written (not title-cased).</param>
/// <param name="Value">
/// The field's value as a single display line: a quote-stripped scalar, a semicolon-joined
/// list, or a block scalar with any embedded line breaks collapsed to spaces.
/// </param>
public sealed record PersonaFrontmatterField(string Key, string Value)
{
    /// <summary>
    /// The items of a field written as a block list or a bracketed flow list, in order and quote-stripped;
    /// empty for any other field. <see cref="Value"/> is these items joined with <c>"; "</c>, which a reader
    /// cannot split back safely when an item itself contains a semicolon.
    /// </summary>
    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>Value equality that compares <see cref="Items"/> item by item rather than by reference.</summary>
    /// <param name="other">The field to compare with.</param>
    /// <returns><see langword="true"/> when the key, the value and every item match.</returns>
    public bool Equals(PersonaFrontmatterField? other) =>
        other is not null
        && string.Equals(this.Key, other.Key, StringComparison.Ordinal)
        && string.Equals(this.Value, other.Value, StringComparison.Ordinal)
        && this.Items.SequenceEqual(other.Items, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(this.Key),
        StringComparer.Ordinal.GetHashCode(this.Value),
        this.Items.Count);
}

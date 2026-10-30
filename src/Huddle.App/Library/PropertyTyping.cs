namespace Agency.Huddle.App.Library;

/// <summary>
/// Gives each read property its final type (front matter properties spec D-2 and D-3): the fixed defaults for <c>tags</c>, <c>aliases</c> and
/// <c>cssclasses</c>, then the root's types file, then the type the value implied. A value that does not fit an assigned type becomes a Mismatch.
/// Pure.
/// </summary>
internal static class PropertyTyping
{
    /// <summary>Types <paramref name="properties"/>; the order and the count are unchanged.</summary>
    /// <param name="properties">The properties as <see cref="NoteProperties.Read"/> gave them, typed by inference.</param>
    /// <param name="types">The root's assigned types, keyed case-insensitively.</param>
    internal static IReadOnlyList<NoteProperty> Apply(IReadOnlyList<NoteProperty> properties, IReadOnlyDictionary<string, NotePropertyKind> types)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(types);

        List<NoteProperty> typed = new(properties.Count);
        foreach (NoteProperty property in properties)
        {
            NotePropertyKind? assigned = FixedDefault(property.Key);
            if (assigned is null && types.TryGetValue(property.Key, out NotePropertyKind fromFile))
            {
                assigned = fromFile;
            }

            typed.Add(assigned is { } kind ? Retype(property, kind) : property);
        }

        return typed;
    }

    /// <summary>The types the types file cannot change (D-2 step 2). The key is matched as written, as the tag reader matches <c>tags</c>.</summary>
    private static NotePropertyKind? FixedDefault(string key) => key switch
    {
        "tags" => NotePropertyKind.Tags,
        "aliases" => NotePropertyKind.Aliases,
        "cssclasses" => NotePropertyKind.List,
        _ => null,
    };

    /// <summary>Applies <paramref name="assigned"/> to <paramref name="property"/> by D-3's fit rules.</summary>
    private static NoteProperty Retype(NoteProperty property, NotePropertyKind assigned)
    {
        if (assigned == NotePropertyKind.Tags || property.Shape == NotePropertyShape.Empty)
        {
            return property with { Kind = assigned };
        }

        return assigned switch
        {
            NotePropertyKind.List or NotePropertyKind.Aliases => AsList(property, assigned),
            _ => Fits(property, assigned) ? property with { Kind = assigned } : property with { Kind = NotePropertyKind.Mismatch, Expected = assigned },
        };
    }

    /// <summary>A list type takes a list of scalars, or a single scalar as a one-item list.</summary>
    private static NoteProperty AsList(NoteProperty property, NotePropertyKind assigned) => property.Shape switch
    {
        NotePropertyShape.List => property with { Kind = assigned },
        NotePropertyShape.Scalar => property with { Kind = assigned, Shape = NotePropertyShape.List, Items = [property.Value], Value = string.Empty },
        _ => property with { Kind = NotePropertyKind.Mismatch, Expected = assigned },
    };

    /// <summary>Whether a scalar fits one of the scalar types; a list or a nested value fits none of them.</summary>
    private static bool Fits(NoteProperty property, NotePropertyKind assigned)
    {
        if (property.Shape != NotePropertyShape.Scalar)
        {
            return false;
        }

        string value = property.Value;
        return assigned switch
        {
            NotePropertyKind.Text => true,
            NotePropertyKind.Number => NotePropertyValues.IsNumber(value),
            NotePropertyKind.Checkbox => NotePropertyValues.TryParseBoolean(value, out _),
            NotePropertyKind.Date => NotePropertyValues.TryParseDate(value, out _),
            NotePropertyKind.DateTime => NotePropertyValues.TryParseDateTime(value, out _, out _) || NotePropertyValues.TryParseDate(value, out _),
            _ => false,
        };
    }
}

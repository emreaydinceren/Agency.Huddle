using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins <see cref="PropertyTyping"/>: D-2's order (the types file, the fixed defaults, the value) and D-3's fit rules.</summary>
public sealed class PropertyTypingTests
{
    private static IReadOnlyList<NoteProperty> Typed(string yaml, params (string Key, NotePropertyKind Kind)[] assigned)
    {
        Dictionary<string, NotePropertyKind> types = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, NotePropertyKind kind) in assigned)
        {
            types[key] = kind;
        }

        return PropertyTyping.Apply(NoteProperties.Read(NoteFrontmatter.Locate("---\n" + yaml + "\n---\n")), types);
    }

    private static NoteProperty One(string yaml, NotePropertyKind assigned) => Assert.Single(Typed(yaml, ("k", assigned)));

    /// <summary>FP-T3: the types file wins over what the value implies, and a key matches case-insensitively.</summary>
    [Fact]
    public void Apply_TypesFileWinsOverInference()
    {
        Assert.Equal(NotePropertyKind.Text, Assert.Single(Typed("pages: 320", ("pages", NotePropertyKind.Text))).Kind);
        Assert.Equal(NotePropertyKind.Number, Assert.Single(Typed("pages: \"7\"", ("PAGES", NotePropertyKind.Number))).Kind);
    }

    /// <summary>FP-T3: <c>tags</c>, <c>aliases</c> and <c>cssclasses</c> keep their fixed types, whatever the file says.</summary>
    [Fact]
    public void Apply_FixedDefaults_CannotBeChangedByTheFile()
    {
        IReadOnlyList<NoteProperty> typed = Typed(
            "tags: [a]\naliases: [b]\ncssclasses: [c]",
            ("TAGS", NotePropertyKind.Text),
            ("aliases", NotePropertyKind.Number),
            ("cssclasses", NotePropertyKind.Checkbox));

        Assert.Equal([NotePropertyKind.Tags, NotePropertyKind.Aliases, NotePropertyKind.List], typed.Select(static p => p.Kind));
    }

    /// <summary>FP-T3: a key the file does not name falls through to inference.</summary>
    [Fact]
    public void Apply_UnassignedKey_FallsThroughToInference()
    {
        Assert.Equal(NotePropertyKind.Checkbox, Assert.Single(Typed("done: true", ("other", NotePropertyKind.Text))).Kind);
    }

    /// <summary>FP-T4: a value that fits its assigned type takes the type.</summary>
    [Theory]
    [InlineData("k: hello", NotePropertyKind.Text)]
    [InlineData("k: 320", NotePropertyKind.Text)]
    [InlineData("k: [a, b]", NotePropertyKind.List)]
    [InlineData("k: single", NotePropertyKind.List)]
    [InlineData("k: [a]", NotePropertyKind.Aliases)]
    [InlineData("k: single", NotePropertyKind.Aliases)]
    [InlineData("k: 320", NotePropertyKind.Number)]
    [InlineData("k: \"320\"", NotePropertyKind.Number)]
    [InlineData("k: 1.50", NotePropertyKind.Number)]
    [InlineData("k: true", NotePropertyKind.Checkbox)]
    [InlineData("k: \"false\"", NotePropertyKind.Checkbox)]
    [InlineData("k: 2024-01-15", NotePropertyKind.Date)]
    [InlineData("k: \"2024-01-15\"", NotePropertyKind.Date)]
    [InlineData("k: 2024-01-15T09:00", NotePropertyKind.DateTime)]
    [InlineData("k: 2024-01-15", NotePropertyKind.DateTime)]
    [InlineData("k: {a: 1}", NotePropertyKind.Tags)]
    [InlineData("k: [a, b]", NotePropertyKind.Tags)]
    public void Apply_FittingValue_TakesTheAssignedType(string yaml, NotePropertyKind assigned) =>
        Assert.Equal(assigned, One(yaml, assigned).Kind);

    /// <summary>FP-T4: a value that does not fit is a Mismatch that names the type it expected; nothing is coerced.</summary>
    [Theory]
    [InlineData("k: [a, b]", NotePropertyKind.Text)]
    [InlineData("k: {a: 1}", NotePropertyKind.Text)]
    [InlineData("k:\n  a: 1", NotePropertyKind.List)]
    [InlineData("k: {a: 1}", NotePropertyKind.Aliases)]
    [InlineData("k: next week", NotePropertyKind.Number)]
    [InlineData("k: [1, 2]", NotePropertyKind.Number)]
    [InlineData("k: yes", NotePropertyKind.Checkbox)]
    [InlineData("k: next week", NotePropertyKind.Date)]
    [InlineData("k: 2024-01-15T09:00", NotePropertyKind.Date)]
    [InlineData("k: 2026-02-30", NotePropertyKind.Date)]
    [InlineData("k: next week", NotePropertyKind.DateTime)]
    public void Apply_NonFittingValue_IsAMismatch(string yaml, NotePropertyKind assigned)
    {
        NoteProperty property = One(yaml, assigned);

        Assert.Equal(NotePropertyKind.Mismatch, property.Kind);
        Assert.Equal(assigned, property.Expected);
    }

    /// <summary>FP-T4: a Mismatch keeps the value as written, so it can be shown as it is.</summary>
    [Fact]
    public void Apply_Mismatch_KeepsTheRawValue()
    {
        NoteProperty property = One("k: next week", NotePropertyKind.Date);

        Assert.Equal("next week", property.Value);
    }

    /// <summary>FP-T4: an empty value fits every type and keeps it.</summary>
    [Theory]
    [InlineData(NotePropertyKind.Text)]
    [InlineData(NotePropertyKind.List)]
    [InlineData(NotePropertyKind.Aliases)]
    [InlineData(NotePropertyKind.Number)]
    [InlineData(NotePropertyKind.Checkbox)]
    [InlineData(NotePropertyKind.Date)]
    [InlineData(NotePropertyKind.DateTime)]
    [InlineData(NotePropertyKind.Tags)]
    public void Apply_EmptyValue_FitsEveryType(NotePropertyKind assigned)
    {
        NoteProperty property = One("k:", assigned);

        Assert.Equal(assigned, property.Kind);
        Assert.Equal(NotePropertyShape.Empty, property.Shape);
    }

    /// <summary>FP-T4: a single scalar assigned a list type is read as a one-item list.</summary>
    [Fact]
    public void Apply_ScalarAssignedList_IsOneItem()
    {
        Assert.Equal(["single"], One("k: single", NotePropertyKind.List).Items);
    }
}

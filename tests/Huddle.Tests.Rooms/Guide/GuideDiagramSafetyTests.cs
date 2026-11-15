using System.Xml.Linq;
using Agency.Huddle.App.Guide;

namespace Agency.Huddle.Tests.Guide;

/// <summary>
/// Holds every shipped diagram to ADR-0087 D-4. A diagram is written into the page as markup, so this test is what
/// keeps "never SVG" true in spirit: no script, no way to load anything, no colour of its own, and ids that cannot
/// collide with another diagram in the same Message.
/// </summary>
public sealed class GuideDiagramSafetyTests
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";

    /// <summary>Elements that can run code, load something or restyle the page; none may appear in a diagram.</summary>
    private static readonly HashSet<string> ForbiddenElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "foreignObject", "image", "iframe", "object", "embed", "style", "a",
        "set", "animate", "animateMotion", "animateTransform", "feImage",
    };

    /// <summary>Attributes that paint a colour; a diagram leaves colour to <c>guide.css</c>.</summary>
    private static readonly HashSet<string> ColourAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fill", "stroke", "color", "stop-color", "flood-color", "lighting-color",
    };

    /// <summary>The id of every shipped diagram.</summary>
    public static TheoryData<string> DiagramIds => [.. GuideDiagrams.All.Keys];

    /// <summary>A diagram is an <c>svg</c> root with a <c>viewBox</c>, <c>role="img"</c> and a <c>title</c> first.</summary>
    /// <param name="id">The diagram's id.</param>
    [Theory]
    [MemberData(nameof(DiagramIds))]
    public void Diagram_RootIsAnAccessibleSvg(string id)
    {
        XElement root = GuideDiagramSafetyTests.Parse(id);

        Assert.Equal(GuideDiagramSafetyTests.SvgNamespace + "svg", root.Name);
        Assert.NotNull(root.Attribute("viewBox"));
        Assert.Equal("img", (string?)root.Attribute("role"));
        XElement? first = root.Elements().FirstOrDefault();
        Assert.Equal(GuideDiagramSafetyTests.SvgNamespace + "title", first?.Name);
        Assert.False(string.IsNullOrWhiteSpace(first?.Value));
    }

    /// <summary>No element that can run code, load a resource or restyle the page.</summary>
    /// <param name="id">The diagram's id.</param>
    [Theory]
    [MemberData(nameof(DiagramIds))]
    public void Diagram_HasNoForbiddenElement(string id)
    {
        List<string> found = GuideDiagramSafetyTests.Parse(id).DescendantsAndSelf()
            .Select(static element => element.Name.LocalName)
            .Where(static name => GuideDiagramSafetyTests.ForbiddenElements.Contains(name))
            .ToList();

        Assert.Empty(found);
    }

    /// <summary>
    /// No event handler, no <c>style</c>, no colour or font of its own, and no reference outside the diagram: an
    /// <c>href</c> or <c>url(…)</c> may only point at a <c>#</c> fragment.
    /// </summary>
    /// <param name="id">The diagram's id.</param>
    [Theory]
    [MemberData(nameof(DiagramIds))]
    public void Diagram_HasNoForbiddenAttribute(string id)
    {
        List<string> problems = [];
        foreach (XElement element in GuideDiagramSafetyTests.Parse(id).DescendantsAndSelf())
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                string? problem = GuideDiagramSafetyTests.ProblemWith(attribute);
                if (problem is not null)
                {
                    problems.Add($"<{element.Name.LocalName} {attribute.Name.LocalName}=\"{attribute.Value}\">: {problem}");
                }
            }
        }

        Assert.Empty(problems);
    }

    /// <summary>Every <c>id</c> starts with the diagram's own id and a hyphen, so two diagrams in one Message never share one.</summary>
    /// <param name="id">The diagram's id.</param>
    [Theory]
    [MemberData(nameof(DiagramIds))]
    public void Diagram_EveryIdIsPrefixedWithTheDiagramId(string id)
    {
        List<string> unprefixed = GuideDiagramSafetyTests.Parse(id).DescendantsAndSelf()
            .Select(static element => (string?)element.Attribute("id"))
            .OfType<string>()
            .Where(value => !value.StartsWith(id + "-", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(unprefixed);
    }

    /// <summary>Parses a diagram's SVG. <see cref="XDocument.Parse(string)"/> refuses a DTD, so none can hide in one.</summary>
    /// <param name="id">The diagram's id.</param>
    private static XElement Parse(string id) => XDocument.Parse(GuideDiagrams.All[id].Svg).Root
        ?? throw new InvalidOperationException($"Diagram '{id}' has no root element.");

    /// <summary>What is wrong with <paramref name="attribute"/>, or null when nothing is.</summary>
    /// <param name="attribute">An attribute of a diagram element.</param>
    private static string? ProblemWith(XAttribute attribute)
    {
        string name = attribute.Name.LocalName;
        string value = attribute.Value.Trim();
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            return "an event handler";
        }

        if (string.Equals(name, "style", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("font", StringComparison.OrdinalIgnoreCase))
        {
            return "styling belongs in guide.css";
        }

        if (string.Equals(name, "href", StringComparison.OrdinalIgnoreCase) && !value.StartsWith('#'))
        {
            return "a reference outside the diagram";
        }

        if (value.Contains("url(", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("url(#", StringComparison.OrdinalIgnoreCase))
        {
            return "a url() outside the diagram";
        }

        bool colourAllowed = string.Equals(value, "none", StringComparison.Ordinal) || string.Equals(value, "currentColor", StringComparison.Ordinal);
        if (GuideDiagramSafetyTests.ColourAttributes.Contains(name) && !colourAllowed)
        {
            return "a colour of its own; use a g-* class";
        }

        return null;
    }
}

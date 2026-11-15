using System.Xml.Linq;
using Agency.Huddle.App.Guide;

namespace Agency.Huddle.Tests.Guide;

/// <summary>Pins that the catalog in <c>Guide/guide.json</c> and the embedded <c>.svg</c> files agree (ADR-0087 D-1).</summary>
public sealed class GuideDiagramCatalogTests
{
    /// <summary>The catalog loads and holds the first diagram, the layers of the Second Brain.</summary>
    [Fact]
    public void All_LoadsTheCatalog_WithTheLayersDiagram()
    {
        Assert.True(GuideDiagrams.TryGet("layers", out GuideDiagram? layers));
        Assert.Equal("layers", layers.Id);
        Assert.Equal("The layers of your second brain", layers.Title);
        Assert.Equal("svg", XDocument.Parse(layers.Svg).Root?.Name.LocalName);
    }

    /// <summary>Every entry has a title and a "show when" line, which the Chief of Staff's index is built from.</summary>
    [Fact]
    public void All_EveryEntryHasATitleAndAShowWhenLine()
    {
        List<string> incomplete = GuideDiagrams.All.Values
            .Where(static diagram => string.IsNullOrWhiteSpace(diagram.Title) || string.IsNullOrWhiteSpace(diagram.ShowWhen))
            .Select(static diagram => diagram.Id)
            .ToList();

        Assert.Empty(incomplete);
    }

    /// <summary>No SVG is embedded without a catalog entry, so a diagram cannot ship that nothing can name.</summary>
    [Fact]
    public void All_EveryEmbeddedSvgIsInTheCatalog()
    {
        List<string> orphans = typeof(GuideDiagrams).Assembly.GetManifestResourceNames()
            .Select(static name => name.Replace('\\', '/'))
            .Where(static name => name.StartsWith("Guide/", StringComparison.Ordinal) && name.EndsWith(".svg", StringComparison.Ordinal))
            .Select(static name => name["Guide/".Length..^".svg".Length])
            .Where(static id => !GuideDiagrams.All.ContainsKey(id))
            .ToList();

        Assert.Empty(orphans);
    }

    /// <summary>An id the catalog does not hold is reported missing, not thrown.</summary>
    [Fact]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        Assert.False(GuideDiagrams.TryGet("no-such-diagram", out _));
    }
}

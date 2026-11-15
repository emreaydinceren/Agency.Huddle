using System.Text;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Agency.Huddle.App.Guide;

namespace Agency.Huddle.App.Services;

/// <summary>Built-in diagrams (ADR-0087): an image whose address is <c>guide:&lt;id&gt;</c> is drawn inline.</summary>
public static partial class MarkdownRenderer
{
    /// <summary>
    /// Replaces every image addressed <c>guide:&lt;id&gt;</c> in <paramref name="document"/>: with the diagram when
    /// <see cref="GuideDiagrams"/> holds the id, and otherwise with its alt text (or the id when it has none), so an
    /// unknown id never draws a broken image (ADR-0087 D-1, D-2). Runs before every other pass, so no later rewrite
    /// ever sees a <c>guide:</c> address.
    /// </summary>
    /// <param name="document">The parsed Markdown document, mutated in place.</param>
    private static void InlineGuideDiagrams(MarkdownDocument document)
    {
        List<(LinkInline Image, string Id)> images = [];
        foreach (LinkInline link in document.Descendants<LinkInline>())
        {
            if (link is { IsImage: true, Url: { } url } && url.StartsWith(GuideDiagrams.Scheme, StringComparison.Ordinal))
            {
                images.Add((link, url[GuideDiagrams.Scheme.Length..]));
            }
        }

        foreach ((LinkInline image, string id) in images)
        {
            Inline replacement = GuideDiagrams.TryGet(id, out GuideDiagram? diagram)
                ? new GuideDiagramInline(diagram)
                : new LiteralInline(MarkdownRenderer.FallbackTextOf(image, id));
            // copyChildren: false, or the alt text would be moved in beside the diagram.
            image.ReplaceBy(replacement, copyChildren: false);
        }
    }

    /// <summary>What an unknown diagram renders as: its alt text, or <paramref name="id"/> when it has none.</summary>
    /// <param name="image">The image link.</param>
    /// <param name="id">The id it named.</param>
    private static string FallbackTextOf(LinkInline image, string id)
    {
        string alt = MarkdownRenderer.AltTextOf(image);
        return alt.Length > 0 ? alt : id;
    }

    /// <summary>The plain text of an image's alt text: its literal descendants, concatenated.</summary>
    /// <param name="image">The image link.</param>
    private static string AltTextOf(LinkInline image)
    {
        StringBuilder text = new();
        foreach (LiteralInline literal in image.Descendants<LiteralInline>())
        {
            text.Append(literal.Content.ToString());
        }

        return text.ToString().Trim();
    }

    /// <summary>A built-in diagram in Markdown text, rendered by <see cref="GuideDiagramInlineRenderer"/>.</summary>
    /// <param name="diagram">The diagram the image named.</param>
    private sealed class GuideDiagramInline(GuideDiagram diagram) : LeafInline
    {
        /// <summary>The diagram the image named.</summary>
        internal GuideDiagram Diagram { get; } = diagram;
    }

    /// <summary>
    /// Writes a <see cref="GuideDiagramInline"/> as <c>&lt;span class="guide-diagram"&gt;</c> holding the diagram's
    /// SVG. The SVG is the catalog's, embedded in this assembly and held to ADR-0087 D-4 by a test; nothing from
    /// the Message reaches the page as markup. With HTML output off, only the title is written.
    /// </summary>
    private sealed class GuideDiagramInlineRenderer : HtmlObjectRenderer<GuideDiagramInline>
    {
        /// <summary>Writes <paramref name="obj"/>'s diagram.</summary>
        /// <param name="renderer">The HTML renderer being written to.</param>
        /// <param name="obj">The diagram to write.</param>
        protected override void Write(Markdig.Renderers.HtmlRenderer renderer, GuideDiagramInline obj)
        {
            if (!renderer.EnableHtmlForInline)
            {
                renderer.WriteEscape(obj.Diagram.Title);
                return;
            }

            renderer.Write("<span class=\"guide-diagram\" data-guide-id=\"").WriteEscape(obj.Diagram.Id).Write("\">");
            renderer.Write(obj.Diagram.Svg);
            renderer.Write("</span>");
        }
    }
}

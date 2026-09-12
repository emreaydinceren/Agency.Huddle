using Markdig;

namespace Agency.Huddle.App.Services;

public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    public static string ToHtml(string markdown)
    {
        using var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer)
        {
            LinkRewriter = url => IsSafe(url) ? url : "#",
        };
        Pipeline.Setup(renderer);

        var document = Markdown.Parse(markdown, Pipeline);
        renderer.Render(document);

        return writer.ToString();
    }

    private static bool IsSafe(string? url)
    {
        return url is not null &&
            (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));
    }
}
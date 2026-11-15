namespace Agency.Huddle.App.Guide;

/// <summary>One built-in diagram (ADR-0087): an SVG shipped in the assembly, named by its catalog id.</summary>
/// <param name="Id">The id Markdown names it by, as in <c>![alt](guide:layers)</c>.</param>
/// <param name="Title">What the diagram shows, in a few words; its accessible name.</param>
/// <param name="ShowWhen">When an Agent should show it, for the Agent's index.</param>
/// <param name="Svg">The diagram's markup, from <c>&lt;svg</c> to <c>&lt;/svg&gt;</c>.</param>
internal sealed record GuideDiagram(string Id, string Title, string ShowWhen, string Svg);

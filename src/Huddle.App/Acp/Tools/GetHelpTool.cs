namespace Agency.Huddle.App.Acp.Tools;

using System.Text;
using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Hooks;

/// <summary>
/// Explains the chat application and lists every App Tool available to the calling Agent, so the
/// rest of the tool surface can be discovered at run time instead of being carried in every prompt.
/// </summary>
/// <remarks>
/// <para>
/// This is the progressive-discovery entry point. The system prompt names only this tool; this tool
/// names the others. That keeps the per-turn cost of the prompt flat as tools are added, and gives
/// one place — rather than two — to describe how a Room behaves.
/// </para>
/// <para>
/// Every tool is listed with its full <c>mcp__team__</c> prefix, for the same reason
/// <see cref="SystemPromptComposer"/> uses it: a model in deferred-tool mode looks a name up
/// verbatim and reports that no such tool exists when it is named loosely. That prefix is supplied
/// by the caller as <c>toolNamePrefix</c>, computed from the same tool-server name it hands to
/// <c>AppToolServer</c> — never typed into a hook's template, and never a second, independently
/// hard-coded copy of that literal here.
/// </para>
/// <para>
/// Every piece of this tool's own model-facing text — its <see cref="Description"/> and every
/// section of <see cref="BuildHelp"/> — is a hook, resolved through <see cref="IHookSource"/>. The
/// help body is assembled by joining <c>getHelp.intro</c> through <c>getHelp.budget</c> with a blank
/// line, then a tools block: <c>getHelp.toolsHeader</c>, one <c>getHelp.toolEntry</c> per tool (each
/// of which already carries its own trailing blank line), then <c>getHelp.footer</c>.
/// </para>
/// <para>
/// One exception to that, and it is the point of <see cref="catalog"/>: the tool <em>descriptions</em>
/// quoted in the TOOLS block are captured once, here, rather than re-read per call. Every
/// <c>tool.*.description</c> hook is badged <b>Next session</b> on the settings page, and this type
/// is built once per session — so reading them live let an edit reach a Teammate that was already
/// running, which the badge promises it cannot. MCP's own <c>tools/list</c> honours that badge by
/// construction, because it is sent once at session start; this is the surface that did not, and it
/// is the one that decides what a model knows, since progressive discovery means the system prompt
/// names tools without describing them.
/// </para>
/// </remarks>
internal sealed class GetHelpTool : IAppTool
{
    private static readonly IReadOnlyDictionary<string, string> NoValues = new Dictionary<string, string>();

    private readonly IReadOnlyList<CatalogEntry> catalog;
    private readonly IHookSource hooks;
    private readonly string toolNamePrefix;

    /// <summary>Initialises a new instance of the <see cref="GetHelpTool"/> class.</summary>
    /// <param name="otherTools">
    /// Every other tool offered to the same session. Its names and descriptions are copied on the
    /// way in, so neither the caller's array nor a later hook edit can change what this tool
    /// reports. This tool is not in the list and adds itself.
    /// </param>
    /// <param name="hooks">Resolves each hook's current text — a configured override, or the <see cref="HookCatalog"/> default.</param>
    /// <param name="toolNamePrefix">
    /// The full <c>mcp__&lt;server&gt;__</c> prefix every tool name carries in this help text, e.g.
    /// <c>"mcp__team__"</c>. Supplied by the caller — this type never hard-codes it.
    /// </param>
    public GetHelpTool(IReadOnlyList<IAppTool> otherTools, IHookSource hooks, string toolNamePrefix)
    {
        ArgumentNullException.ThrowIfNull(otherTools);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolNamePrefix);

        this.hooks = hooks;
        this.toolNamePrefix = toolNamePrefix;

        // this.Description reads a hook, so hooks must already be assigned. This tool goes first for
        // the same reason AllTools once yielded it first: it is offered first.
        this.catalog =
        [
            new CatalogEntry(this.Name, this.Description),
            .. otherTools.Select(static tool => new CatalogEntry(tool.Name, tool.Description)),
        ];
    }

    /// <inheritdoc />
    public string Name => "get_help";

    /// <inheritdoc />
    public string Description => this.hooks.Render("tool.getHelp.description", NoValues);

    /// <inheritdoc />
    public JsonObject InputSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray(),
    };

    /// <inheritdoc />
    public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return Task.FromResult(this.BuildHelp());
    }

    /// <summary>Builds the help text: how the chat works, then the catalog of tools.</summary>
    /// <returns>The help text handed back to the model.</returns>
    private string BuildHelp()
    {
        string[] sections =
        [
            this.hooks.Render("getHelp.intro", NoValues),
            this.hooks.Render("getHelp.rooms", NoValues),
            this.hooks.Render("getHelp.messages", NoValues),
            this.hooks.Render("getHelp.mentions", NoValues),
            this.hooks.Render("getHelp.replying", NoValues),
            this.hooks.Render("getHelp.budget", NoValues),
            this.BuildToolsSection(),
        ];

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// Builds the TOOLS section: its heading, one <c>getHelp.toolEntry</c> per tool (each already
    /// ending in a blank line), then the footer.
    /// </summary>
    /// <returns>The joined tools section, ready to take its place among <see cref="BuildHelp"/>'s other sections.</returns>
    private string BuildToolsSection()
    {
        var builder = new StringBuilder();

        builder.Append(this.hooks.Render("getHelp.toolsHeader", NoValues)).Append('\n');

        foreach (var entry in this.catalog)
        {
            var values = new Dictionary<string, string>
            {
                ["{{toolName}}"] = this.toolNamePrefix + entry.Name,
                ["{{toolDescription}}"] = entry.Description,
            };

            builder.Append(this.hooks.Render("getHelp.toolEntry", values));
        }

        builder.Append(this.hooks.Render("getHelp.footer", NoValues));

        return builder.ToString();
    }

    /// <summary>
    /// One tool as this session will report it: the name and the description read when the session's
    /// tool list was built, not as they read now. See the <see cref="GetHelpTool"/> remarks for why
    /// the description is frozen while the surrounding help prose is not.
    /// </summary>
    /// <param name="Name">The tool's bare name, without the <c>mcp__&lt;server&gt;__</c> prefix.</param>
    /// <param name="Description">The tool's job description as of this session's start.</param>
    private sealed record CatalogEntry(string Name, string Description);
}

namespace Agency.Huddle.App.Acp;

/// <summary>
/// One ACP agent this installation can launch, and how to launch it. Public because
/// <c>TeammateCard</c> binds it to a Razor <c>[Parameter]</c>, and Razor generates component
/// classes as <c>public</c> — see Spec §6.1 and §6.6.
/// </summary>
/// <param name="Id">Stable identifier, stored in a Persona's frontmatter and compared <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
/// <param name="DisplayName">The name rendered on the Teammate card.</param>
/// <param name="Description">Optional human-facing text, for example "cloud, spends money per turn".</param>
/// <param name="Command">The executable or interpreter to launch.</param>
/// <param name="Args">Explicit process arguments, or <see langword="null"/> to fall through to <paramref name="AdapterPath"/> or the locator.</param>
/// <param name="AdapterPath">An explicit adapter script/executable path, used when <paramref name="Args"/> is empty.</param>
/// <param name="UsesToolNamePrefix">Whether model-facing tool names carry the <c>mcp__team__</c> prefix.</param>
/// <param name="EnvironmentOverrides">
/// Environment variables set on the launched adapter process, or <see langword="null"/> when the
/// Adapter needs none. Defaults to <see langword="null"/> so every existing positional call site
/// keeps compiling unchanged.
/// </param>
/// <param name="ReadsFiles">
/// Whether this Adapter's agent process can read files at all — FC §6.11. Defaults to
/// <see langword="true"/> so every existing positional call site keeps compiling unchanged; an
/// Adapter with no file tools (<c>agency-acp</c>) sets it <see langword="false"/>, which turns off
/// the Watched Folder list, <c>watch_folder</c>/<c>unwatch_folder</c> and frontmatter <c>watches</c>
/// for every Persona on that Adapter.
/// </param>
/// <param name="IsolateUserSettings">
/// Whether a session on this Adapter should be started with the isolation <c>_meta</c> (RS §6.10
/// "Recommended", finding P-11): <c>settingSources: ["project", "local"]</c> and
/// <c>settings.autoMemoryEnabled: false</c>, merged in by <c>DotAcpAgentHostFactory</c>. Defaults
/// to <see langword="false"/> so every existing positional call site keeps compiling unchanged; the
/// synthesised legacy profile sets it <see langword="true"/>, because <c>agency-acp</c> ignores the
/// <c>claudeCode</c>-shaped entry anyway (RS §6.10) and a stock install is exactly the case FC
/// §6.15's memory feature needs it for. <b>Unverified until Task 14.3.m passes</b> - correction item
/// 22: claude-agent-acp's <c>settings</c> option REPLACES its own computed settings rather than
/// merging, so sending <c>autoMemoryEnabled: false</c> also drops any <c>CLAUDE_MODEL_CONFIG</c>
/// model override that session would otherwise have carried; <c>settingSources</c> merges instead
/// of replacing.
/// </param>
/// <param name="SessionPerRoom">
/// Whether this Adapter Profile gives each Room its own Room Session (RS §6.12). Defaults to
/// <see langword="true"/> (D28, finding P-9): every configured Adapter and the synthesised legacy
/// profile both get their own Room Session per Room unless a Profile opts out. With
/// <see langword="false"/>, every Room maps to one shared Room Session and the shared-session
/// system prompt is used - the mode a configured <c>agency-acp</c> entry must stay in until V-5
/// (RS §6.12: it reports <c>loadSession: false</c> and nothing is known of its resume support).
/// </param>
/// <param name="Commands">
/// The names of the Adapter commands (<c>/compact</c>) a Human may run on a Teammate on this Adapter,
/// from <c>Team:Acp:Adapters:*:Commands</c>; <see langword="null"/> means none. A Teammate offers a
/// command only when its Adapter advertises it <i>and</i> this list names it (Commands spec §6.2).
/// Defaults to <see langword="null"/> so every existing positional call site keeps compiling unchanged.
/// </param>
public sealed record AdapterProfile(
    string Id,
    string DisplayName,
    string? Description,
    string Command,
    IReadOnlyList<string>? Args,
    string? AdapterPath,
    bool UsesToolNamePrefix,
    IReadOnlyDictionary<string, string>? EnvironmentOverrides = null,
    bool ReadsFiles = true,
    bool IsolateUserSettings = false,
    bool SessionPerRoom = true,
    IReadOnlyList<string>? Commands = null);

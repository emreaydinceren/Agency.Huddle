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
public sealed record AdapterProfile(
    string Id,
    string DisplayName,
    string? Description,
    string Command,
    IReadOnlyList<string>? Args,
    string? AdapterPath,
    bool UsesToolNamePrefix,
    IReadOnlyDictionary<string, string>? EnvironmentOverrides = null,
    bool ReadsFiles = true);

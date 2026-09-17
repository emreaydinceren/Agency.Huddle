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
public sealed record AdapterProfile(
    string Id,
    string DisplayName,
    string? Description,
    string Command,
    IReadOnlyList<string>? Args,
    string? AdapterPath,
    bool UsesToolNamePrefix);

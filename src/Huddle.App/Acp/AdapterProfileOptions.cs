namespace Agency.Huddle.App.Acp;

/// <summary>
/// The <c>ConfigurationBinder</c>-facing shape of one entry under <c>Team:Acp:Adapters</c>. Mutable
/// and public because the binder cannot reliably bind a positional record — see Spec §6.1.
/// <see cref="AdapterCatalog"/> projects each entry into an immutable <see cref="AdapterProfile"/>.
/// </summary>
public sealed class AdapterProfileOptions
{
    /// <summary>Stable identifier, compared <see cref="StringComparer.OrdinalIgnoreCase"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name rendered on the Teammate card. Falls back to <see cref="Id"/> when unset.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Optional human-facing text, for example "cloud, spends money per turn".</summary>
    public string? Description { get; set; }

    /// <summary>The executable or interpreter to launch.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Explicit process arguments. No initialiser: <c>ConfigurationBinder</c> appends bound array
    /// elements to an already-populated list/array property instead of replacing it, silently
    /// doubling the value (docs/agencyteam/rules.md, "Collection options need no initialiser").
    /// Consumers default when this is null or empty.
    /// </summary>
    public IReadOnlyList<string>? Args { get; set; }

    /// <summary>An explicit adapter script/executable path, used when <see cref="Args"/> is empty.</summary>
    public string? AdapterPath { get; set; }

    /// <summary>Whether model-facing tool names carry the <c>mcp__team__</c> prefix.</summary>
    public bool UsesToolNamePrefix { get; set; } = true;
}

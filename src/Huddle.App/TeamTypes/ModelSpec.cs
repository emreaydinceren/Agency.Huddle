namespace Agency.Huddle.App.TeamTypes;

/// <summary>The model a role asks for.</summary>
/// <param name="Tier">The model strength; Balanced when the manifest omits it.</param>
/// <param name="DifferentVendorFrom">The role ids whose model vendor this role must differ from, or null when omitted.</param>
internal sealed record ModelSpec(ModelTier Tier = ModelTier.Balanced, IReadOnlyList<string>? DifferentVendorFrom = null)
{
    /// <summary>The role ids whose model vendor this role must differ from; empty when omitted.</summary>
    public IReadOnlyList<string> DifferentVendorFrom { get; init; } = DifferentVendorFrom ?? [];
}

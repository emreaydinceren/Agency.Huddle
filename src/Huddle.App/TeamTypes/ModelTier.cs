namespace Agency.Huddle.App.TeamTypes;

/// <summary>The model strength a role asks for.</summary>
public enum ModelTier
{
    /// <summary>The strongest available model.</summary>
    Strongest,

    /// <summary>A balanced model. The default when a manifest omits the tier.</summary>
    Balanced,

    /// <summary>A fast, low-cost model.</summary>
    Fast,
}

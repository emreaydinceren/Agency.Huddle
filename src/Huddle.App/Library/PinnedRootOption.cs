namespace Agency.Huddle.App.Library;

/// <summary>A pinned root configuration entry, bound from <c>Team:Library:Roots:0:*</c>.</summary>
public sealed class PinnedRootOption
{
    /// <summary>The display name of the pinned root.</summary>
    public string Name { get; set; } = "";

    /// <summary>The path to the pinned root, absolute or relative to <c>DataDir</c>.</summary>
    public string Path { get; set; } = "";
}

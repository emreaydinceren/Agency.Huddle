namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Memory</c>, per Instructions and Memory §5.7.</summary>
public sealed class MemoryOptions
{
    /// <summary>The most bytes a memory file may hold after a <c>write</c>, <c>append</c> or <c>replace</c> through the memory tool; a larger result is refused. Zero or less means no cap.</summary>
    public int MaxFileBytes { get; set; } = 16384;
}

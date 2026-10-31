namespace Agency.Huddle.App.Gateway;

/// <summary>What a <see cref="HostActions"/> action did.</summary>
/// <param name="Done">Whether it happened.</param>
/// <param name="Error">Why it did not, or <see langword="null"/>.</param>
internal sealed record HostActionResult(bool Done, string? Error);

using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.SkillPacks.Files;

/// <summary>The outcome of reading one file: the record when its shape is usable, and the findings either way.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="Value">The record, or null when the file's shape is unusable.</param>
/// <param name="Findings">The shape findings.</param>
public sealed record ParseResult<T>(T? Value, IReadOnlyList<Finding> Findings) where T : class
{
    /// <summary>True when <see cref="Value"/> is set.</summary>
    public bool Ok => this.Value is not null;
}

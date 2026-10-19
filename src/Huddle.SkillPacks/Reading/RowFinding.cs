using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.SkillPacks.Reading;

/// <summary>A reader finding and the row it is about.</summary>
/// <param name="Kind">The row's table.</param>
/// <param name="Index">The row's index in its table.</param>
/// <param name="Finding">The finding.</param>
public sealed record RowFinding(RowKind Kind, int Index, Finding Finding);
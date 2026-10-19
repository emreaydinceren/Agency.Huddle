using Agency.Huddle.SkillPacks.Reading;

namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>One SKILLS.md row as checked.</summary>
/// <param name="Kind">The table the row is in.</param>
/// <param name="Index">The row's 0-based index in its table.</param>
/// <param name="LocalPath">The path the row resolved to, or the cell text when it resolved to none.</param>
/// <param name="Line">The row's first line in SKILLS.md; 0 for a virtual row.</param>
/// <param name="State">The state the row's file ended in.</param>
/// <param name="Errors">Errors that belong to the row.</param>
/// <param name="Warnings">Warnings that belong to the row.</param>
public sealed record RowCheck(RowKind Kind, int Index, string LocalPath, int Line, ItemCheckState State, int Errors, int Warnings);

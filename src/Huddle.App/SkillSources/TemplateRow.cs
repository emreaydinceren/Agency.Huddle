namespace Agency.Huddle.App.SkillSources;

/// <summary>One row the move writes.</summary>
/// <param name="LocalPath">The Local path cell: a plain path or <c>NAME: path</c>.</param>
/// <param name="OriginUrl">The Origin URL cell, or empty.</param>
/// <param name="LastUpdated">The Last updated cell (<c>yyyy-MM-dd</c>).</param>
/// <param name="Notes">The Notes cell.</param>
internal sealed record TemplateRow(string LocalPath, string OriginUrl, string LastUpdated, string Notes);

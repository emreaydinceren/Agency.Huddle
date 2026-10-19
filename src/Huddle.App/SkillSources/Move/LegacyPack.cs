namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>One of the Human's old Packs.</summary>
/// <param name="File">The file it came from.</param>
/// <param name="Id">The Pack id.</param>
/// <param name="Version">The version.</param>
/// <param name="Title">The title.</param>
/// <param name="AppliesTo">Role ids.</param>
/// <param name="ConsultWhenAdds">The consult phrase.</param>
/// <param name="Skills">The Skills: name, source (<c>owner/repo</c> or <c>builtin</c>) and path.</param>
internal sealed record LegacyPack(string File, string Id, string Version, string Title, IReadOnlyList<string> AppliesTo, string ConsultWhenAdds, IReadOnlyList<(string Name, string Source, string? Path)> Skills);

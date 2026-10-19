namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>A role a Pack's <c>appliesTo</c> may name.</summary>
/// <param name="Id">The role id.</param>
/// <param name="Title">The role's title.</param>
/// <param name="Description">One line on what the role does.</param>
/// <param name="SuggestedPacks">The Pack ids the hiring pool suggests for it (check R008).</param>
public sealed record RoleInfo(string Id, string Title, string Description, IReadOnlyList<string> SuggestedPacks);

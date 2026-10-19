using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.App.SkillSources;

/// <summary>A Pack or Skill that is not ready.</summary>
/// <param name="Name">The Pack id or Skill name.</param>
/// <param name="IsPack">True for a Pack.</param>
/// <param name="State">The state that makes it not ready.</param>
/// <param name="Reason">A one-line reason for a card.</param>
/// <param name="Title">The Pack title, or the Skill name again.</param>
/// <param name="CatalogId">The catalog that lists a missing Skill, or null.</param>
internal sealed record MissingItem(string Name, bool IsPack, ItemState State, string Reason, string Title, string? CatalogId);

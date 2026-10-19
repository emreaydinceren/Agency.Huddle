using Agency.Huddle.App.Acp;

namespace Agency.Huddle.App.TeamTypes;

/// <summary>A candidate Teammate template read from a team type's candidate file (Spec §Candidate).</summary>
/// <param name="Title">The role title.</param>
/// <param name="Alias">The optional alias.</param>
/// <param name="Skills">The skill ids the candidate starts with.</param>
/// <param name="Adapter">The optional adapter id.</param>
/// <param name="JobDescriptionFields">Every other non-reserved field, in file order.</param>
/// <param name="Stance">The body text that becomes the Teammate's stance.</param>
internal sealed record CandidateTemplate(
    string Title,
    string? Alias,
    IReadOnlyList<string> Skills,
    string? Adapter,
    IReadOnlyList<PersonaFrontmatterField> JobDescriptionFields,
    string Stance);

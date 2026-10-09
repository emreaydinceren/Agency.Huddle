namespace Agency.Huddle.App.Acp;

/// <summary>
/// What <see cref="SystemPromptComposer"/> needs to prime a hired Team member (Task 29, MS-7): who it is,
/// its role and Team, which colleagues to consult and which skills it holds with their triggers.
/// </summary>
/// <param name="Name">The member's Name.</param>
/// <param name="Role">The member's role, as the Team knows it.</param>
/// <param name="Team">The Team's name.</param>
/// <param name="Consult">The other members of the Team, the manager first.</param>
/// <param name="Skills">The member's resolved skills.</param>
/// <param name="Process">The Team's process priming, or <see langword="null"/> when the Team has no <c>Process.md</c>.</param>
internal sealed record RoleBlockInput(
    string Name,
    string Role,
    string Team,
    IReadOnlyList<ConsultLine> Consult,
    IReadOnlyList<SkillLine> Skills,
    ProcessBlockInput? Process = null);

/// <summary>One colleague in a <see cref="RoleBlockInput"/>.</summary>
/// <param name="Name">The colleague's Name.</param>
/// <param name="Role">The colleague's role.</param>
/// <param name="ConsultWhen">When to consult them, from their public job description; empty when they state none.</param>
internal sealed record ConsultLine(string Name, string Role, string ConsultWhen);

/// <summary>One skill in a <see cref="RoleBlockInput"/>.</summary>
/// <param name="Name">The skill's name.</param>
/// <param name="Description">The skill's description, which states its trigger.</param>
internal sealed record SkillLine(string Name, string Description);

/// <summary>The process priming of a hired member whose Team has a <c>Process.md</c> (Task 58, PR-4).</summary>
/// <param name="IsManager">Whether the member is the Team's manager, who receives the whole text rather than its path.</param>
/// <param name="Content">The whole <c>Process.md</c> text for the manager, or its Library path for every other member.</param>
internal sealed record ProcessBlockInput(bool IsManager, string Content);

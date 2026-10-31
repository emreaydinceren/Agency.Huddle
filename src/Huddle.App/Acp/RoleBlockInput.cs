namespace Agency.Huddle.App.Acp;

/// <summary>
/// What <see cref="SystemPromptComposer"/> needs to prime a hired Agent (Task 29, MS-7): who it is,
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

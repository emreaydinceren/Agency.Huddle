namespace Agency.Huddle.SkillPacks.Checking;

/// <summary>The counts of the <c>PACKS-VALIDATE</c> line (§6.4).</summary>
/// <param name="Rows">Rows checked.</param>
/// <param name="Packs">Packs among them.</param>
/// <param name="Skills">Distinct Skill names reached by the rows checked.</param>
/// <param name="Errors">Error findings.</param>
/// <param name="Warnings">Warning findings.</param>
public sealed record CheckSummary(int Rows, int Packs, int Skills, int Errors, int Warnings);

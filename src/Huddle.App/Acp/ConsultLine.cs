namespace Agency.Huddle.App.Acp;

/// <summary>One colleague in a <see cref="RoleBlockInput"/>.</summary>
/// <param name="Name">The colleague's Name.</param>
/// <param name="Role">The colleague's role.</param>
/// <param name="ConsultWhen">When to consult them, from their public job description; empty when they state none.</param>
internal sealed record ConsultLine(string Name, string Role, string ConsultWhen);

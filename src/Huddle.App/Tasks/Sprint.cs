namespace Agency.Huddle.App.Tasks;

/// <summary>A Sprint: a stable id and a display name (Sprints spec §7.2).</summary>
/// <param name="Id">The Sprint's id, which a Task holds.</param>
/// <param name="Name">The Sprint's name, shown wherever the id is drawn.</param>
internal sealed record Sprint(SprintId Id, string Name);

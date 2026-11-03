namespace Agency.Huddle.App.Teams;

/// <summary>One change a retro proposes: an exact-text replacement in one allowed file (RT-5).</summary>
/// <param name="Id">The change's id within its set: <c>1</c>, <c>2</c> or <c>3</c>.</param>
/// <param name="Title">A short name for the change; it becomes the register row's Finding.</param>
/// <param name="TargetFile">The file to edit: <c>AGENTS.md</c>, <c>_team.md</c> or an Agent's <c>Name.md</c>.</param>
/// <param name="Find">The text to replace; it must occur exactly once in the file when the change is accepted.</param>
/// <param name="Replace">The replacement; empty removes the found text.</param>
internal sealed record RetroChange(string Id, string Title, string TargetFile, string Find, string Replace);

namespace Agency.Huddle.App.Acp;

/// <summary>The names the Team Memory paragraphs give the two tools they mention, already carrying the Adapter's prefix.</summary>
/// <param name="Memory">The <c>memory</c> tool's name.</param>
/// <param name="Propose">The <c>propose_team_memory</c> tool's name.</param>
internal sealed record TeamMemoryTools(string Memory, string Propose);

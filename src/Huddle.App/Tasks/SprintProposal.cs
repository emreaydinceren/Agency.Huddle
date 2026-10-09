namespace Agency.Huddle.App.Tasks;

/// <summary>
/// A Sprint a manager has proposed and the Human has not yet approved (PL-4). It is only a request: no Sprint
/// exists until the Human approves it (ADR-0037).
/// </summary>
/// <param name="Id">The proposal's own id, also its file name.</param>
/// <param name="Name">The proposed Sprint's name.</param>
/// <param name="TaskIds">The Tasks proposed for the Sprint, as ids such as <c>PLAT-0042</c>, in the order given.</param>
/// <param name="Proposer">The name of the manager who proposed it.</param>
/// <param name="Team">The Team whose manager proposed it.</param>
internal sealed record SprintProposal(string Id, string Name, IReadOnlyList<string> TaskIds, string Proposer, string Team);

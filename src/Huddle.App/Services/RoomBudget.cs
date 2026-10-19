namespace Agency.Huddle.App.Services;

/// <summary>
/// One Room's Budget: how many agent-authored Messages it has taken since the Human last spoke there,
/// and how many it currently allows. <paramref name="Granted"/> is not the configured default — the
/// Human can extend it a Budget at a time — which is why both halves travel together everywhere they
/// go, including onto the wire.
/// </summary>
/// <param name="Used">Agent-authored Messages taken since the Human last spoke in this Room.</param>
/// <param name="Granted">How many this Room currently allows. Zero or less means uncapped.</param>
public sealed record RoomBudget(int Used, int Granted)
{
    /// <summary>Whether the Room has spent its Budget and will take no more until a Human speaks.</summary>
    public bool Exhausted => this.Granted > 0 && this.Used >= this.Granted;
}
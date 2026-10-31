namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>The Proof of a Task: its Definition of Done checklist, item by item.</summary>
/// <param name="Items">The items, Process items first and then the gates that joined on their own.</param>
public sealed record Proof(IReadOnlyList<ProofItem> Items)
{
    /// <summary>A Proof with no items, which is complete.</summary>
    public static Proof Empty { get; } = new([]);

    /// <summary>How many items are met.</summary>
    public int MetCount => this.Items.Count(static item => item.Met);

    /// <summary>How many items there are.</summary>
    public int Total => this.Items.Count;

    /// <summary>Whether every item is met; a Proof with no items is complete.</summary>
    public bool IsComplete => this.MetCount == this.Total;
}

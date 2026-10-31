using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Tasks.HumanGate;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Tasks.HumanGate;

/// <summary>An <see cref="IProofSource"/> that answers every Task with one settable Proof, for tests of the Human gate rules.</summary>
/// <param name="initial">The Proof returned until <see cref="Current"/> is changed.</param>
internal sealed class FixedProofSource(Proof initial) : IProofSource
{
    /// <summary>A Proof with four items, all met.</summary>
    public static Proof Complete { get; } = Make(4, 4);

    /// <summary>A Proof with four items, three met and the citation check open.</summary>
    public static Proof ThreeOfFour { get; } = Make(3, 4);

    /// <summary>The Proof returned for every Task.</summary>
    public Proof Current { get; set; } = initial;

    /// <summary>Returns <see cref="Current"/> whatever the Task.</summary>
    /// <param name="task">The Task, ignored.</param>
    /// <returns>The current Proof.</returns>
    public Proof For(TaskItem task) => this.Current;

    /// <summary>Builds a Proof of <paramref name="total"/> items of which the first <paramref name="met"/> are met.</summary>
    /// <param name="met">How many items are met.</param>
    /// <param name="total">How many items there are.</param>
    private static Proof Make(int met, int total)
    {
        List<ProofItem> items = [];
        for (int index = 0; index < total; index++)
        {
            bool isMet = index < met;
            string label = isMet ? $"Item {index + 1}" : "Citation check";
            items.Add(new ProofItem(label, isMet, null, null, ProofItemSource.Process));
        }

        return new Proof(items);
    }
}

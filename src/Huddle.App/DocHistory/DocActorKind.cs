namespace Agency.Huddle.App.DocHistory;

/// <summary>Who wrote a History entry: an Agent through <c>log_change</c>, or the Human through the Library (DocHistory spec §6.1).</summary>
public enum DocActorKind
{
    /// <summary>An Agent, through the <c>log_change</c> App Tool.</summary>
    Agent,

    /// <summary>The Human, through a save or a create in the Library.</summary>
    Human,
}

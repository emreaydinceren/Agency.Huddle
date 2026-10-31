namespace Agency.Huddle.App.Sidebar;

/// <summary>What a page is showing (Spec D-2): a closed set of immutable records, published by the page.</summary>
#pragma warning disable S1694 // Spec D-2 fixes a closed record hierarchy; an interface would lose the base record's value equality that Subject comparison relies on.
public abstract record PageSubject
{
    /// <summary>What the Subject is about, without its live state. Keys a mounted panel (Spec D-5).</summary>
    public abstract string Key { get; }
}

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Knows which Task id prefixes belong to a Team that exists or once existed, so a token such as an invoice
/// number (<c>INV-2043</c>) is not mistaken for a Task id.
/// </summary>
internal interface ITaskPrefixes
{
    /// <summary>Says whether a prefix was ever given to a Team, whether or not that Team still exists.</summary>
    /// <param name="prefix">The prefix part of a Task id, in any case.</param>
    /// <returns>True when a Team has carried this prefix.</returns>
    bool IsKnown(string prefix);
}

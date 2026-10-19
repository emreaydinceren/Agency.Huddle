namespace Agency.Huddle.App.Tasks;

/// <summary>The outcome of <see cref="SprintStore.Create(string)"/> (Sprints spec §8).</summary>
internal abstract record SprintCreateResult
{
    /// <summary>A new Sprint was appended to <c>sprints.md</c>.</summary>
    /// <param name="Sprint">The Sprint that was created.</param>
    public sealed record Created(Sprint Sprint) : SprintCreateResult;

    /// <summary>A Sprint with the same name, ignoring case, already existed; nothing was written.</summary>
    /// <param name="Sprint">The Sprint that already had the name.</param>
    public sealed record Existing(Sprint Sprint) : SprintCreateResult;

    /// <summary>The name was invalid, or the file could not be written; nothing was written.</summary>
    /// <param name="Problem">What is wrong, in prose a Human can read as-is.</param>
    public sealed record Refused(string Problem) : SprintCreateResult;
}

namespace Agency.Huddle.App.SkillSources.Move;

/// <summary>Places one staged file; the seam a crash test replaces (§6.13, "Crash safety").</summary>
internal interface IMoveFiles
{
    /// <summary>Moves <paramref name="from"/> to <paramref name="to"/>, creating the target's folder.</summary>
    /// <exception cref="IOException">The target exists and <paramref name="overwrite"/> is false, or the move failed.</exception>
    void Place(string from, string to, bool overwrite);
}

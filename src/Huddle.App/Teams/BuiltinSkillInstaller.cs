using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Treats a skill as installed when the <see cref="SkillStore"/> already holds it; Task 47 replaces
/// this with an installer that fetches imported skills.
/// </summary>
/// <param name="store">The skills on disk.</param>
internal sealed class BuiltinSkillInstaller(SkillStore store) : ISkillInstaller
{
    /// <inheritdoc />
    public Task<IReadOnlyList<SkillRef>> EnsureInstalledAsync(IReadOnlyList<SkillRef> skills, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(skills);

        IReadOnlyList<SkillRef> missing = [.. skills.Where(s => string.IsNullOrWhiteSpace(s.Name) || this.Missing(s.Name))];
        return Task.FromResult(missing);
    }

    /// <summary>Tells whether the store has no skill of that name.</summary>
    private bool Missing(string name)
    {
        return store.Get(name) is null;
    }
}

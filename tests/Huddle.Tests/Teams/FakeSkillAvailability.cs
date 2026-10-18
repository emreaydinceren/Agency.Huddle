using Agency.Huddle.App.SkillSources;
using Agency.Huddle.App.Teams;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.SkillPacks.Reading;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tests.Teams;

/// <summary>An <see cref="ISkillAvailability"/> set up by hand: Packs by id, a state per Skill name (any other name has
/// <see cref="Default"/>, <see cref="SkillAvailability.Present"/> unless set, as every Skill was before the registry), and an
/// optional problem per Pack.</summary>
internal sealed class FakeSkillAvailability : ISkillAvailability
{
    private readonly Dictionary<string, PackEntry> packs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (SkillAvailability State, string Detail)> skills = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (SkillAvailability State, string Detail)> packProblems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> subCategories = new(StringComparer.Ordinal);

    /// <summary>The state of a Skill name no <see cref="WithSkill"/> set; <see cref="SkillAvailability.Unknown"/> models a
    /// stock install with no catalog.</summary>
    public SkillAvailability Default { get; init; } = SkillAvailability.Present;

    /// <summary>The Packs a <see cref="SkillPackStore"/> loads, every Skill <see cref="SkillAvailability.Present"/>: the same
    /// answers the old Pack store gave.</summary>
    public static FakeSkillAvailability From(SkillPackStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        FakeSkillAvailability fake = new();
        foreach (SkillPack pack in store.All)
        {
            fake.WithPack(pack.Id, pack.Version, pack.Title, [.. pack.Skills.Select(static skill => skill.Name)], pack.ConsultWhenAdds);
        }

        return fake;
    }

    /// <summary>Adds a Pack.</summary>
    public FakeSkillAvailability WithPack(string id, string version, string title, IReadOnlyList<string> skillNames, string? consultWhenAdds = null)
    {
        RegistryRow row = new(RowKind.Pack, this.packs.Count, PathEntries.Empty, null, null, null, null, default);
        this.packs[id] = new PackEntry(row, null, null, id, version, title, "local/test", [], consultWhenAdds, skillNames, [], []);
        return this;
    }

    /// <summary>Gives a Skill a state, with the detail its setup item carries.</summary>
    public FakeSkillAvailability WithSkill(string name, SkillAvailability state, string detail = "")
    {
        this.skills[name] = (state, detail);
        return this;
    }

    /// <summary>Gives a Pack its own problem (Broken, Withdrawn or NotDownloaded), whatever its Skills are.</summary>
    public FakeSkillAvailability WithPackProblem(string id, SkillAvailability state, string detail)
    {
        this.packProblems[id] = (state, detail);
        return this;
    }

    /// <summary>Gives a Skill a catalog sub-category.</summary>
    public FakeSkillAvailability WithSubCategory(string name, string subCategory)
    {
        this.subCategories[name] = subCategory;
        return this;
    }

    /// <inheritdoc/>
    public SkillAvailability Of(string skillName)
    {
        return this.skills.TryGetValue(skillName, out (SkillAvailability State, string Detail) entry) ? entry.State : this.Default;
    }

    /// <inheritdoc/>
    public PackEntry? FindPack(string packId)
    {
        return this.packs.GetValueOrDefault(packId);
    }

    /// <inheritdoc/>
    public string? SubCategoryOf(string skillName)
    {
        return this.subCategories.GetValueOrDefault(skillName);
    }

    /// <inheritdoc/>
    public SkillSetupItem? SetupItem(SkillSetupKind kind, string name)
    {
        if (kind == SkillSetupKind.Pack)
        {
            return this.packs.ContainsKey(name) && this.packProblems.TryGetValue(name, out (SkillAvailability State, string Detail) problem)
                ? new SkillSetupItem(kind, name, problem.State, problem.Detail)
                : null;
        }

        (SkillAvailability state, string detail) = this.skills.TryGetValue(name, out (SkillAvailability State, string Detail) entry) ? entry : (this.Default, string.Empty);
        return state is SkillAvailability.Present or SkillAvailability.Copyable ? null : new SkillSetupItem(kind, name, state, detail);
    }
}

/// <summary>An <see cref="ISkillCopier"/> that records every <see cref="EnsureAvailableAsync"/> call and answers with a
/// configured list of problems.</summary>
internal sealed class FakeSkillCopier : ISkillCopier
{
    /// <summary>The Skill names of each call, in order.</summary>
    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>The problems every call reports; empty means every Skill is ready.</summary>
    public IReadOnlyList<SkillSetupItem> Missing { get; set; } = [];

    /// <summary>Runs at each call, before it returns, for tests that check order.</summary>
    public Action<IReadOnlyList<string>>? OnEnsure { get; set; }

    /// <inheritdoc/>
    public Task<CopyResult> CopyAsync(IReadOnlyList<SkillEntry> skills, CancellationToken ct)
    {
        return Task.FromResult(new CopyResult([], []));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SkillSetupItem>> EnsureAvailableAsync(IReadOnlyList<string> skillNames, CancellationToken ct)
    {
        this.Calls.Add(skillNames);
        this.OnEnsure?.Invoke(skillNames);
        return Task.FromResult(this.Missing);
    }

    /// <summary>A setup item for a Skill that is not downloaded.</summary>
    public static SkillSetupItem NotDownloaded(string name)
    {
        return new SkillSetupItem(SkillSetupKind.Skill, name, SkillAvailability.NotDownloaded, string.Empty);
    }
}

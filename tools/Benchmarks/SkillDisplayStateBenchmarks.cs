using BenchmarkDotNet.Attributes;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tools.Benchmarks;

/// <summary>
/// perf-audit S1: the two lookups <c>SkillRegistry.DisplayState(SkillEntry)</c> makes by a Skill's state key, for
/// every Skill, as <c>SkillArrivalWatcher.Usable</c> runs them on each page render. <see cref="Old"/> reads
/// <see cref="SkillEntry.StateKey"/> once per lookup, as the code did; <see cref="New"/> reads it once per Skill.
/// </summary>
[MemoryDiagnoser]
public class SkillDisplayStateBenchmarks
{
    private const int SkillCount = 100;

    private SkillEntry[] skills = [];
    private CheckResult checkedResult = CheckResult.Empty;
    private HashSet<string> newKeys = new(StringComparer.Ordinal);

    /// <summary>Builds 100 Skills, every one checked Usable and every tenth one New.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.skills = [.. Enumerable.Range(0, SkillCount).Select(static index => Skill($"skill-{index:D3}"))];
        Dictionary<string, ItemCheckState> states = this.skills.ToDictionary(static skill => skill.StateKey, static _ => ItemCheckState.Usable, StringComparer.Ordinal);
        this.checkedResult = CheckResult.Empty with { States = states };
        this.newKeys = new HashSet<string>(this.skills.Where(static (_, index) => index % 10 == 0).Select(static skill => skill.StateKey), StringComparer.Ordinal);
    }

    /// <summary>The code before the fix: <see cref="SkillEntry.StateKey"/> read for each lookup.</summary>
    [Benchmark(Baseline = true)]
    public int Old()
    {
        int usableAndNew = 0;
        foreach (SkillEntry skill in this.skills)
        {
            ItemCheckState state = this.checkedResult.StateOf(skill.StateKey) ?? ItemCheckState.Broken;
            bool isNew = this.newKeys.Contains(skill.StateKey);
            usableAndNew += state == ItemCheckState.Usable && isNew ? 1 : 0;
        }

        return usableAndNew;
    }

    /// <summary>The shipped code: the key read once per Skill.</summary>
    [Benchmark]
    public int New()
    {
        int usableAndNew = 0;
        foreach (SkillEntry skill in this.skills)
        {
            string key = skill.StateKey;
            ItemCheckState state = this.checkedResult.StateOf(key) ?? ItemCheckState.Broken;
            bool isNew = this.newKeys.Contains(key);
            usableAndNew += state == ItemCheckState.Usable && isNew ? 1 : 0;
        }

        return usableAndNew;
    }

    /// <summary>A downloaded Skill with only a name; the other fields play no part in the lookups.</summary>
    private static SkillEntry Skill(string name) =>
        new(name, string.Empty, null, null, null, null, [], string.Empty, null, null, [], null, null, [], SkillPathState.Present, []);
}

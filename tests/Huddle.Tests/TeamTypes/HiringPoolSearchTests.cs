using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="HiringPoolSearch"/>: the two tag clouds filter the pool's cards, types by OR and tags and skills by AND, the way the avatar picker's clouds do.</summary>
public sealed class HiringPoolSearchTests
{
    private const string Open = "alpha";

    private static PoolEntry Entry(string id, bool listed, string[] types, params string[] terms) =>
        new(new RoleSpec(id, id, "p.md"), listed, types, terms);

    /// <summary>With nothing selected every card shows.</summary>
    [Fact]
    public void Matches_NothingSelected_ShowsEveryCard()
    {
        PoolEntry entry = Entry("a", false, ["beta"], "tax");

        Assert.True(HiringPoolSearch.Matches(entry, [], [], Open));
    }

    /// <summary>A card shows when it suits any one of the selected team types.</summary>
    [Fact]
    public void Matches_SeveralTypesSelected_NeedsOnlyOne()
    {
        PoolEntry entry = Entry("a", false, ["beta"], "tax");

        Assert.True(HiringPoolSearch.Matches(entry, ["gamma", "beta"], [], Open));
        Assert.False(HiringPoolSearch.Matches(entry, ["gamma"], [], Open));
    }

    /// <summary>A role the open type lists itself always suits that type, even when its job names other types only.</summary>
    [Fact]
    public void Matches_ListedRoleOfTheOpenType_SuitsTheOpenTypeWhateverItsJobSays()
    {
        PoolEntry listed = Entry("a", true, ["beta"], "tax");
        PoolEntry pooled = Entry("b", false, ["beta"], "tax");

        Assert.True(HiringPoolSearch.Matches(listed, [Open], [], Open));
        Assert.False(HiringPoolSearch.Matches(pooled, [Open], [], Open));
    }

    /// <summary>A card must carry every selected tag or skill name, compared without regard to case.</summary>
    [Fact]
    public void Matches_SeveralTerms_NeedsEveryOneIgnoringCase()
    {
        PoolEntry entry = Entry("a", false, ["beta"], "tax", "forms", "budget-modeling");

        Assert.True(HiringPoolSearch.Matches(entry, [], ["TAX", "budget-modeling"], Open));
        Assert.False(HiringPoolSearch.Matches(entry, [], ["tax", "legal"], Open));
    }

    /// <summary>The two clouds combine with AND: a card that passes only one of them is hidden.</summary>
    [Fact]
    public void Matches_TypeAndTerm_BothMustPass()
    {
        PoolEntry entry = Entry("a", false, ["beta"], "tax");

        Assert.True(HiringPoolSearch.Matches(entry, ["beta"], ["tax"], Open));
        Assert.False(HiringPoolSearch.Matches(entry, ["beta"], ["legal"], Open));
        Assert.False(HiringPoolSearch.Matches(entry, ["gamma"], ["tax"], Open));
    }

    /// <summary>The second cloud lists each term once, the most used first, then alphabetically.</summary>
    [Fact]
    public void TermPool_OrdersByCountThenName()
    {
        List<PoolEntry> entries =
        [
            Entry("a", false, [], "tax", "forms"),
            Entry("b", false, [], "tax", "legal"),
            Entry("c", false, [], "TAX", "care"),
        ];

        IReadOnlyList<string> pool = HiringPoolSearch.TermPool(entries);

        Assert.Equal(["tax", "care", "forms", "legal"], pool);
    }

    /// <summary>A role with no job is filed under the open type with no terms, and a role with a job takes the job's types, tags and skills.</summary>
    [Fact]
    public void Entries_RoleWithAndWithoutJob_AreFiledFromTheJobOrTheOpenType()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["hiringpool.json"] = """{ "schemaVersion": 1, "jobs": [ { "id": "bookkeeper", "title": "Bookkeeper", "tags": [ "accounting" ], "skills": [ "budget-modeling" ], "teamTypes": [ "beta" ] } ] }""",
            ["personas/bookkeeper.md"] = "---\ntitle: 'Bookkeeper'\n---\nYou keep the books.",
        };
        HiringPoolCatalog pool = new(dir.Options(), NullLogger<HiringPoolCatalog>.Instance, builtins);
        RoleSpec withJob = new("bookkeeper", "Bookkeeper", "p.md", Job: "bookkeeper", Pooled: true);
        RoleSpec withoutJob = new("legacy", "Legacy", "c.md");

        IReadOnlyList<PoolEntry> entries = HiringPoolSearch.Entries([withJob, withoutJob], Open, pool);

        Assert.Equal(["beta"], entries[0].TeamTypes);
        Assert.Equal(["accounting", "budget-modeling"], entries[0].Terms);
        Assert.False(entries[0].Listed);
        Assert.Equal([Open], entries[1].TeamTypes);
        Assert.Empty(entries[1].Terms);
        Assert.True(entries[1].Listed);
    }
}

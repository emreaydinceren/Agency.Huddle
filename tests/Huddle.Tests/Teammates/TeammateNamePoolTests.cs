using Agency.Huddle.App.Teammates;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// <see cref="TeammateNamePool"/>: the names a new Teammate's Name box starts with, and the draw that skips the ones
/// already taken.
/// </summary>
public sealed class TeammateNamePoolTests
{
    /// <summary>The pool is the 121 names it ships with.</summary>
    [Fact]
    public void Names_HoldsTheShippedPool()
    {
        Assert.Equal(121,TeammateNamePool.Names.Count);
    }

    /// <summary>No name appears twice, ignoring case, so every name is as likely as any other.</summary>
    [Fact]
    public void Names_HaveNoDuplicates()
    {
        Assert.Equal(TeammateNamePool.Names.Count, TeammateNamePool.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>Every name passes the Teammate name rules, so a suggestion can be saved without editing it.</summary>
    [Fact]
    public void Names_AllSatisfyTheNameRules()
    {
        Assert.All(TeammateNamePool.Names, static name => Assert.True(NameRules.IsValidAgentName(name), name));
    }

    /// <summary>The same seed draws the same name.</summary>
    [Fact]
    public void Pick_SameSeed_ReturnsTheSameName()
    {
        Assert.Equal(
            TeammateNamePool.Pick(new Random(7), []),
            TeammateNamePool.Pick(new Random(7), []));
    }

    /// <summary>Across seeds the draw returns many different names.</summary>
    [Fact]
    public void Pick_AcrossSeeds_Varies()
    {
        HashSet<string> seen = [.. Enumerable.Range(0, 40).Select(static seed => TeammateNamePool.Pick(new Random(seed), []))];

        Assert.True(seen.Count > 10);
    }

    /// <summary>A name an existing Teammate has is never drawn, whatever its case.</summary>
    [Fact]
    public void Pick_SkipsTakenNames()
    {
        string[] taken = [.. TeammateNamePool.Names.Skip(1).Select(static name => name.ToUpperInvariant())];

        for (int seed = 0; seed < 20; seed++)
        {
            Assert.Equal(TeammateNamePool.Names[0], TeammateNamePool.Pick(new Random(seed), taken));
        }
    }

    /// <summary>Asking again never returns the name now showing while another is free.</summary>
    [Fact]
    public void Pick_WithCurrent_NeverReturnsIt()
    {
        string current = TeammateNamePool.Names[0];

        for (int seed = 0; seed < 200; seed++)
        {
            Assert.NotEqual(current, TeammateNamePool.Pick(new Random(seed), [], current));
        }
    }

    /// <summary>With every pool name taken the draw falls back to a numbered name that is still valid and free.</summary>
    [Fact]
    public void Pick_EveryNameTaken_ReturnsAValidNumberedName()
    {
        string name = TeammateNamePool.Pick(new Random(1), TeammateNamePool.Names);

        Assert.DoesNotContain(name, TeammateNamePool.Names);
        Assert.True(NameRules.IsValidAgentName(name), name);
    }
}

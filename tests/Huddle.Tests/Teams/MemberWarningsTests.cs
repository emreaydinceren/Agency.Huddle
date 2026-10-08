using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="MemberWarnings"/>: shared vendor, too many skills and two skills in one sub-category (MS-3, MS-5, D8).</summary>
public sealed class MemberWarningsTests
{
    private static readonly RoleSpec ReviewerRole = new("reviewer", "Reviewer", "c/rev.md", Model: new ModelSpec(ModelTier.Balanced, ["developer"]));

    private static string? NoSubCategory(string skill)
    {
        return null;
    }

    private static string? SubCategory(string skill)
    {
        return skill switch
        {
            "test-driven-development" or "incremental-implementation" => "Implementation",
            "code-review" => "Review",
            _ => null,
        };
    }

    private static MemberView Ana(string? adapter = "claude", params string[] skills)
    {
        return new MemberView("Ana", "reviewer", adapter, skills);
    }

    private static MemberView Jane(string? adapter = "claude")
    {
        return new MemberView("Jane", "developer", adapter, []);
    }

    /// <summary>A reviewer on the same adapter as a developer is warned, naming both members and their roles.</summary>
    [Fact]
    public void For_SharedAdapterWithListedRole_WarnsAboutVendor()
    {
        MemberView ana = MemberWarningsTests.Ana();

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana, MemberWarningsTests.Jane()], MemberWarningsTests.NoSubCategory);

        Assert.Equal(["Reviewer Ana runs on the same vendor as Developer Jane."], warnings);
    }

    /// <summary>A member on a different adapter from the listed role gets no vendor warning.</summary>
    [Fact]
    public void For_DifferentAdapter_NoWarnings()
    {
        MemberView ana = MemberWarningsTests.Ana();

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana, MemberWarningsTests.Jane("codex")], MemberWarningsTests.NoSubCategory);

        Assert.Empty(warnings);
    }

    /// <summary>A member without an adapter gets no vendor warning, even when another member also has none.</summary>
    [Fact]
    public void For_NullAdapter_NoVendorWarning()
    {
        MemberView ana = MemberWarningsTests.Ana(null);

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana, MemberWarningsTests.Jane(null)], MemberWarningsTests.NoSubCategory);

        Assert.Empty(warnings);
    }

    /// <summary>A shared adapter with a role that is not in differentVendorFrom is not a warning.</summary>
    [Fact]
    public void For_SharedAdapterWithUnlistedRole_NoWarnings()
    {
        MemberView ana = MemberWarningsTests.Ana();
        MemberView qa = new("Quinn", "qa", "claude", []);

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana, qa], MemberWarningsTests.NoSubCategory);

        Assert.Empty(warnings);
    }

    /// <summary>Twelve skills warn that more than ten makes skill choice harder.</summary>
    [Fact]
    public void For_TwelveSkills_WarnsAboutCount()
    {
        string[] skills = [.. Enumerable.Range(1, 12).Select(static i => "skill-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        MemberView ana = MemberWarningsTests.Ana("claude", skills);

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana], MemberWarningsTests.NoSubCategory);

        Assert.Equal(["Ana has 12 skills; more than 10 makes skill choice harder."], warnings);
    }

    /// <summary>Exactly ten skills is within the limit and gives no warning.</summary>
    [Fact]
    public void For_TenSkills_NoWarnings()
    {
        string[] skills = [.. Enumerable.Range(1, 10).Select(static i => "skill-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        MemberView ana = MemberWarningsTests.Ana("claude", skills);

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana], MemberWarningsTests.NoSubCategory);

        Assert.Empty(warnings);
    }

    /// <summary>Two skills in one sub-category warn, naming both skills and the sub-category.</summary>
    [Fact]
    public void For_TwoSkillsInOneSubCategory_Warns()
    {
        MemberView ana = MemberWarningsTests.Ana("claude", "test-driven-development", "incremental-implementation", "code-review");

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana], MemberWarningsTests.SubCategory);

        Assert.Equal(["test-driven-development and incremental-implementation are both in Implementation."], warnings);
    }

    /// <summary>Skills with an unknown sub-category never count as sharing one.</summary>
    [Fact]
    public void For_UnknownSubCategories_NoWarnings()
    {
        MemberView ana = MemberWarningsTests.Ana("claude", "alpha", "beta");

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana], MemberWarningsTests.SubCategory);

        Assert.Empty(warnings);
    }

    /// <summary>When all three apply, the warnings come in order: vendor, count, sub-category.</summary>
    [Fact]
    public void For_AllThree_ReturnsInOrder()
    {
        List<string> skills = ["test-driven-development", "incremental-implementation"];
        skills.AddRange(Enumerable.Range(1, 10).Select(static i => "skill-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        MemberView ana = MemberWarningsTests.Ana("claude", [.. skills]);

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana, MemberWarningsTests.Jane()], MemberWarningsTests.SubCategory);

        Assert.Equal(
            [
                "Reviewer Ana runs on the same vendor as Developer Jane.",
                "Ana has 12 skills; more than 10 makes skill choice harder.",
                "test-driven-development and incremental-implementation are both in Implementation.",
            ],
            warnings);
    }

    /// <summary>A clean member with few skills gets no warnings.</summary>
    [Fact]
    public void For_Clean_NoWarnings()
    {
        MemberView ana = MemberWarningsTests.Ana("claude", "code-review");

        IReadOnlyList<string> warnings = MemberWarnings.For(ana, MemberWarningsTests.ReviewerRole, [ana], MemberWarningsTests.SubCategory);

        Assert.Empty(warnings);
    }
}

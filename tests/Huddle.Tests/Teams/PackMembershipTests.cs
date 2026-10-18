using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="PackMembership"/> (Skill Packs spec §6.10.4): the one rule for which Skills arrive and leave with
/// a Pack, the <c>consult_when</c> clause, and the Keep choice that drops only the record.</summary>
public sealed class PackMembershipTests
{
    /// <summary>A Teammate file as Huddle writes one: lists in the flow form <see cref="PersonaFrontmatter.WriteListField"/> uses.</summary>
    private const string Original = "---\nName: Nova\nTitle: Developer\nAlias: nova\nskills: ['tdd']\n---\nYou build.\n";

    private static readonly PackContents Writing = new("writing", "1.2.0", "drafting text", ["draft", "outline"]);

    private static readonly PackContents Review = new("review", "1.0.0", null, ["edit", "draft"]);

    /// <summary>Adding a Pack whose Skills the file does not hold yet and removing it again gives back the original file,
    /// byte for byte (for a file in Huddle's own list form).</summary>
    [Fact]
    public void Add_ThenRemove_ReturnsTheOriginalText()
    {
        string added = PackMembership.Add(Original, Writing);

        string removed = PackMembership.Remove(added, Writing, [], []);

        Assert.NotEqual(Original, added);
        Assert.Equal(Original, removed);
    }

    /// <summary>Adding records the Pack at its version, adds each of its Skills once, and its clause.</summary>
    [Fact]
    public void Add_RecordsPackSkillsAndClause()
    {
        string added = PackMembership.Add(Original, Writing with { SkillNames = ["draft", "tdd", "outline", "draft"] });

        Assert.True(PersonaFrontmatter.TryReadIdentity(added, out PersonaIdentity? identity, out _));
        Assert.Equal(["tdd", "draft", "outline"], identity.Skills);
        Assert.Equal([new VersionedRef("writing", "1.2.0")], identity.Packs);
        Assert.Equal("drafting text", ConsultWhen(added));
    }

    /// <summary>Removing keeps a Skill the role carries and a Skill another Pack provides; only the rest leave.</summary>
    [Fact]
    public void Remove_KeepsSkillsTheRoleOrAnotherPackProvides()
    {
        PackContents withTdd = Writing with { SkillNames = ["draft", "outline", "tdd"] };
        string both = PackMembership.Add(PackMembership.Add(Original, withTdd), Review);

        string removed = PackMembership.Remove(both, withTdd, ["tdd"], [Review]);

        Assert.True(PersonaFrontmatter.TryReadIdentity(removed, out PersonaIdentity? identity, out _));
        Assert.Equal(["tdd", "draft", "edit"], identity.Skills);
        Assert.Equal([new VersionedRef("review", "1.0.0")], identity.Packs);
        Assert.Null(ConsultWhen(removed));
    }

    /// <summary>Removing a Pack whose Skills nothing else provides takes them out.</summary>
    [Fact]
    public void Remove_NothingElseProvides_SkillsLeave()
    {
        string added = PackMembership.Add(Original, Review);

        string removed = PackMembership.Remove(added, Review, [], []);

        Assert.True(PersonaFrontmatter.TryReadIdentity(removed, out PersonaIdentity? identity, out _));
        Assert.Equal(["tdd"], identity.Skills);
    }

    /// <summary>The Keep choice drops only the <c>_packs</c> entry; the Skills and the clause stay.</summary>
    [Fact]
    public void DropRecord_LeavesSkillsAndConsultWhen()
    {
        string added = PackMembership.Add(Original, Writing);

        string dropped = PackMembership.DropRecord(added, "writing");

        Assert.True(PersonaFrontmatter.TryReadIdentity(dropped, out PersonaIdentity? identity, out _));
        Assert.Empty(identity.Packs ?? []);
        Assert.Equal(["tdd", "draft", "outline"], identity.Skills);
        Assert.Equal("drafting text", ConsultWhen(dropped));
    }

    /// <summary>Adding a Pack the file already records at another version replaces the entry rather than adding a second.</summary>
    [Fact]
    public void Add_SamePackNewVersion_ReplacesTheEntry()
    {
        string old = PackMembership.Add(Original, Writing with { Version = "1.0.0" });

        string updated = PackMembership.Add(old, Writing);

        Assert.True(PersonaFrontmatter.TryReadIdentity(updated, out PersonaIdentity? identity, out _));
        Assert.Equal([new VersionedRef("writing", "1.2.0")], identity.Packs);
        Assert.Equal("drafting text", ConsultWhen(updated));
    }

    /// <summary>The <c>consult_when</c> value, or null when the field is absent.</summary>
    private static string? ConsultWhen(string text)
    {
        return PersonaFrontmatter.Parse(text).Fields.FirstOrDefault(static field => field.Key == "consult_when")?.Value;
    }
}

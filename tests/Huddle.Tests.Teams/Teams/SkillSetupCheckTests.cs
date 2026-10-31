using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Pins <see cref="SkillSetupCheck.For"/> (Skill Packs spec §6.10.2): Pack problems fold into one item per Pack, a
/// Pack's own problem wins over its Skills', and a Skill outside the Packs is judged on its own.</summary>
public sealed class SkillSetupCheckTests
{
    /// <summary>One Pack with two missing Skills gives one Pack item, not three, even when the Skills are also listed.</summary>
    [Fact]
    public void For_PackWithTwoMissingSkills_GivesOnePackItem()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("writing", "1.0.0", "Writing", ["draft", "edit"])
            .WithSkill("draft", SkillAvailability.NotDownloaded)
            .WithSkill("edit", SkillAvailability.NotDownloaded);

        IReadOnlyList<SkillSetupItem> items = SkillSetupCheck.For(["draft", "edit"], [new VersionedRef("writing", "1.0.0")], skills);

        Assert.Equal(new SkillSetupItem(SkillSetupKind.Pack, "writing", SkillAvailability.NotDownloaded, "Writing"), Assert.Single(items));
    }

    /// <summary>A Pack's own problem (withdrawn) is its item, whatever its Skills are.</summary>
    [Fact]
    public void For_PackWithdrawn_ItsOwnProblemWins()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("writing", "1.0.0", "Writing", ["draft"])
            .WithSkill("draft", SkillAvailability.NotDownloaded)
            .WithPackProblem("writing", SkillAvailability.Withdrawn, "Pulled.");

        IReadOnlyList<SkillSetupItem> items = SkillSetupCheck.For([], [new VersionedRef("writing", "1.0.0")], skills);

        SkillSetupItem item = Assert.Single(items);
        Assert.Equal(SkillAvailability.Withdrawn, item.Problem);
        Assert.Equal("Pack \"writing\" was withdrawn by its Source: Pulled.", SkillSetupText.Reason(item));
    }

    /// <summary>A Skill no listed Pack provides gets its own item, after the Pack items; a ready Skill gets none.</summary>
    [Fact]
    public void For_SkillOutsideThePacks_JudgedOnItsOwn()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("writing", "1.0.0", "Writing", ["draft"])
            .WithSkill("draft", SkillAvailability.Broken, "S002")
            .WithSkill("review", SkillAvailability.Unknown)
            .WithSkill("ok", SkillAvailability.Copyable);

        IReadOnlyList<SkillSetupItem> items = SkillSetupCheck.For(["review", "ok", "draft"], [new VersionedRef("writing", "1.0.0")], skills);

        Assert.Equal(
            [(SkillSetupKind.Pack, "writing", SkillAvailability.Broken), (SkillSetupKind.Skill, "review", SkillAvailability.Unknown)],
            items.Select(static item => (item.Kind, item.Name, item.Problem)));
    }

    /// <summary>A Pack listing a Skill no catalog knows says which Skill, not that the Pack itself is unknown.</summary>
    [Fact]
    public void For_PackListingAnUnknownSkill_NamesTheSkill()
    {
        FakeSkillAvailability skills = new FakeSkillAvailability()
            .WithPack("writing", "1.0.0", "Writing", ["mystery"])
            .WithSkill("mystery", SkillAvailability.Unknown);

        SkillSetupItem item = Assert.Single(SkillSetupCheck.For([], [new VersionedRef("writing", "1.0.0")], skills));

        Assert.Equal("Pack \"writing\" lists Skill \"mystery\", which is not in any catalog", SkillSetupText.Reason(item));
    }
}

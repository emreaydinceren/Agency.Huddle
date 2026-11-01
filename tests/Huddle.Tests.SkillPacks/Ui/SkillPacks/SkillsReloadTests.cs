using Agency.Huddle.App.Skills.Ui;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins the wording and key of the one Reload snackbar shared by the Skills tab and the Pack bar.</summary>
public sealed class SkillsReloadTests
{
    /// <summary>Zero problems reads as a clean bill.</summary>
    [Fact]
    public void Message_NoProblems_SaysNoProblems()
    {
        Assert.Equal("Reloaded. No problems.", SkillsReload.Message(0));
    }

    /// <summary>One problem uses the singular noun.</summary>
    [Fact]
    public void Message_OneProblem_UsesTheSingular()
    {
        Assert.Equal("Reloaded. 1 problem found.", SkillsReload.Message(1));
    }

    /// <summary>Several problems use the plural noun.</summary>
    [Fact]
    public void Message_SeveralProblems_UsesThePlural()
    {
        Assert.Equal("Reloaded. 3 problems found.", SkillsReload.Message(3));
    }

    /// <summary>The key stays the one existing snackbars were keyed with.</summary>
    [Fact]
    public void SnackbarKey_IsTheOriginalKey()
    {
        Assert.Equal("skills-reload", SkillsReload.SnackbarKey);
    }
}

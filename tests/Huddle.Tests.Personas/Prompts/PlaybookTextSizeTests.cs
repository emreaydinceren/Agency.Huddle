namespace Agency.Huddle.Tests.Prompts;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Prompts;

/// <summary>
/// Pins the length of each shipped Playbook text (Team Playbooks spec §4.5), measured before placeholders are filled and with LF line
/// endings, so a later edit that grows one fails here.
/// </summary>
public sealed class PlaybookTextSizeTests
{
    /// <summary>The Member Huddle rules are at most 1,800 characters, and not empty.</summary>
    [Fact]
    public void MemberHuddleRules_AreWithinTheCeiling()
    {
        int length = Lf(HuddleRules.RawMember).Length;

        Assert.InRange(length, 1000, 1800);
    }

    /// <summary>The Manager Huddle rules are at most 1,500 characters, and not empty.</summary>
    [Fact]
    public void ManagerHuddleRules_AreWithinTheCeiling()
    {
        int length = Lf(HuddleRules.RawManager).Length;

        Assert.InRange(length, 500, 1500);
    }

    /// <summary>The Member Playbook default is at most 2,500 characters, and not empty.</summary>
    [Fact]
    public void MemberPlaybookDefault_IsWithinTheCeiling()
    {
        int length = Lf(PromptCatalog.Get("systemPrompt.memberPlaybook").Default).Length;

        Assert.InRange(length, 1000, 2500);
    }

    /// <summary>The Manager Playbook default is at most 2,500 characters, and not empty.</summary>
    [Fact]
    public void ManagerPlaybookDefault_IsWithinTheCeiling()
    {
        int length = Lf(PromptCatalog.Get("systemPrompt.managerPlaybook").Default).Length;

        Assert.InRange(length, 1000, 2500);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}

using Agency.Huddle.App.Skills;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §6.8 "Offered Jobs" (SB-R23, second half): the onboarding Skill offers the upkeep Jobs and creates each only on
/// the Human's yes, and a fresh install schedules none by itself.
/// </summary>
public sealed class OnboardingJobOfferTests
{
    /// <summary>The sentence that gates creation; the Skill must carry it word for word.</summary>
    private const string YesGate = "Create each Job with the `jobs` tool only after the Human says yes to that offer, and never before.";

    private static string Onboarding => SkillCatalog.All["team-building"]["onboarding.md"];

    /// <summary>The Skill has an "Offer upkeep Jobs" step.</summary>
    [Fact]
    public void Onboarding_HasOfferUpkeepJobsStep()
    {
        Assert.Contains("## Offer upkeep Jobs", Onboarding, StringComparison.Ordinal);
    }

    /// <summary>The weekday review is offered in the Jobs spec's line format (Jobs spec §2: <c>weekdays at 09:00</c>).</summary>
    [Fact]
    public void Onboarding_OffersWeekdayReview_InJobsLineFormat()
    {
        Assert.Contains("`weekdays at 09:00`", Onboarding, StringComparison.Ordinal);
    }

    /// <summary>A weekly <c>brain-check</c> is offered per manager (Jobs spec §2: <c>weekly on fri at 16:00</c>).</summary>
    [Fact]
    public void Onboarding_OffersWeeklyBrainCheckPerManager()
    {
        Assert.Contains("`weekly on fri at 16:00`", Onboarding, StringComparison.Ordinal);
        Assert.Contains("weekly `brain-check` for each manager", Onboarding, StringComparison.Ordinal);
    }

    /// <summary>
    /// A monthly <c>brain-check</c> is offered for the Chief of Staff. The line format has no monthly preset, so it is
    /// <c>weekly on mon at 09:00</c> and the instruction skips every Monday but the first of the month.
    /// </summary>
    [Fact]
    public void Onboarding_OffersMonthlyBrainCheckForChiefOfStaff()
    {
        Assert.Contains("monthly `brain-check` for the Chief of Staff", Onboarding, StringComparison.Ordinal);
        Assert.Contains("`weekly on mon at 09:00`", Onboarding, StringComparison.Ordinal);
        Assert.Contains("first Monday of the month", Onboarding, StringComparison.Ordinal);
    }

    /// <summary>Creation waits for the Human's yes: the exact gate sentence is present.</summary>
    [Fact]
    public void Onboarding_CreatesEachJobOnlyAfterYes()
    {
        Assert.Contains(YesGate, Onboarding, StringComparison.Ordinal);
    }

    /// <summary>A fresh install has no Job in any <c>schedule.md</c> once the host has started.</summary>
    [Fact]
    public void FreshInstall_AfterStartUp_HasNoSchedulesFile()
    {
        using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string[] schedules = Directory.GetFiles(factory.DataDirPath, "schedule.md", SearchOption.AllDirectories);

        Assert.Empty(schedules);
    }
}

using System.Text.Json;
using Agency.Huddle.SkillPacks.Checking;

namespace Agency.Huddle.Tests.SkillPacks.EndToEnd;

/// <summary>Pins that Huddle and <c>huddle-packs</c> agree (Skill Packs spec §6.3, task P32): over the output of the move, the
/// registry's own <c>Checks.Run</c> and <c>huddle-packs validate --json</c> report the same summary and the same findings.</summary>
public sealed class HuddlePacksParityTests
{
    /// <summary>After the move, the summary counts match, and the findings match by severity and code, in order.</summary>
    [Fact]
    public async Task HuddlePacksParity_AfterTheMove_SameSummaryAndFindings()
    {
        await using SkillPacksWorld world = await SkillPacksWorld.StartAsync(runMove: true);
        CheckResult huddle = world.Registry.Current.Checked;

        (int exit, string output) = world.AiTool().Validate("--json");

        using JsonDocument json = JsonDocument.Parse(output);
        JsonElement summary = json.RootElement.GetProperty("summary");
        Assert.Equal(huddle.Summary.Errors > 0 ? 1 : 0, exit);
        Assert.Equal(huddle.Summary.Rows, summary.GetProperty("rows").GetInt32());
        Assert.Equal(huddle.Summary.Packs, summary.GetProperty("packs").GetInt32());
        Assert.Equal(huddle.Summary.Skills, summary.GetProperty("skills").GetInt32());
        Assert.Equal(huddle.Summary.Errors, summary.GetProperty("errors").GetInt32());
        Assert.Equal(huddle.Summary.Warnings, summary.GetProperty("warnings").GetInt32());
        IReadOnlyList<string> fromHuddle = [.. huddle.Findings.Select(static finding => $"{(finding.Severity == Severity.Error ? "error" : "warning")} {finding.Code}")];
        IReadOnlyList<string> fromTool = [.. json.RootElement.GetProperty("findings").EnumerateArray().Select(static finding => $"{finding.GetProperty("severity").GetString()} {finding.GetProperty("code").GetString()}")];
        Assert.Equal(fromHuddle, fromTool);
    }
}

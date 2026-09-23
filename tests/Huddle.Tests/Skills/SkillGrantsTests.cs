using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §6.5 (which tools a Persona is offered) and Spec §8.2 against
/// <see cref="SkillGrants.Offer"/>: a table of Skill sets against the tool names the caller should see,
/// each built from <see cref="AllTools"/> so the tests exercise the same fixed catalog throughout.
/// </summary>
public sealed class SkillGrantsTests
{
    /// <summary>A minimal <see cref="IAppTool"/> stand-in identified only by its <see cref="Name"/>.</summary>
    /// <param name="name">The tool's name, and its whole behaviour for this stub.</param>
    private sealed class StubTool(string name) : IAppTool
    {
        public string Name => name;

        public string Description => name;

        public JsonObject InputSchema => new JsonObject { ["type"] = "object" };

        public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken)
        {
            return Task.FromResult(name);
        }
    }

    /// <summary>
    /// The fixed catalog every case in <see cref="OfferCases"/> offers from: two tools nothing gates,
    /// the two <see cref="SkillGrants.Grantable"/> tools, and <c>read_skill</c> itself.
    /// </summary>
    private static readonly IReadOnlyList<IAppTool> AllTools =
    [
        new StubTool("post_message"),
        new StubTool("create_room"),
        new StubTool("validate_teammate"),
        new StubTool("propose_teammates"),
        new StubTool("read_skill"),
    ];

    /// <summary>
    /// Table-driven cases for <see cref="Offer_VariousSkillSets_OffersExpectedTools"/>: no Skills; a Skill
    /// granting both <see cref="SkillGrants.Grantable"/> tools; a Skill granting neither. The Skill's
    /// granted tools travel as a plain <c>string[]?</c> (<see langword="null"/> meaning no Skills at all)
    /// rather than as a built <see cref="Skill"/>, because <see cref="Skill"/> is <see langword="internal"/>
    /// and a public <c>[MemberData]</c> method may not return it (CS0050).
    /// </summary>
    public static TheoryData<string, string[]?, string[]> OfferCases()
    {
        return new TheoryData<string, string[]?, string[]>
        {
            { "NoSkills", null, ["post_message", "create_room"] },
            {
                "SkillWithBothGrantableTools",
                ["validate_teammate", "propose_teammates"],
                ["post_message", "create_room", "validate_teammate", "propose_teammates", "read_skill"]
            },
            { "SkillWithNoTools", [], ["post_message", "create_room", "read_skill"] },
        };
    }

    /// <summary>
    /// With no Skills, none of <c>read_skill</c>, <c>validate_teammate</c> or <c>propose_teammates</c> is
    /// offered, but every other tool is kept; a Skill listing both <see cref="SkillGrants.Grantable"/>
    /// tools offers both of them plus <c>read_skill</c>; a Skill listing no tools offers only
    /// <c>read_skill</c> among the gated ones.
    /// </summary>
    /// <param name="scenario">A label identifying the case; not itself asserted on.</param>
    /// <param name="skillTools">
    /// The single Skill's granted tools, or <see langword="null"/> to pass no Skills at all.
    /// </param>
    /// <param name="expectedNames">The tool names, in order, <see cref="SkillGrants.Offer"/> should return.</param>
    [Theory]
    [MemberData(nameof(OfferCases))]
    public void Offer_VariousSkillSets_OffersExpectedTools(string scenario, string[]? skillTools, string[] expectedNames)
    {
        _ = scenario;
        IReadOnlyList<Skill> skills = skillTools is null ? [] : [TeamBuildingSkill(skillTools)];

        IReadOnlyList<IAppTool> offered = SkillGrants.Offer(AllTools, skills);

        Assert.Equal(expectedNames, offered.Select(tool => tool.Name));
    }

    /// <summary>
    /// The order of <c>all</c> is preserved in the result: <c>read_skill</c> placed before the other
    /// tools in the input still comes before them in the output, rather than being appended at the end.
    /// </summary>
    [Fact]
    public void Offer_InputOrderIsPreserved_OutputKeepsTheSameOrder()
    {
        IReadOnlyList<IAppTool> all =
        [
            new StubTool("read_skill"),
            new StubTool("post_message"),
            new StubTool("propose_teammates"),
            new StubTool("validate_teammate"),
        ];
        Skill skill = TeamBuildingSkill(["propose_teammates", "validate_teammate"]);

        IReadOnlyList<IAppTool> offered = SkillGrants.Offer(all, [skill]);

        string[] expectedNames = ["read_skill", "post_message", "propose_teammates", "validate_teammate"];
        Assert.Equal(expectedNames, offered.Select(tool => tool.Name));
    }

    /// <summary>Builds a resolved <c>team-building</c> Skill granting exactly <paramref name="tools"/>.</summary>
    /// <param name="tools">The Skill's granted tool names.</param>
    private static Skill TeamBuildingSkill(IReadOnlyList<string> tools)
    {
        return new Skill("team-building", "Use when the Human wants to build a team.", tools, ["SKILL.md"], SkillSource.Default, null);
    }
}

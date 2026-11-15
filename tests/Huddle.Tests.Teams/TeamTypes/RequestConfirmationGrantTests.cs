using System.Text.Json.Nodes;
using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>
/// #539: the catalog grants the built-in <c>request-confirmation</c> Skill to every role whose coverage includes <c>verify</c>,
/// managers included, and to no other role, so whoever can confirm a Request at runtime is offered <c>confirm_request</c>.
/// </summary>
public sealed class RequestConfirmationGrantTests
{
    private static readonly SkillRef RequestConfirmation = new("request-confirmation", "builtin", "request-confirmation");

    /// <summary>The tools <c>team-management</c> grants a manager, none of which a QA Engineer should be offered.</summary>
    private static readonly string[] ManagerOnlyTools = ["propose_sprint", "triage_request", "file_request", "list_teams"];

    /// <summary>Every shipped type id, so the theory runs once per type.</summary>
    public static TheoryData<string> TypeIds
    {
        get
        {
            using TempDataDir dir = new();
            TheoryData<string> data = [];
            foreach (TeamTypeManifest type in new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance).Types)
            {
                data.Add(type.Id);
            }

            return data;
        }
    }

    /// <summary>In every shipped type, a role covering verify holds <c>request-confirmation</c> once, and every other role (pooled ones included) does not hold it.</summary>
    /// <param name="typeId">The shipped type id.</param>
    [Theory]
    [MemberData(nameof(TypeIds))]
    public void Find_BuiltinType_GrantsRequestConfirmationToExactlyTheVerifyRoles(string typeId)
    {
        using TempDataDir dir = new();
        TeamTypeManifest type = Assert.IsType<TeamTypeManifest>(new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance).Find(typeId));

        foreach (RoleSpec role in type.Roles)
        {
            int held = role.Skills.Count(skill => skill == RequestConfirmation);
            bool coversVerify = role.Covers.Contains("verify", StringComparer.Ordinal);
            Assert.True(held == (coversVerify ? 1 : 0), $"{typeId}/{role.Id} covers [{string.Join(", ", role.Covers)}] and holds request-confirmation {held} time(s).");
        }
    }

    /// <summary>The Content Team's fact-checker (a non-Development verify role) holds <c>request-confirmation</c>; its writer, who covers only draft, does not.</summary>
    [Fact]
    public void Find_ContentType_FactCheckerHoldsRequestConfirmation_WriterDoesNot()
    {
        TeamTypeManifest content = BuiltinManifests.Load("content");

        RoleSpec factChecker = Assert.Single(content.Roles, static role => role.Id == "fact-checker");
        RoleSpec writer = Assert.Single(content.Roles, static role => role.Id == "writer");

        Assert.Contains(RequestConfirmation, factChecker.Skills);
        Assert.DoesNotContain(RequestConfirmation, writer.Skills);
    }

    /// <summary>
    /// Done when of #539: a new Development QA Engineer holds no <c>team-management</c>, holds <c>request-confirmation</c>, and the
    /// Skills it holds offer it <c>confirm_request</c> but none of the manager's tools.
    /// </summary>
    [Fact]
    public void Offer_DevelopmentQaEngineer_GetsConfirmRequestAndNoManagerTool()
    {
        RoleSpec qa = Assert.Single(BuiltinManifests.Load("development").Roles, static role => role.Id == "qa-engineer");
        IReadOnlyList<IAppTool> all = [.. SkillGrants.Grantable.Order(StringComparer.Ordinal).Select(static name => (IAppTool)new StubTool(name))];

        IReadOnlyList<string> offered = [.. SkillGrants.Offer(all, [.. qa.Skills.Select(static skill => Resolve(skill.Name))]).Select(static tool => tool.Name)];

        Assert.DoesNotContain(qa.Skills, static skill => skill.Name == "team-management");
        Assert.Contains(RequestConfirmation, qa.Skills);
        Assert.Contains("confirm_request", offered);
        foreach (string tool in ManagerOnlyTools)
        {
            Assert.DoesNotContain(tool, offered);
        }
    }

    /// <summary>Resolves a shipped Skill the way the catalog validates it, so its granted tools are the ones its front matter lists.</summary>
    /// <param name="name">The built-in Skill name.</param>
    /// <returns>The Skill with its validated tools.</returns>
    private static Skill Resolve(string name)
    {
        SkillValidation validation = SkillValidator.Validate(name, SkillCatalog.All[name]);
        return new Skill(name, validation.Description ?? name, validation.Tools, ["SKILL.md"], SkillSource.Default, null);
    }

    /// <summary>A minimal <see cref="IAppTool"/> stand-in identified only by its <see cref="Name"/>.</summary>
    /// <param name="name">The tool's name, and its whole behaviour for this stub.</param>
    private sealed class StubTool(string name) : IAppTool
    {
        public string Name => name;

        public string Description => name;

        public JsonObject InputSchema => new() { ["type"] = "object" };

        public Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken) => Task.FromResult(name);
    }
}

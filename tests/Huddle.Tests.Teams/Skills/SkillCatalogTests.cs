using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §6.1 (<c>SkillCatalog</c>): the shipped Skills are compiled into the assembly as
/// embedded resources, so an install with an empty <c>{DataDir}</c> still has <c>team-building</c>,
/// and an upgrade delivers improved text without touching disk.
/// </summary>
public sealed class SkillCatalogTests
{
    /// <summary>The two grantable tools that the team-building Skill must list in its frontmatter (Spec §6.5).</summary>
    private static readonly string[] ExpectedTools = ["validate_agent", "propose_agents"];

    /// <summary>The catalog holds <c>team-building</c> with exactly its four shipped files, keyed ordinally.</summary>
    [Fact]
    public void All_ContainsTeamBuilding_WithItsFourFiles()
    {
        IReadOnlyDictionary<string, string> teamBuilding = SkillCatalog.All["team-building"];

        HashSet<string> expectedFiles = new(StringComparer.Ordinal)
        {
            "SKILL.md",
            "onboarding.md",
            "roles.md",
            "team-patterns.md",
        };

        Assert.Equal(expectedFiles, teamBuilding.Keys.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>The catalog holds <c>team-management</c> (Tasks 62, 68): valid, and it lists the four Request tools and <c>propose_sprint</c>.</summary>
    [Fact]
    public void All_ContainsTeamManagement_GrantingRequestToolsAndProposeSprint()
    {
        IReadOnlyDictionary<string, string> files = SkillCatalog.All["team-management"];

        SkillValidation validation = SkillValidator.Validate("team-management", files);

        Assert.Equal(["SKILL.md"], files.Keys.ToList());
        Assert.Empty(validation.Issues);
        Assert.Equal(["list_teams", "file_request", "triage_request", "confirm_request", "propose_sprint"], validation.Tools);
    }

    /// <summary>#535: the <c>team-management</c> Skill tells a manager to answer a question Task with add_evidence and close it with close_task, and that an already-closed Task is fine.</summary>
    [Fact]
    public void TeamManagement_TeachesAnsweringAndClosingAQuestionTask()
    {
        string text = string.Join(' ', SkillCatalog.All["team-management"]["SKILL.md"].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        Assert.Contains("1. Record it on the Task with `add_evidence`. Put the whole answer in `note`, so it is on record without the conversation.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("3. Close the Task with `close_task`. Closing is not marking it Done, so it needs no QA Evidence and does not break the rule that you never mark work Done yourself.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("If `close_task` says the Task is already closed, the one who asked closed it first, and there is nothing more to do.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>#536: the catalog holds <c>browser-check</c>: valid, one file, and it grants no tools.</summary>
    [Fact]
    public void All_ContainsBrowserCheck_GrantingNoTools()
    {
        Assert.Contains("browser-check", SkillCatalog.All.Keys);
        IReadOnlyDictionary<string, string> files = SkillCatalog.All["browser-check"];

        SkillValidation validation = SkillValidator.Validate("browser-check", files);

        Assert.Equal(["SKILL.md"], files.Keys.ToList());
        Assert.Empty(validation.Issues);
        Assert.Empty(validation.Tools);
    }

    /// <summary>#536: the <c>browser-check</c> Skill installs Playwright outside the repository, never counts a click as a pass, admits a partial verify and states that the localhost rule is not enforced.</summary>
    [Fact]
    public void BrowserCheck_TeachesPlaywrightOutsideTheRepoAndPartialVerify()
    {
        string text = string.Join(' ', SkillCatalog.All["browser-check"]["SKILL.md"].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        Assert.Contains("Never install Playwright in the repository you are checking: it would dirty the tree you verify.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("On Windows launch `chromium.launch({ channel: \"msedge\" })`; on Linux and macOS launch `chromium.launch({ channel: \"chrome\" })`.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("Never count a click as a pass only because it ran: a button that does nothing must fail the check.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("Never record a browser check you did not run.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
        Assert.Contains("Opening `localhost` only is a rule you follow, not something Huddle enforces in this version.", text, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>The catalog holds <c>team-rollup</c> (Task 79): valid, one file, and its tools list is exactly <c>list_teams</c>.</summary>
    [Fact]
    public void All_ContainsTeamRollup_GrantingListTeamsOnly()
    {
        Assert.Contains("team-rollup", SkillCatalog.All.Keys);
        IReadOnlyDictionary<string, string> files = SkillCatalog.All["team-rollup"];

        SkillValidation validation = SkillValidator.Validate("team-rollup", files);

        Assert.Equal(["SKILL.md"], files.Keys.ToList());
        Assert.Empty(validation.Issues);
        Assert.Equal(["list_teams"], validation.Tools);
    }

    /// <summary>The catalog holds <c>retrospective</c> (Task 68): valid, one file, and its tools list is exactly <c>read_records</c>.</summary>
    [Fact]
    public void All_ContainsRetrospective_GrantingReadRecords()
    {
        IReadOnlyDictionary<string, string> files = SkillCatalog.All["retrospective"];

        SkillValidation validation = SkillValidator.Validate("retrospective", files);

        Assert.Equal(["SKILL.md"], files.Keys.ToList());
        Assert.Empty(validation.Issues);
        Assert.Equal(["read_records", "propose_retro_changes", "mark_retro_row"], validation.Tools);
    }

    /// <summary>The catalog lists both team skills by name next to <c>team-building</c> (Task 68).</summary>
    [Fact]
    public void All_ListsTeamBuildingTeamManagementAndRetrospective()
    {
        Assert.Contains("team-building", SkillCatalog.All.Keys);
        Assert.Contains("team-management", SkillCatalog.All.Keys);
        Assert.Contains("retrospective", SkillCatalog.All.Keys);
    }

    /// <summary>
    /// Every embedded file's text has already been normalised to <c>\n</c> line endings: no value
    /// contains a carriage return.
    /// </summary>
    [Fact]
    public void All_FileText_HasNoCarriageReturns()
    {
        Assert.NotEmpty(SkillCatalog.All);

        foreach (var files in SkillCatalog.All.Values)
        {
            foreach (var text in files.Values)
            {
                Assert.DoesNotContain('\r', text);
            }
        }
    }

    /// <summary>
    /// Enforces Spec §6.1 (Constraints): a shipped Skill that fails <see cref="SkillValidator.Validate"/>
    /// is a test failure here, never a runtime warning. Every Skill in <see cref="SkillCatalog.All"/>
    /// must come back with zero Issues at any severity — Info and Warning included, not just Error —
    /// and <c>team-building</c>'s two-entry <c>tools: [validate_agent, propose_agents]</c> flow
    /// list must survive into <see cref="SkillValidation.Tools"/> unchanged, in order.
    /// </summary>
    [Fact]
    public void All_EveryShippedSkill_PassesValidationWithNoErrorsOrWarnings()
    {
        foreach (var (skillName, files) in SkillCatalog.All)
        {
            SkillValidation validation = SkillValidator.Validate(skillName, files);

            Assert.True(
                validation.Issues.Count == 0,
                $"Shipped Skill '{skillName}' has validation issues:\n" +
                string.Join('\n', validation.Issues.Select(issue => $"  [{issue.Severity}] {issue.Message}")));
        }

        SkillValidation teamBuildingValidation = SkillValidator.Validate("team-building", SkillCatalog.All["team-building"]);
        Assert.Equal(ExpectedTools.ToList(), teamBuildingValidation.Tools);
    }

    /// <summary>
    /// The shipped <c>team-building</c> Skill honours its content contract (Spec §6.13). Checks the
    /// <c>tools</c> field, prohibited tool prefix, grantable tool names, file naming, description
    /// length, and greeting turn wording.
    /// </summary>
    [Fact]
    public void TeamBuilding_Tools_AreExactlyTheGrantableTwo()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> toolViolations = violations.Where(v => v.StartsWith("tools:", StringComparison.Ordinal)).ToList();
        Assert.Empty(toolViolations);
    }

    /// <summary>
    /// No file in the shipped <c>team-building</c> Skill contains the reserved MCP tool prefix
    /// (Spec §6.13 Constraints, §12 F-1).
    /// </summary>
    [Fact]
    public void TeamBuilding_NoFileContainsToolPrefix()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> prefixViolations = violations.Where(v => v.StartsWith("prefix:", StringComparison.Ordinal)).ToList();
        Assert.Empty(prefixViolations);
    }

    /// <summary>
    /// The shipped <c>team-building</c> SKILL.md names <c>propose_agents</c>, never
    /// <c>create_agent</c>, and no file mentions that obsolete tool name (Spec §6.13 Changes).
    /// </summary>
    [Fact]
    public void TeamBuilding_SkillMd_NamesProposeAgents_NeverCreateAgent()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> createAgentViolations = violations.Where(v => v.StartsWith("create_agent:", StringComparison.Ordinal)).ToList();
        Assert.Empty(createAgentViolations);
    }

    /// <summary>
    /// Every supporting file in the shipped <c>team-building</c> Skill other than <c>SKILL.md</c> is
    /// named in <c>SKILL.md</c> (Spec §6.13, rule 4).
    /// </summary>
    [Fact]
    public void TeamBuilding_EverySupportingFile_IsNamedInSkillMd()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> unnamedViolations = violations.Where(v => v.StartsWith("unnamed-file:", StringComparison.Ordinal)).ToList();
        Assert.Empty(unnamedViolations);
    }

    /// <summary>
    /// The shipped <c>team-building</c> Skill's description is at most 300 characters (Spec §6.13,
    /// rule 5).
    /// </summary>
    [Fact]
    public void TeamBuilding_Description_AtMost300Chars()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> descriptionViolations = violations.Where(v => v.StartsWith("description:", StringComparison.Ordinal)).ToList();
        Assert.Empty(descriptionViolations);
    }

    /// <summary>
    /// The shipped <c>team-building</c> SKILL.md's step 0 contains the agreed greeting turn wording
    /// (Spec §6.13, rule 6).
    /// </summary>
    [Fact]
    public void TeamBuilding_SkillMdStep0_KeysOnGreetingTurn()
    {
        List<string> violations = ContentContractViolations(SkillCatalog.All["team-building"]);
        List<string> step0Violations = violations.Where(v => v.StartsWith("step0:", StringComparison.Ordinal)).ToList();
        Assert.Empty(step0Violations);
    }

    /// <summary>
    /// RS §6.11 (Coordinators and Following, RS-T13): the shipped <c>team-patterns.md</c> teaches a
    /// coordinator following several work Rooms how to keep an overview - one memory file per piece
    /// of work, a work Room reporting back by Mentioning the coordinator (a worker cannot post into
    /// the coordinator's own Room with the Human: it is not a Member there), and reading the
    /// <c>create_room</c> seed as that Room's own first Message. Asserts on short, stable phrases
    /// per this task's own instruction, not whole sentences.
    /// </summary>
    [Fact]
    public void TeamBuilding_TeachesCoordinatorRoutes()
    {
        string teamPatterns = SkillCatalog.All["team-building"]["team-patterns.md"];

        Assert.Contains("memory folder", teamPatterns, StringComparison.Ordinal);
        Assert.Contains("Mention you", teamPatterns, StringComparison.Ordinal);
        Assert.Contains("seed", teamPatterns, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shipped <c>team-building</c> Skill teaches the team-building step (Task 25, HR-13): it asks
    /// the Human which team type they want, names the built-in Development Team as <c>development</c>,
    /// and proposes Candidates by role rather than inventing one. Asserts on short, stable phrases.
    /// </summary>
    [Fact]
    public void TeamBuilding_ProposesCandidatesByRole()
    {
        string skill = SkillCatalog.All["team-building"]["SKILL.md"];

        // contains-ok: shipped Skill prose, not markup
        Assert.Contains("role:", skill, StringComparison.Ordinal);
        // contains-ok: shipped Skill prose, not markup
        Assert.Contains("development/", skill, StringComparison.Ordinal);
    }

    /// <summary>The shipped <c>team-building</c> Skill tells the Agent what to do when <c>propose_agents</c> refuses a role whose Skill or Pack is not ready (#563).</summary>
    [Fact]
    public void TeamBuilding_TeachesWhatToDoWhenARoleCannotBeHiredYet()
    {
        string skill = SkillCatalog.All["team-building"]["SKILL.md"];

        Assert.Contains("A role cannot be hired yet because a Skill or Pack it needs is not ready", skill, StringComparison.Ordinal); // contains-ok: exact row of model-facing text
        Assert.Contains("A role whose Skills or Packs are not ready is refused", skill, StringComparison.Ordinal); // contains-ok: exact rule sentence of model-facing text
    }

    /// <summary>
    /// The content contract checks can fail when a Skill violates it. This test builds an in-memory
    /// copy of the shipped <c>team-building</c> files, mutates its <c>SKILL.md</c> frontmatter and
    /// body to violate the contract, and asserts the helper catches both violations.
    /// </summary>
    [Fact]
    public void ContentContract_PreSpecDraft_ReportsViolations()
    {
        // Create a mutable copy of team-building files.
        Dictionary<string, string> files = new(SkillCatalog.All["team-building"], StringComparer.Ordinal);

        // Mutate SKILL.md: change frontmatter tools to list create_agent instead of validate_agent.
        string skillMd = files["SKILL.md"];
        string mutatedSkillMd = skillMd.Replace(
            "tools: [validate_agent, propose_agents]",
            "tools: [validate_agent, create_agent]",
            StringComparison.Ordinal);

        // Also add a mention of create_agent to the body so it fails the create_agent check too.
        mutatedSkillMd = mutatedSkillMd.Replace(
            "Call `validate_agent`",
            "Call `create_agent` and `validate_agent`",
            StringComparison.Ordinal);

        files["SKILL.md"] = mutatedSkillMd;

        // Check violations.
        List<string> violations = ContentContractViolations(files);

        // Assert both a tools: violation and a create_agent: violation are reported.
        List<string> toolsViolations = violations.Where(v => v.StartsWith("tools:", StringComparison.Ordinal)).ToList();
        List<string> createAgentViolations = violations.Where(v => v.StartsWith("create_agent:", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(toolsViolations);
        Assert.NotEmpty(createAgentViolations);
    }

    /// <summary>
    /// Validates a Skill's files against the content contract (Spec §6.13). Returns one human-readable
    /// violation string per violation found, each prefixed with a kind tag: <c>tools:</c>,
    /// <c>prefix:</c>, <c>create_agent:</c>, <c>unnamed-file:</c>, <c>description:</c>, or
    /// <c>step0:</c>. An empty list means the Skill is compliant.
    /// </summary>
    /// <param name="files">Every file in the Skill, keyed by file name.</param>
    /// <returns>Violation strings, one per violation, each tagged with its kind.</returns>
    private static List<string> ContentContractViolations(IReadOnlyDictionary<string, string> files)
    {
        List<string> violations = [];

        // Rule 1: tools must be exactly [validate_agent, propose_agents].
        // We need to check the raw frontmatter because SkillValidator.Validate drops ungrantable tools.
        if (files.TryGetValue("SKILL.md", out string? skillMdText))
        {
            (IReadOnlyList<PersonaFrontmatterField> fields, _) = PersonaFrontmatter.Parse(skillMdText);
            string? rawTools = null;
            foreach (var field in fields)
            {
                if (string.Equals(field.Key, "tools", StringComparison.OrdinalIgnoreCase))
                {
                    rawTools = field.Value;
                    break;
                }
            }

            if (rawTools is not null)
            {
                // Parse the flow list and check it's exactly the two grantable tools.
                List<string> toolsInFrontmatter = [];
                foreach (var tool in rawTools.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    toolsInFrontmatter.Add(tool);
                }

                if (!toolsInFrontmatter.SequenceEqual(ExpectedTools, StringComparer.Ordinal))
                {
                    violations.Add($"tools: frontmatter lists {string.Join(", ", toolsInFrontmatter)}, expected {string.Join(", ", ExpectedTools)}");
                }
            }
            else
            {
                violations.Add("tools: frontmatter is missing the 'tools' field");
            }
        }

        // Rule 2: no file contains mcp__.
        foreach (var (fileName, text) in files)
        {
            if (text.Contains("mcp__", StringComparison.Ordinal))
            {
                violations.Add($"prefix: file '{fileName}' contains the reserved tool prefix 'mcp__'");
                break;
            }
        }

        // Rule 3: SKILL.md names propose_agents and no file contains create_agent.
        bool skillMdNamesPropose = false;
        bool anyFileNamesCreateAgent = false;

        if (files.TryGetValue("SKILL.md", out skillMdText) &&
            skillMdText.Contains("propose_agents", StringComparison.Ordinal))
        {
            skillMdNamesPropose = true;
        }

        foreach (var (_, text) in files)
        {
            if (text.Contains("create_agent", StringComparison.Ordinal))
            {
                anyFileNamesCreateAgent = true;
                break;
            }
        }

        if (!skillMdNamesPropose)
        {
            violations.Add("create_agent: SKILL.md does not name 'propose_agents'");
        }

        if (anyFileNamesCreateAgent)
        {
            violations.Add("create_agent: at least one file mentions the obsolete 'create_agent' tool");
        }

        // Rule 4: every file other than SKILL.md is named in SKILL.md.
        if (files.TryGetValue("SKILL.md", out skillMdText))
        {
            foreach (var (fileName, _) in files)
            {
                if (!string.Equals(fileName, "SKILL.md", StringComparison.Ordinal) &&
                    !skillMdText.Contains(fileName, StringComparison.Ordinal))
                {
                    violations.Add($"unnamed-file: supporting file '{fileName}' is not mentioned in SKILL.md");
                }
            }
        }

        // Rule 5: description <= 300 characters.
        SkillValidation validation = SkillValidator.Validate("team-building", files);
        if (validation.Description is not null && validation.Description.Length > 300)
        {
            violations.Add($"description: {validation.Description.Length} characters exceeds the limit of 300");
        }

        // Rule 6: SKILL.md contains "greet" and "has not written" (case-insensitive).
        if (files.TryGetValue("SKILL.md", out skillMdText))
        {
            bool hasGreet = skillMdText.Contains("greet", StringComparison.OrdinalIgnoreCase);
            bool hasHasNotWritten = skillMdText.Contains("has not written", StringComparison.OrdinalIgnoreCase);

            if (!hasGreet)
            {
                violations.Add("step0: SKILL.md does not contain 'greet'");
            }

            if (!hasHasNotWritten)
            {
                violations.Add("step0: SKILL.md does not contain 'has not written'");
            }
        }

        return violations;
    }
}

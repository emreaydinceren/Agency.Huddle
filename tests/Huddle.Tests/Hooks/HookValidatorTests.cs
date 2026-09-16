namespace Agency.Huddle.Tests.Hooks;

using Agency.Huddle.App.Hooks;

/// <summary>
/// Tests for <see cref="HookValidator"/>: that it surfaces the two silent failure modes
/// <c>docs/agencyteam/rules.md</c> documents as binding — a missing <c>mcp__team__</c> tool name (rule
/// 32) and a Room label that lost its id (rule 33) — plus the related placeholder mistakes, and that it
/// never throws regardless of how malformed its input is.
/// </summary>
public sealed class HookValidatorTests
{
    /// <summary>The seven tool names a running agent roster actually has, already prefixed.</summary>
    private static readonly IReadOnlyList<string> AllToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__list_agents",
        "mcp__team__create_room",
        "mcp__team__invite_agent",
        "mcp__team__post_message",
        "mcp__team__follow_room",
        "mcp__team__unfollow_room",
    ];

    /// <summary>
    /// Rule 33: stripping <c>{{roomId}}</c> out of the <c>turn.roomLabel</c> default is reported as an
    /// <see cref="HookIssueSeverity.Error"/> naming that placeholder.
    /// </summary>
    [Fact]
    public void Validate_RoomLabelWithoutRoomId_ProducesErrorNamingRoomId()
    {
        var definition = HookCatalog.Get("turn.roomLabel");
        var textWithoutRoomId = "[Room: {{roomName}}]";

        var issues = HookValidator.Validate(definition, textWithoutRoomId);

        Assert.Contains(issues, issue =>
            issue.Severity == HookIssueSeverity.Error &&
            issue.Message.Contains("{{roomId}}", StringComparison.Ordinal));
    }

    /// <summary>The unmodified <c>turn.roomLabel</c> default, which does carry <c>{{roomId}}</c>, produces no such error.</summary>
    [Fact]
    public void Validate_RoomLabelWithRoomId_ProducesNoRequiredPlaceholderError()
    {
        var definition = HookCatalog.Get("turn.roomLabel");

        var issues = HookValidator.Validate(definition, definition.Default);

        Assert.DoesNotContain(issues, issue => issue.Severity == HookIssueSeverity.Error);
    }

    /// <summary>A required placeholder missing from any hook's text is an <see cref="HookIssueSeverity.Error"/>.</summary>
    [Fact]
    public void Validate_MissingRequiredPlaceholder_ProducesError()
    {
        var definition = HookCatalog.Get("systemPrompt.identity");

        var issues = HookValidator.Validate(definition, "You are a member of the Team chat application.");

        var error = Assert.Single(issues);
        Assert.Equal(HookIssueSeverity.Error, error.Severity);
        Assert.Contains("{{personaName}}", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A <c>{{...}}</c> token not declared on the hook is a <see cref="HookIssueSeverity.Warning"/>, not an error.</summary>
    [Fact]
    public void Validate_UnknownPlaceholderToken_ProducesWarning()
    {
        var definition = HookCatalog.Get("systemPrompt.chatRules");

        var issues = HookValidator.Validate(definition, "Some text with a {{typoPlaceholder}} in it.");

        var warning = Assert.Single(issues);
        Assert.Equal(HookIssueSeverity.Warning, warning.Severity);
        Assert.Contains("{{typoPlaceholder}}", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>Text that uses only declared placeholders produces no unknown-placeholder warning.</summary>
    [Fact]
    public void Validate_OnlyDeclaredPlaceholders_ProducesNoWarning()
    {
        var definition = HookCatalog.Get("turn.message");

        var issues = HookValidator.Validate(definition, definition.Default);

        Assert.DoesNotContain(issues, issue => issue.Severity == HookIssueSeverity.Warning);
    }

    /// <summary>Null, empty and whitespace-only text are each reported as a blank-text <see cref="HookIssueSeverity.Error"/>.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankText_ProducesError(string? blankText)
    {
        var definition = HookCatalog.Get("systemPrompt.chatRules");

        var issues = HookValidator.Validate(definition, blankText);

        var error = Assert.Single(issues);
        Assert.Equal(HookIssueSeverity.Error, error.Severity);
    }

    /// <summary>Ordinary, non-blank text produces no blank-text error.</summary>
    [Fact]
    public void Validate_NonBlankText_ProducesNoBlankTextError()
    {
        var definition = HookCatalog.Get("systemPrompt.chatRules");

        var issues = HookValidator.Validate(definition, definition.Default);

        Assert.DoesNotContain(issues, issue => issue.Message.Contains("empty or whitespace", StringComparison.Ordinal));
    }

    /// <summary>A null <see cref="HookDefinition"/> is reported as findings, never thrown.</summary>
    [Fact]
    public void Validate_NullDefinition_DoesNotThrow()
    {
        var issues = HookValidator.Validate(null, "some text");

        Assert.NotNull(issues);
    }

    /// <summary>Both a null definition and null text together are reported as findings, never thrown.</summary>
    [Fact]
    public void Validate_NullDefinitionAndNullText_DoesNotThrow()
    {
        var issues = HookValidator.Validate(null, null);

        var error = Assert.Single(issues);
        Assert.Equal(HookIssueSeverity.Error, error.Severity);
    }

    /// <summary>Every default text shipped in <see cref="HookCatalog"/> validates clean against its own definition.</summary>
    [Fact]
    public void Validate_EveryCatalogDefault_ValidatesClean()
    {
        foreach (var hook in HookCatalog.All)
        {
            var issues = HookValidator.Validate(hook, hook.Default);

            Assert.True(
                issues.Count == 0,
                $"Hook '{hook.Key}' did not validate clean: " +
                string.Join("; ", issues.Select(issue => $"[{issue.Severity}] {issue.Message}")));
        }
    }

    /// <summary>An override keyed by a name absent from <see cref="HookCatalog"/> is a <see cref="HookIssueSeverity.Warning"/>.</summary>
    [Fact]
    public void ValidateAll_UnknownKey_ProducesWarning()
    {
        var overrides = new Dictionary<string, string?> { ["not.a.real.hook"] = "some text" };

        var issues = HookValidator.ValidateAll(overrides);

        var warning = Assert.Single(issues);
        Assert.Equal(HookIssueSeverity.Warning, warning.Severity);
        Assert.Contains("not.a.real.hook", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>An override keyed by a real hook, with clean text, produces no issues.</summary>
    [Fact]
    public void ValidateAll_KnownKeyWithCleanText_ProducesNoIssues()
    {
        var definition = HookCatalog.Get("systemPrompt.identity");
        var overrides = new Dictionary<string, string?> { [definition.Key] = definition.Default };

        var issues = HookValidator.ValidateAll(overrides);

        Assert.Empty(issues);
    }

    /// <summary>An override keyed by a real hook, missing a required placeholder, is reported through <see cref="HookValidator.ValidateAll"/>.</summary>
    [Fact]
    public void ValidateAll_KnownKeyMissingRequiredPlaceholder_ProducesError()
    {
        var overrides = new Dictionary<string, string?> { ["turn.roomLabel"] = "[Room: {{roomName}}]" };

        var issues = HookValidator.ValidateAll(overrides);

        Assert.Contains(issues, issue => issue.Severity == HookIssueSeverity.Error);
    }

    /// <summary>A null overrides dictionary is reported as no findings, never thrown.</summary>
    [Fact]
    public void ValidateAll_NullOverrides_DoesNotThrow()
    {
        var issues = HookValidator.ValidateAll(null);

        Assert.Empty(issues);
    }

    /// <summary>
    /// Rule 32: a rendered system prompt missing one of the five <c>mcp__team__</c> tool names produces
    /// an <see cref="HookIssueSeverity.Error"/> naming that exact tool.
    /// </summary>
    [Fact]
    public void ValidateSystemPrompt_MissingOneToolName_ProducesErrorNamingIt()
    {
        var renderedPrompt = string.Join(
            " ",
            AllToolNames.Where(name => !string.Equals(name, "mcp__team__invite_agent", StringComparison.Ordinal)));

        var issues = HookValidator.ValidateSystemPrompt(renderedPrompt, AllToolNames);

        var error = Assert.Single(issues);
        Assert.Equal(HookIssueSeverity.Error, error.Severity);
        Assert.Equal("mcp__team__invite_agent", error.Key);
    }

    /// <summary>A rendered prompt containing all five tool names produces no issues.</summary>
    [Fact]
    public void ValidateSystemPrompt_AllToolNamesPresent_ProducesNoIssues()
    {
        var renderedPrompt = "Here are your tools: " + string.Join(", ", AllToolNames) + ".";

        var issues = HookValidator.ValidateSystemPrompt(renderedPrompt, AllToolNames);

        Assert.Empty(issues);
    }

    /// <summary>A null rendered prompt is reported as an error per missing tool, never thrown.</summary>
    [Fact]
    public void ValidateSystemPrompt_NullRenderedPrompt_DoesNotThrow()
    {
        var issues = HookValidator.ValidateSystemPrompt(null, AllToolNames);

        Assert.Equal(AllToolNames.Count, issues.Count);
        Assert.All(issues, issue => Assert.Equal(HookIssueSeverity.Error, issue.Severity));
    }

    /// <summary>A null tool name list is reported as no findings, never thrown.</summary>
    [Fact]
    public void ValidateSystemPrompt_NullToolNames_DoesNotThrow()
    {
        var issues = HookValidator.ValidateSystemPrompt("any prompt text", null);

        Assert.Empty(issues);
    }
}

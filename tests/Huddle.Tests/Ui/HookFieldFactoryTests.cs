using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for <see cref="HookFieldFactory.Build"/>, the pure grouping logic behind the Settings page's Hooks tab.</summary>
public sealed class HookFieldFactoryTests
{
    /// <summary>An empty pending-edits dictionary, for tests that only care about the stored/default relationship.</summary>
    private static readonly Dictionary<string, string> NoPendingEdits = new(StringComparer.Ordinal);

    /// <summary>Every hook in the catalog shows up somewhere in the built groups.</summary>
    [Fact]
    public void Build_UntouchedSource_ListsEveryCatalogHook()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks, NoPendingEdits);

        var keys = groups.SelectMany(group => group.Fields).Select(field => field.Key).ToList();
        Assert.Equal(HookCatalog.All.Count, keys.Count);
        foreach (var definition in HookCatalog.All)
        {
            Assert.Contains(definition.Key, keys);
        }
    }

    /// <summary>The four groups appear in the documented order, with the labels the Settings page shows.</summary>
    [Fact]
    public void Build_GroupsInOrder_SystemPromptTurnGetHelpTool()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks, NoPendingEdits);

        Assert.Equal(
            ["System prompt", "Turn", "Get help", "Tool descriptions"],
            groups.Select(group => group.Label).ToList());
    }

    /// <summary>Within a group, fields keep the catalog's own order rather than being re-sorted.</summary>
    [Fact]
    public void Build_WithinAGroup_KeepsHookCatalogOrder()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks, NoPendingEdits);

        var expectedSystemPromptKeys = HookCatalog.All
            .Where(definition => definition.Key.StartsWith("systemPrompt.", StringComparison.Ordinal))
            .Select(definition => definition.Key)
            .ToList();

        var systemPromptGroup = groups.Single(group => group.Label == "System prompt");
        Assert.Equal(expectedSystemPromptKeys, systemPromptGroup.Fields.Select(field => field.Key).ToList());
    }

    /// <summary>A hook with no configured override and no pending edit is neither modified nor unsaved.</summary>
    [Fact]
    public void Build_UntouchedSource_IsModifiedAndHasUnsavedChangeAreFalse()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks, NoPendingEdits);

        Assert.All(groups.SelectMany(group => group.Fields), field => Assert.False(field.IsModified));
        Assert.All(groups.SelectMany(group => group.Fields), field => Assert.False(field.HasUnsavedChange));
    }

    /// <summary>A hook with a configured override that differs from the default is flagged modified, and - with no pending edit on top of it - not unsaved.</summary>
    [Fact]
    public void Build_KeyWithOverride_IsModifiedIsTrueAndHasUnsavedChangeIsFalse()
    {
        var hooks = new FakeHookSource();
        hooks.SetOverride("turn.roomLabel", "custom room label {{roomId}}");

        var groups = HookFieldFactory.Build(hooks, NoPendingEdits);

        var field = groups.SelectMany(group => group.Fields).Single(f => f.Key == "turn.roomLabel");
        Assert.True(field.IsModified);
        Assert.False(field.HasUnsavedChange);
        Assert.Equal("custom room label {{roomId}}", field.Value);
    }

    /// <summary>
    /// The three-state distinction this factory exists to get right: a pending edit wins over the
    /// stored value for <see cref="HookFieldState.Value"/>, and the two flags it drives are
    /// independent. Here the pending text differs from both the default and the stored override, so
    /// both flags are true.
    /// </summary>
    [Fact]
    public void Build_PendingEditDiffersFromStoredAndDefault_BothFlagsAreTrue()
    {
        var hooks = new FakeHookSource();
        hooks.SetOverride("getHelp.intro", "a previously saved override");
        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["getHelp.intro"] = "text typed just now, not yet saved",
        };

        var groups = HookFieldFactory.Build(hooks, pendingEdits);

        var field = groups.SelectMany(group => group.Fields).Single(f => f.Key == "getHelp.intro");
        Assert.Equal("text typed just now, not yet saved", field.Value);
        Assert.True(field.IsModified);
        Assert.True(field.HasUnsavedChange);
    }

    /// <summary>
    /// <see cref="HookFieldState.IsModified"/> and <see cref="HookFieldState.HasUnsavedChange"/> are
    /// independent: a pending edit equal to the stored override (itself already equal to the
    /// default) is neither modified nor unsaved, even though a pending edit exists in the dictionary.
    /// </summary>
    [Fact]
    public void Build_PendingEditEqualToStoredDefault_NeitherFlagIsTrue()
    {
        var hooks = new FakeHookSource();
        var definition = HookCatalog.Get("getHelp.intro");
        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["getHelp.intro"] = definition.Default,
        };

        var groups = HookFieldFactory.Build(hooks, pendingEdits);

        var field = groups.SelectMany(group => group.Fields).Single(f => f.Key == "getHelp.intro");
        Assert.False(field.IsModified);
        Assert.False(field.HasUnsavedChange);
    }

    /// <summary>
    /// The Reset case: staging the default over an already-overridden stored value makes
    /// <see cref="HookFieldState.HasUnsavedChange"/> true (there is something to save) while
    /// <see cref="HookFieldState.IsModified"/> is false (the pending value matches the shipped
    /// default) - the exact combination <see cref="HookFieldState"/>'s remarks call out as the one
    /// most likely to be got backwards.
    /// </summary>
    [Fact]
    public void Build_PendingEditEqualToDefaultButStoredIsAnOverride_IsModifiedFalseHasUnsavedChangeTrue()
    {
        var hooks = new FakeHookSource();
        var definition = HookCatalog.Get("getHelp.intro");
        hooks.SetOverride(definition.Key, "a previously saved override");
        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [definition.Key] = definition.Default,
        };

        var groups = HookFieldFactory.Build(hooks, pendingEdits);

        var field = groups.SelectMany(group => group.Fields).Single(f => f.Key == definition.Key);
        Assert.False(field.IsModified);
        Assert.True(field.HasUnsavedChange);
    }

    /// <summary>An invalid pending value (missing a required placeholder) surfaces as an issue on that field.</summary>
    [Fact]
    public void Build_PendingEditMissingRequiredPlaceholder_ProducesAnIssue()
    {
        var hooks = new FakeHookSource();
        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["turn.roomLabel"] = "[Room: {{roomName}}]",
        };

        var groups = HookFieldFactory.Build(hooks, pendingEdits);

        var field = groups.SelectMany(group => group.Fields).Single(f => f.Key == "turn.roomLabel");
        Assert.Contains(field.Issues, issue => issue.Severity == HookIssueSeverity.Error);
    }

    /// <summary>A field with no problems has no issues.</summary>
    [Fact]
    public void Build_UntouchedSource_HasNoIssues()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks, NoPendingEdits);

        Assert.All(groups.SelectMany(group => group.Fields), field => Assert.Empty(field.Issues));
    }

    /// <summary>
    /// <see cref="HookFieldFactory.StageAllDefaults"/> - the "Reset all to defaults" logic - stages
    /// every catalog key's default into the pending-edits map, so that once
    /// <see cref="HookFieldFactory.Build"/> is run against it every field reports
    /// <see cref="HookFieldState.IsModified"/> false, regardless of what override was configured
    /// beforehand.
    /// </summary>
    [Fact]
    public void StageAllDefaults_ThenBuild_EveryFieldReportsIsModifiedFalse()
    {
        var hooks = new FakeHookSource();
        hooks.SetOverride("turn.roomLabel", "custom room label {{roomId}}");
        hooks.SetOverride("getHelp.intro", "a custom introduction");
        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal);

        HookFieldFactory.StageAllDefaults(pendingEdits);
        var groups = HookFieldFactory.Build(hooks, pendingEdits);

        Assert.All(groups.SelectMany(group => group.Fields), field => Assert.False(field.IsModified));
    }

    /// <summary>Staging every default touches only the in-memory pending-edits map - it never calls into a hook source or any storage.</summary>
    [Fact]
    public void StageAllDefaults_StagesExactlyOneEntryPerCatalogKey()
    {
        var pendingEdits = new Dictionary<string, string>(StringComparer.Ordinal);

        HookFieldFactory.StageAllDefaults(pendingEdits);

        Assert.Equal(HookCatalog.All.Count, pendingEdits.Count);
        foreach (var definition in HookCatalog.All)
        {
            Assert.Equal(definition.Default, pendingEdits[definition.Key]);
        }
    }

    /// <summary>A single-line value needs two rows: one for the line, one so it still reads as a box rather than a slot.</summary>
    [Fact]
    public void RowsFor_OneLineValue_ReturnsTwo()
    {
        var rows = HookFieldFactory.RowsFor("{{roomLabel}} {{sender}}: {{text}}");

        Assert.Equal(2, rows);
    }

    /// <summary>A multi-line value gets one row per line plus one, unclamped while still short.</summary>
    [Fact]
    public void RowsFor_MultiLineValue_ReturnsLineCountPlusOne()
    {
        var rows = HookFieldFactory.RowsFor("line one\nline two\nline three");

        Assert.Equal(4, rows);
    }

    /// <summary>A value with more newlines than the clamp allows is capped at 14 rows, not left to dominate the page.</summary>
    [Fact]
    public void RowsFor_ValueLongerThanTheClamp_IsCappedAtFourteen()
    {
        var value = string.Join('\n', Enumerable.Repeat("a line", 30));

        var rows = HookFieldFactory.RowsFor(value);

        Assert.Equal(14, rows);
    }

    /// <summary>A null or empty value is not an error - it just needs the minimum of two rows.</summary>
    [Fact]
    public void RowsFor_NullOrEmptyValue_ReturnsTheMinimum()
    {
        Assert.Equal(2, HookFieldFactory.RowsFor(null));
        Assert.Equal(2, HookFieldFactory.RowsFor(string.Empty));
    }
}

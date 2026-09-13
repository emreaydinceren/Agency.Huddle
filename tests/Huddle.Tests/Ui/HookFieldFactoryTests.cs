using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Hooks;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for <see cref="HookFieldFactory.Build"/>, the pure grouping logic behind the Settings page's Hooks tab.</summary>
public sealed class HookFieldFactoryTests
{
    /// <summary>Every hook in the catalog shows up somewhere in the built groups.</summary>
    [Fact]
    public void Build_UntouchedSource_ListsEveryCatalogHook()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks);

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

        var groups = HookFieldFactory.Build(hooks);

        Assert.Equal(
            ["System prompt", "Turn", "Get help", "Tool descriptions"],
            groups.Select(group => group.Label).ToList());
    }

    /// <summary>Within a group, fields keep the catalog's own order rather than being re-sorted.</summary>
    [Fact]
    public void Build_WithinAGroup_KeepsHookCatalogOrder()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks);

        var expectedSystemPromptKeys = HookCatalog.All
            .Where(definition => definition.Key.StartsWith("systemPrompt.", StringComparison.Ordinal))
            .Select(definition => definition.Key)
            .ToList();

        var systemPromptGroup = groups.Single(group => group.Label == "System prompt");
        Assert.Equal(expectedSystemPromptKeys, systemPromptGroup.Fields.Select(field => field.Key).ToList());
    }

    /// <summary>A hook with no configured override is not modified.</summary>
    [Fact]
    public void Build_UntouchedSource_IsModifiedIsFalse()
    {
        var hooks = new FakeHookSource();

        var groups = HookFieldFactory.Build(hooks);

        Assert.All(groups.SelectMany(group => group.Fields), field => Assert.False(field.IsModified));
    }

    /// <summary>A hook with a configured override that differs from the default is flagged modified.</summary>
    [Fact]
    public void Build_KeyWithOverride_IsModifiedIsTrue()
    {
        var hooks = new FakeHookSource();
        hooks.SetOverride("turn.roomLabel", "custom room label {{roomId}}");

        var groups = HookFieldFactory.Build(hooks);

        var field = groups.SelectMany(group => group.Fields).Single(f => f.Key == "turn.roomLabel");
        Assert.True(field.IsModified);
        Assert.Equal("custom room label {{roomId}}", field.Value);
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

using Agency.Huddle.App.Prompts;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for <see cref="PlaceholderCatalog"/>: every token a prompt can hold says what Huddle puts there.</summary>
public sealed class PlaceholderCatalogTests
{
    /// <summary>Every placeholder any prompt in <see cref="PromptCatalog"/> lists has a description, so the Prompts tab's table is never blank.</summary>
    [Fact]
    public void EveryCatalogPlaceholder_HasADescription()
    {
        var tokens = PromptCatalog.All.SelectMany(definition => definition.Placeholders).Distinct(StringComparer.Ordinal);

        var undescribed = tokens.Where(token => PlaceholderCatalog.Describe(token) is null).ToList();

        Assert.True(undescribed.Count == 0, "No description for: " + string.Join(", ", undescribed));
    }

    /// <summary>No description is empty or only white space.</summary>
    [Fact]
    public void NoDescription_IsEmpty()
    {
        foreach (var token in PlaceholderCatalog.Tokens)
        {
            Assert.False(string.IsNullOrWhiteSpace(PlaceholderCatalog.Describe(token)), token);
        }
    }

    /// <summary>A description for a token no prompt lists is dead text, so each entry names a placeholder that exists in the catalog.</summary>
    [Fact]
    public void EveryDescription_NamesAPlaceholderSomePromptLists()
    {
        var listed = PromptCatalog.All.SelectMany(definition => definition.Placeholders).ToHashSet(StringComparer.Ordinal);

        var orphans = PlaceholderCatalog.Tokens.Where(token => !listed.Contains(token)).ToList();

        Assert.Empty(orphans);
    }
}

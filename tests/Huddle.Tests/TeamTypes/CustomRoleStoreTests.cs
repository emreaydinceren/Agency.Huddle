using Agency.Huddle.App.HiringPool;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="CustomRoleStore"/>.</summary>
public sealed class CustomRoleStoreTests
{
    /// <summary>An added role is trimmed, given a <c>custom-</c> id, and still there when the store is read again.</summary>
    [Fact]
    public void Add_StoresTheRole_AndAFreshStoreReadsItBack()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());

        CustomRole added = store.Add("  Gardener ", " Grows things. ", "You garden.", ModelTier.Fast);

        Assert.Equal("custom-gardener", added.Id);
        Assert.Equal("Gardener", added.Title);
        CustomRole again = Assert.Single(new CustomRoleStore(dir.Options()).Roles);
        Assert.Equal(added with { Skills = [] }, again with { Skills = [] });
        Assert.Equal(added.Skills, again.Skills);
    }

    /// <summary>A role's Skills are kept without repeats and read back; a file written before Skills existed reads with none.</summary>
    [Fact]
    public void Skills_AreStoredDistinct_AndAnOlderFileReadsWithNone()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());

        _ = store.Add("Gardener", string.Empty, string.Empty, ModelTier.Balanced, ["a-skill", "b-skill", "a-skill"]);

        Assert.Equal(["a-skill", "b-skill"], Assert.Single(new CustomRoleStore(dir.Options()).Roles).Skills);
        File.WriteAllText(Path.Combine(dir.Path, "custom-roles.json"), """{ "roles": [ { "id": "custom-old", "title": "Old", "description": "", "instructions": "", "tier": "balanced" } ] }""");
        Assert.Empty(Assert.Single(new CustomRoleStore(dir.Options()).Roles).Skills);
    }

    /// <summary>Two roles with the same title get different ids.</summary>
    [Fact]
    public void Add_SameTitleTwice_GivesDistinctIds()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());

        CustomRole first = store.Add("Gardener", string.Empty, string.Empty, ModelTier.Balanced);
        CustomRole second = store.Add("Gardener", string.Empty, string.Empty, ModelTier.Balanced);

        Assert.NotEqual(first.Id, second.Id);
    }

    /// <summary>A blank title is refused and nothing is written.</summary>
    [Fact]
    public void Add_BlankTitle_IsRefused()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());

        _ = Assert.Throws<InvalidOperationException>(() => store.Add("  ", string.Empty, string.Empty, ModelTier.Balanced));

        Assert.Empty(store.Roles);
        Assert.False(File.Exists(Path.Combine(dir.Path, "custom-roles.json")));
    }

    /// <summary>Update replaces the fields of the role with that id and keeps its id; an unknown id changes nothing.</summary>
    [Fact]
    public void Update_ReplacesTheRole_ByIdOnly()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());
        CustomRole added = store.Add("Gardener", "Old", "Old", ModelTier.Fast);

        bool changed = store.Update(added with { Title = "Head gardener", Tier = ModelTier.Strongest });
        bool missing = store.Update(new CustomRole("custom-nobody", "X", string.Empty, string.Empty, ModelTier.Fast));

        Assert.True(changed);
        Assert.False(missing);
        CustomRole stored = Assert.Single(new CustomRoleStore(dir.Options()).Roles);
        Assert.Equal(added.Id, stored.Id);
        Assert.Equal("Head gardener", stored.Title);
        Assert.Equal(ModelTier.Strongest, stored.Tier);
    }

    /// <summary>Delete removes the role from the file; an unknown id returns false.</summary>
    [Fact]
    public void Delete_RemovesTheRole()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());
        CustomRole added = store.Add("Gardener", string.Empty, string.Empty, ModelTier.Balanced);

        Assert.True(store.Delete(added.Id));
        Assert.False(store.Delete(added.Id));

        Assert.Empty(new CustomRoleStore(dir.Options()).Roles);
    }

    /// <summary>A file that is not valid JSON is reported, left as it is, and every write is refused.</summary>
    [Fact]
    public void BrokenFile_IsReportedAndNeverOverwritten()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "custom-roles.json");
        File.WriteAllText(path, "{ not json");

        CustomRoleStore store = new(dir.Options());

        Assert.NotNull(store.LoadError);
        _ = Assert.Throws<InvalidOperationException>(() => store.Add("Gardener", string.Empty, string.Empty, ModelTier.Balanced));
        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    /// <summary>The changed event fires once per add, update and delete.</summary>
    [Fact]
    public void Changed_FiresForEachChange()
    {
        using TempDataDir dir = new();
        CustomRoleStore store = new(dir.Options());
        int raised = 0;
        store.Changed += () => raised++;

        CustomRole added = store.Add("Gardener", string.Empty, string.Empty, ModelTier.Balanced);
        _ = store.Update(added with { Description = "More" });
        _ = store.Delete(added.Id);

        Assert.Equal(3, raised);
    }
}

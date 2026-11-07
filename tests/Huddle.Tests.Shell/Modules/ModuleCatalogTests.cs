using Agency.Huddle.App.Modules;

namespace Agency.Huddle.Tests.Modules;

/// <summary>Pins the hand-written list of Feature Modules, whose order sets the order of what they contribute.</summary>
public sealed class ModuleCatalogTests
{
    /// <summary>The catalog lists exactly the migrated features, in order, because a module's tools join the prompt in that order.</summary>
    [Fact]
    public void All_ListsTheModulesInOrder()
    {
        Assert.Equal(["file-changes", "doc-history"], ModuleCatalog.All.Select(static module => module.Id));
    }
}

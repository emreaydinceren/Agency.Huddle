using Agency.Huddle.App.DocHistory;

namespace Agency.Huddle.App.Modules;

/// <summary>
/// The Feature Modules, listed by hand with no scanning, so what runs and in what order can be read here. The order is the order of
/// what they contribute: a module's tools join a session's list in this order, and that list reaches the system prompt.
/// </summary>
internal static class ModuleCatalog
{
    /// <summary>Gets the modules, in the order they contribute.</summary>
    internal static IReadOnlyList<IFeatureModule> All { get; } = [new DocHistoryModule()];
}

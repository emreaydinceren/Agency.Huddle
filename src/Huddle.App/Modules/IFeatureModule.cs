using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Modules;

/// <summary>
/// One feature's wiring, declared in one place: what it registers, what it offers an Agent and what it puts in a Work Dir. It is a way
/// to organise compiled code, not an extension point. Guards, the Reply Gate, the Budget and the order of Turn hooks stay central.
/// </summary>
internal interface IFeatureModule
{
    /// <summary>Gets the module's name in <see cref="ModuleCatalog"/>.</summary>
    string Id { get; }

    /// <summary>Registers the feature's services and sidebar panels. Runs once, while the app is composed.</summary>
    /// <param name="services">The collection to add to.</param>
    void AddServices(IServiceCollection services);

    /// <summary>Returns the tools the feature offers one session, after Skill gating and before <c>get_help</c> is built, so <c>get_help</c> lists them.</summary>
    /// <param name="offer">The session being started.</param>
    /// <returns>The tools, in the order they join the session's list; none by default.</returns>
    IReadOnlyList<IAppTool> OfferTools(ModuleToolOffer offer)
    {
        return [];
    }

    /// <summary>Puts what the feature needs in the Work Dir, before the Adapter process reads that folder's settings. Does nothing by default.</summary>
    /// <param name="workDir">The Work Dir being prepared.</param>
    void PrepareWorkDir(ModuleWorkDir workDir)
    {
    }
}

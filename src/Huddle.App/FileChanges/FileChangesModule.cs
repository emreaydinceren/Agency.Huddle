using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp.Sessions.TurnHooks;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Modules;

namespace Agency.Huddle.App.FileChanges;

/// <summary>
/// The wiring of File Changes (FC spec): its services, its Turn hook and the folder-watch tools. <c>PersonaSupervisor</c> still decides
/// per Persona whether the tracker applies, and the hook's place in the Turn stays in <see cref="TurnHookPipeline"/>.
/// </summary>
internal sealed class FileChangesModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "file-changes";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // FC §6.3: resolves a Watched Folder entry (an Agent Name, a full path, or a path relative to DataDir) into a full path, or refuses it with a reason.
        services.AddSingleton<WatchedFolderResolver>();

        // FC §6.6: one JSON file per Agent under {DataDir}/file-state/, holding what that Agent last saw per Room.
        services.AddSingleton<FileStateStore>();

        // FC §6.7: a singleton, like RoomFollows - the watch_folder/unwatch_folder tools and every runner share it through DI.
        services.AddSingleton<FileChangeTracker>();

        services.AddSingleton<FileChangesTurnHook>();
        services.AddSingleton<ITurnHook>(static sp => sp.GetRequiredService<FileChangesTurnHook>());
    }

    /// <summary>
    /// FC §6.9, §6.11: offers <c>watch_folder</c> and <c>unwatch_folder</c> only when File Changes is on for this installation and the
    /// resolved Adapter Profile can read files at all: an Adapter with no file tools (agency-acp) would otherwise be handed paths it cannot open.
    /// </summary>
    /// <param name="offer">The session being started.</param>
    /// <returns>The two tools, or none.</returns>
    public IReadOnlyList<IAppTool> OfferTools(ModuleToolOffer offer)
    {
        if (!offer.Options.FileChanges.Enabled || !offer.Profile.ReadsFiles)
        {
            return [];
        }

        _ = offer.Services.GetRequiredService<FileChangeTracker>();
        return
        [
            ActivatorUtilities.CreateInstance<WatchFolderTool>(offer.Services, offer.Persona.Name),
            ActivatorUtilities.CreateInstance<UnwatchFolderTool>(offer.Services, offer.Persona.Name),
        ];
    }
}

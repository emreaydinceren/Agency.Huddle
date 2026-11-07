using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Modules;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.App.DocHistory;

/// <summary>The wiring of a document's History (DocHistory spec): its store and writer, its sidebar panel, <c>log_change</c> and the Stop hook.</summary>
internal sealed class DocHistoryModule : IFeatureModule
{
    /// <inheritdoc />
    public string Id => "doc-history";

    /// <inheritdoc />
    public void AddServices(IServiceCollection services)
    {
        // DocHistory spec §6.2, §6.4: the store, its one writer, and the listener that records the Human's saves and moves from LibraryEvents.
        services.AddSingleton<DocHistoryStore>();
        services.AddSingleton<DocHistoryRecorder>();
        services.AddSingleton<DocHistoryLibraryListener>();
        services.AddHostedService(static sp => sp.GetRequiredService<DocHistoryLibraryListener>());
        services.AddSidebarPanel(new SidebarPanel("recent-edits", "History", MudBlazor.Icons.Material.Filled.History, 30,
            static subject => subject is DocumentSubject { Kind: LibraryFileKind.Markdown }, typeof(RecentEditsPanel), static options => options.Library.Enabled && options.DocHistory.Enabled));
    }

    /// <summary>Offers <c>log_change</c> (DocHistory spec §6.3) only to an Adapter that reads files, so an Agent that cannot write files is not asked to report writing one.</summary>
    /// <param name="offer">The session being started.</param>
    /// <returns>The tool, or none.</returns>
    public IReadOnlyList<IAppTool> OfferTools(ModuleToolOffer offer)
    {
        if (offer.Options.DocHistory.Enabled && offer.Profile.ReadsFiles)
        {
            return [ActivatorUtilities.CreateInstance<LogChangeTool>(offer.Services, offer.Persona.Name)];
        }

        return [];
    }

    /// <inheritdoc />
    public void PrepareWorkDir(ModuleWorkDir workDir)
    {
        InstallStopHook(workDir.Path, workDir.DataDir, workDir.ToolNames, workDir.Identity, workDir.Services.GetService<ProcessGateService>(), workDir.Logger);
    }

    /// <summary>
    /// DocHistory spec section 10: when the session offers <c>log_change</c>, puts the Stop hook in the Work Dir before the
    /// Adapter process reads that folder's settings.
    /// </summary>
    /// <param name="workDir">The Persona's Work Dir.</param>
    /// <param name="dataDir">The App's data folder, which keeps the hook script.</param>
    /// <param name="toolNames">The session's tool names, already prefixed.</param>
    /// <param name="identity">The Persona's parsed identity, or <see langword="null"/>. An Agent whose role covers verify also gets the read reminder (#354); one with no role, such as the Chief of Staff, does not.</param>
    /// <param name="gates">Tells which roles cover verify, or <see langword="null"/> when the service is not registered.</param>
    /// <param name="logger">Receives an install failure.</param>
    internal static void InstallStopHook(string workDir, string dataDir, IReadOnlyList<string> toolNames, PersonaIdentity? identity, ProcessGateService? gates, ILogger logger)
    {
        if (SystemPromptComposer.FindLogChangeTool(toolNames) is { } logChangeToolName)
        {
            bool remindReads = gates?.CoversVerify(identity?.Role) == true;
            DocHistoryStopHook.Install(workDir, dataDir, logChangeToolName, logger, remindReads);
        }
    }
}

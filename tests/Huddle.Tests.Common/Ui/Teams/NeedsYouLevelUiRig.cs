using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Feed;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// The services the Needs You level control draws and saves through (ADR-0088), registered on a bUnit context: Team Dev (member Nova, Project Site)
/// whose files are in memory, the stores under a temporary data folder, and the Feed that receives the cap question.
/// </summary>
internal sealed class NeedsYouLevelUiRig : IDisposable
{
    /// <summary>The Team the control sets.</summary>
    public const string Team = "Dev";

    /// <summary>The Project of that Team.</summary>
    public const string Project = "Site";

    private static readonly VersionedRef Type = new("development", "1.0.0");

    private readonly TempDataDir dir = new();

    /// <summary>Registers the services on <paramref name="ctx"/>.</summary>
    /// <param name="ctx">The bUnit context to register on, before anything is rendered.</param>
    /// <param name="globalLevel">The configured global level text, or <see langword="null"/> for none.</param>
    public NeedsYouLevelUiRig(MudBunitContext ctx, string? globalLevel = null)
    {
        this.Catalog = new FakeTeamCatalog { Teams = [new TeamSummary(Team, [Project], ["Nova"], true) { TeamType = Type }] };
        this.Folders = new FakeTeamFolders
        {
            UpdateResult = true,
            ReadResult = new TeamFile(Type, null, null, null, string.Empty),
            TeamResult = LibraryResult<LibraryPath>.Ok(new LibraryPath(new LibraryRoot("teams", "Teams", this.dir.Path, LibraryRootKind.Teams), Team, this.dir.Path, LibraryNodeRole.TeamFolder)),
        };
        this.Spend = new PersonaSpendStore(this.dir.Options());
        this.Feed = new RecordingFeedPublisher();
        this.Caps = new NeedsYouCaps(new NeedsYouCapStore(this.dir.Options()), this.Spend, this.Catalog, this.Feed);
        this.Store = new NeedsYouLevelStore(this.dir.Options());
        var options = this.dir.Options(configured => configured.NeedsYouLevel.Level = globalLevel ?? string.Empty);
        this.Levels = new NeedsYouLevels(this.Catalog, this.Folders, this.Store, options, caps: this.Caps);

        ctx.Services.AddSingleton<ITeamFolders>(this.Folders);
        ctx.Services.AddSingleton<ITeamCatalog>(this.Catalog);
        ctx.Services.AddSingleton(options);
        ctx.Services.AddSingleton(this.Store);
        ctx.Services.AddSingleton(this.Caps);
        ctx.Services.AddSingleton(this.Levels);
    }

    /// <summary>
    /// Registers the level services over a Team folders fake the test already built, for a test that renders a component which draws the control
    /// and does not care about levels (the Process tab's gate tests): the stores live under <paramref name="dir"/>.
    /// </summary>
    /// <param name="ctx">The bUnit context to register on, before anything is rendered.</param>
    /// <param name="folders">The folders the control reads the Team file from.</param>
    /// <param name="dir">The data folder the stores keep their database in.</param>
    public static void AddTo(MudBunitContext ctx, ITeamFolders folders, TempDataDir dir)
    {
        FakeTeamCatalog catalog = new();
        var options = dir.Options();
        NeedsYouLevelStore store = new(options);
        NeedsYouCaps caps = new(new NeedsYouCapStore(options), new PersonaSpendStore(options), catalog, new RecordingFeedPublisher());
        ctx.Services.TryAddSingleton(options);
        ctx.Services.AddSingleton(store);
        ctx.Services.AddSingleton(caps);
        ctx.Services.AddSingleton(new NeedsYouLevels(catalog, folders, store, options, caps: caps));
    }

    /// <summary>The Team catalog.</summary>
    public FakeTeamCatalog Catalog { get; }

    /// <summary>The in-memory Team and Project files.</summary>
    public FakeTeamFolders Folders { get; }

    /// <summary>The persisted spend the cap counts.</summary>
    public PersonaSpendStore Spend { get; }

    /// <summary>What the Feed was told.</summary>
    public RecordingFeedPublisher Feed { get; }

    /// <summary>The global level Settings saved.</summary>
    public NeedsYouLevelStore Store { get; }

    /// <summary>The spend cap's counting and question.</summary>
    public NeedsYouCaps Caps { get; }

    /// <summary>The levels the control reads and the Human sets.</summary>
    public NeedsYouLevels Levels { get; }

    /// <summary>Makes Dev run inside the Team with a cap of 5 USD and spends 5 USD, so the question is open.</summary>
    public void OpenTheTeamQuestion()
    {
        if (!this.Levels.SetTeam(Team, NeedsYouLevel.RunInsideTheTeam, 5m))
        {
            throw new InvalidOperationException("The Team file could not be written.");
        }

        this.Spend.Add("Nova", null, "USD", 5m);
        if (!this.Levels.For(Team, null).CapReached)
        {
            throw new InvalidOperationException("The cap was not reached.");
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();
}

using Agency.Huddle.App.Components.Settings.Helpers;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// <see cref="MemoryTidyCardSection"/>: the next and last pass line, the Tidy now button that waits while a pass runs, and the link to the
/// newest list (Memory Tidy spec §6.16).
/// </summary>
public sealed class MemoryTidyCardSectionTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly TempDataDir dir = new();
    private readonly ManualTimeProvider clock = new(Noon, TimeZoneInfo.Utc);
    private readonly FakeRunner runner = new();
    private IOptions<App.TeamOptions>? options;

    /// <summary>Removes the temp data directory.</summary>
    public void Dispose() => this.dir.Dispose();

    /// <summary>Registers what the section injects, over a temp data directory and a runner the test drives.</summary>
    /// <param name="ctx">The bUnit context.</param>
    private void Register(MudBunitContext ctx)
    {
        this.options = this.dir.Options();
        MemoryTidyBackups backups = new(this.options, this.clock);
        LibraryRootStore roots = new(this.options, new App.Acp.AgentPaths(this.options), NullLogger<LibraryRootStore>.Instance);
        ctx.Services.AddSingleton<IMemoryTidyRunner>(this.runner);
        ctx.Services.AddSingleton(new MemoryTidyLastPassReader(backups));
        ctx.Services.AddSingleton(new LibraryPathResolver(roots, this.options, NullLogger<LibraryPathResolver>.Instance));
        ctx.Services.AddSingleton<TimeProvider>(this.clock);
    }

    /// <summary>Writes a pass folder whose journal starts on 22 November, with a list file when asked.</summary>
    /// <param name="withList">Whether the pass wrote a <c>changes.md</c>.</param>
    private void SeedPass(bool withList)
    {
        MemoryTidyBackups backups = new(this.options ?? this.dir.Options(), this.clock);
        (string id, string folder) = backups.CreatePassFolder();
        new MemoryTidyJournal(folder).Append(new MemoryTidyEntry.PassStarted(id, new DateTimeOffset(2026, 11, 22, 3, 0, 0, TimeSpan.Zero), MemoryTidyStart.Timetable));
        if (withList)
        {
            File.WriteAllText(MemoryTidyList.PathIn(folder), "# Memory Tidy\n");
        }
    }

    /// <summary>While a pass runs, the button is disabled and reads A pass is running.</summary>
    [Fact]
    public async Task PassRunning_DisablesTidyNow_AndSaysSo()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        this.runner.Running = true;

        IRenderedComponent<MemoryTidyCardSection> cut = ctx.Render<MemoryTidyCardSection>();

        Assert.True(cut.Find(".memory-tidy-now").HasAttribute("disabled"));
        Assert.Equal("A pass is running", cut.Find(".memory-tidy-now").TextContent.Trim());
    }

    /// <summary>With no timetable registered the line has no Next pass, and with no pass folder it has no Last pass either.</summary>
    [Fact]
    public async Task NoTimetableAndNoPass_ShowsNoLine()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);

        IRenderedComponent<MemoryTidyCardSection> cut = ctx.Render<MemoryTidyCardSection>();

        Assert.Empty(cut.FindAll(".memory-tidy-line"));
        Assert.Empty(cut.FindAll(".memory-tidy-list"));
        Assert.Equal("Tidy now", cut.Find(".memory-tidy-now").TextContent.Trim());
    }

    /// <summary>Pressing Tidy now asks the runner for a pass of the Tidy now kind.</summary>
    [Fact]
    public async Task TidyNow_AsksTheRunnerForATidyNowPass()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        IRenderedComponent<MemoryTidyCardSection> cut = ctx.Render<MemoryTidyCardSection>();

        await cut.Find(".memory-tidy-now").ClickAsync(new());

        Assert.Equal([MemoryTidyStart.TidyNow], this.runner.Starts);
        Assert.Empty(cut.FindAll(".memory-tidy-refused"));
    }

    /// <summary>When the runner answers null at once, the section says A pass is running.</summary>
    [Fact]
    public async Task TidyNow_RefusedByTheRunner_SaysAPassIsRunning()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        this.runner.Refuse = true;
        IRenderedComponent<MemoryTidyCardSection> cut = ctx.Render<MemoryTidyCardSection>();

        await cut.Find(".memory-tidy-now").ClickAsync(new());

        Assert.Equal("A pass is running", cut.Find(".memory-tidy-refused").TextContent.Trim());
    }

    /// <summary>The last pass reads its local date and counts, and the list link opens the newest changes.md in the Library.</summary>
    [Fact]
    public async Task LastPass_ShowsDateAndCounts_AndLinksTheList()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        this.SeedPass(withList: true);

        IRenderedComponent<MemoryTidyCardSection> cut = ctx.Render<MemoryTidyCardSection>();

        Assert.Equal("Last pass: 22 Nov, tidied 0, dropped 0", cut.Find(".memory-tidy-line").TextContent.Trim());
        Assert.StartsWith("/library?root=", cut.Find(".memory-tidy-list").GetAttribute("href"), StringComparison.Ordinal); // contains-ok: an address prefix, not markup
        Assert.Equal("Open the last list", cut.Find(".memory-tidy-list").TextContent.Trim());
    }

    /// <summary>The runner's Changed event repaints the section, off the renderer's thread, and Dispose stops listening.</summary>
    [Fact]
    public async Task RunnerChanged_Repaints_AndDisposeUnsubscribes()
    {
        await using MudBunitContext ctx = new();
        this.Register(ctx);
        IRenderedComponent<MemoryTidyCardSection> cut = ctx.Render<MemoryTidyCardSection>();
        Assert.Equal("Tidy now", cut.Find(".memory-tidy-now").TextContent.Trim());

        this.runner.Running = true;
        await Task.Run(this.runner.RaiseChanged, Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(() => Assert.Equal("A pass is running", cut.Find(".memory-tidy-now").TextContent.Trim()));
        await ctx.DisposeComponentsAsync();
        Assert.Equal(0, this.runner.Subscribers);
    }

    /// <summary>A runner the test drives: whether a pass runs, and what it was asked.</summary>
    private sealed class FakeRunner : IMemoryTidyRunner
    {
        private Action? changed;

        /// <summary>What every <c>TryStart</c> was asked.</summary>
        public List<MemoryTidyStart> Starts { get; } = [];

        /// <summary>Whether a pass runs now.</summary>
        public bool Running { get; set; }

        /// <summary>Whether <c>TryStart</c> answers null.</summary>
        public bool Refuse { get; set; }

        /// <summary>How many handlers are subscribed to <see cref="Changed"/>.</summary>
        public int Subscribers => this.changed?.GetInvocationList().Length ?? 0;

        /// <inheritdoc />
        public event Action? Changed
        {
            add => this.changed += value;
            remove => this.changed -= value;
        }

        /// <inheritdoc />
        public bool IsRunning => this.Running;

        /// <summary>Raises <see cref="Changed"/>.</summary>
        public void RaiseChanged() => this.changed?.Invoke();

        /// <inheritdoc />
        public Task<MemoryTidyPassEnd>? TryStart(MemoryTidyStart start)
        {
            this.Starts.Add(start);
            return this.Refuse ? null : Task.FromResult(MemoryTidyPassEnd.Done);
        }
    }
}

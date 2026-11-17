using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Jobs;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Acp;
using Agency.Huddle.Tests.Factories;
using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Pins which fields <see cref="JobEditorDialog"/> shows for each Repeats choice, in particular that the day-of-month field belongs to Monthly only.</summary>
public sealed class JobEditorDialogTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly PersonaStore personas;
    private readonly JobStore store;
    private readonly JobStateStore states;
    private readonly IOptions<Agency.Huddle.App.TeamOptions> options;

    /// <summary>Seeds the Agent Nova and opens the stores over it.</summary>
    public JobEditorDialogTests()
    {
        this.options = this.dir.Options();
        TestPersonaFiles.Write(new AgentPaths(this.options), "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
        this.personas = this.options.CreatePersonaStore();
        this.store = new JobStore(this.personas, this.options, NullLogger<JobStore>.Instance);
        this.states = new JobStateStore(this.options);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.store.Dispose();
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>Editing a monthly Job shows the day-of-month field holding its day.</summary>
    [Fact]
    public async Task Monthly_ShowsTheDayOfMonthField()
    {
        Job job = this.Add(JobRecurrence.Monthly(31, new TimeOnly(9, 0)));
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, job.Id);

        IElement field = cut.Find(".job-editor-day-of-month input");
        Assert.Equal("31", field.GetAttribute("value"));
    }

    /// <summary>Editing a daily, weekly or hourly Job shows no day-of-month field.</summary>
    /// <param name="kind">Which timetable the Job has.</param>
    [Theory]
    [InlineData("daily")]
    [InlineData("weekly")]
    [InlineData("hours")]
    public async Task OtherCadences_DoNotShowTheDayOfMonthField(string kind)
    {
        JobRecurrence when = kind switch
        {
            "daily" => JobRecurrence.Daily(new TimeOnly(9, 0)),
            "weekly" => JobRecurrence.Weekly(JobWeekdays.Monday, new TimeOnly(9, 0)),
            _ => JobRecurrence.EveryHours(6),
        };
        Job job = this.Add(when);
        await using MudBunitContext ctx = this.NewContext();

        IRenderedComponent<ContainerFragment> cut = await OpenAsync(ctx, job.Id);

        Assert.NotEmpty(cut.FindAll(".job-editor-cadence"));
        Assert.Empty(cut.FindAll(".job-editor-day-of-month"));
    }

    private Job Add(JobRecurrence when) =>
        this.store.Add(new Job(string.Empty, JobOwner.Agent("Nova"), "Bills", "Check.", when, "UTC", "You", false));

    private MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.personas);
        ctx.Services.AddSingleton(this.store);
        ctx.Services.AddSingleton(new JobScheduler(this.store, this.states, new NoRunner(), this.options, NullLogger<JobScheduler>.Instance, new Agency.Huddle.Tests.Jobs.NoJobNotice()));
        ctx.Services.AddSingleton<IHumanIdentity>(new ConfiguredHumanIdentity("You"));
        ctx.Services.AddSingleton(TimeProvider.System);
        return ctx;
    }

    private static async Task<IRenderedComponent<ContainerFragment>> OpenAsync(MudBunitContext ctx, string jobId)
    {
        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder => { });
        IDialogService dialogs = ctx.Services.GetRequiredService<IDialogService>();
        DialogParameters<JobEditorDialog> parameters = new() { { dialog => dialog.JobId, jobId } };
        await cut.InvokeAsync(() => dialogs.ShowAsync<JobEditorDialog>("Edit job", parameters, JobEditorDialog.Options));
        return cut;
    }

    /// <summary>A runner that is never asked to run anything.</summary>
    private sealed class NoRunner : IJobRunner
    {
        /// <inheritdoc />
        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct) => Task.FromResult(JobOutcome.Delivered);
    }
}

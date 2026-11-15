using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.NeedsHuman;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// The Needs Human Helper is told when the Human set a Task's scope to Run inside the Team (ADR-0087), so it does not gate a local merge
/// that the level lets through; under Ask me it is told nothing.
/// </summary>
public sealed class NeedsHumanLevelNoteTests
{
    /// <summary>Builds the Helper over the rig's Task store, service and levels.</summary>
    /// <param name="rig">The rig.</param>
    private static NeedsHumanHelper HelperOver(NeedsYouLevelRig rig)
    {
        HelperSettingsResolver resolver = new(new HelperSettingsStore(rig.Harness.Options), rig.Harness.Options, NullLogger<HelperSettingsResolver>.Instance);
        NeedsHumanOverrideStore overrides = new(rig.Harness.Options, rig.Clock);
        return new NeedsHumanHelper(rig.Harness.Store, rig.Service, overrides, resolver, rig.Levels);
    }

    /// <summary>Input for a Task as the trigger captures it.</summary>
    /// <param name="task">The Task.</param>
    private static NeedsHumanInput InputFor(TaskItem task) => new(task.Id, "fp", task.Title, task.Description, task.Location.Team, []);

    /// <summary>A Task in a project that runs inside the Team gets the project note in its prompt values.</summary>
    [Fact]
    public void PromptValues_ProjectRunsInsideTheTeam_CarryTheProjectNote()
    {
        using NeedsYouLevelRig rig = new(projectLevel: NeedsYouLevel.RunInsideTheTeam);
        TaskItem task = rig.CreateInReview(NeedsYouLevelRig.Project);

        IReadOnlyDictionary<string, string> values = HelperOver(rig).PromptValues(InputFor(task));

        Assert.Equal(NeedsHumanHelper.LevelNoteFor(NeedsYouScope.Project), values["{{level}}"]);
        Assert.Contains("project to run inside the team", values["{{level}}"], StringComparison.Ordinal);
    }

    /// <summary>Under Ask me the note is empty, so the Task prompt is exactly the one the Helper always saw.</summary>
    [Fact]
    public void PromptValues_AskMe_CarryNoNote()
    {
        using NeedsYouLevelRig rig = new();
        TaskItem task = rig.CreateInReview(NeedsYouLevelRig.Project);

        IReadOnlyDictionary<string, string> values = HelperOver(rig).PromptValues(InputFor(task));

        Assert.Equal(string.Empty, values["{{level}}"]);
    }
}

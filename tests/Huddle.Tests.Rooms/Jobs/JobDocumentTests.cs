using Agency.Huddle.App.Jobs;

namespace Agency.Huddle.Tests.Jobs;

/// <summary>Pins how <see cref="JobDocument"/> reads and writes a <c>schedule.md</c>.</summary>
public sealed class JobDocumentTests
{
    private static readonly JobOwner Nova = JobOwner.Agent("Nova");

    private static readonly Job Digest = new(
        "job-7f3a2c",
        Nova,
        "Morning digest",
        "Summarise yesterday.\n\n- Rooms\n- Tasks",
        JobRecurrence.Weekdays(new TimeOnly(9, 0)),
        "Europe/Berlin",
        "Chief of Staff",
        Paused: false);

    /// <summary>A written Job is read back equal, multi-line instruction and list included.</summary>
    [Fact]
    public void WriteThenParse_RoundTripsAJob()
    {
        string text = JobDocument.Write([new JobSection(Digest, string.Empty, Digest.Title, null)]);

        JobSection section = Assert.Single(JobDocument.Parse(text, Nova));

        Assert.Equal(Digest, section.Job);
    }

    /// <summary>Huddle's own layout, exactly: the heading, the field lines, a blank line and the instruction.</summary>
    [Fact]
    public void Write_UsesTheDocumentedLayout()
    {
        string text = JobDocument.Write([new JobSection(Digest with { Paused = true }, string.Empty, Digest.Title, null)]);

        Assert.Equal(
            "# Schedule\n\n## Morning digest\n- id: job-7f3a2c\n- when: weekdays at 09:00\n- timezone: Europe/Berlin\n- by: Chief of Staff\n- paused: yes\n\nSummarise yesterday.\n\n- Rooms\n- Tasks\n",
            text);
    }

    /// <summary>A bad section is reported with its reason while the good one beside it still loads.</summary>
    [Fact]
    public void Parse_OneBadSection_DoesNotHideTheOthers()
    {
        string text = "# Schedule\n\n## Broken\n- id: job-1\n- when: fortnightly\n\nDo it.\n\n## Fine\n- id: job-2\n- when: daily at 08:00\n\nDo that.\n";

        IReadOnlyList<JobSection> sections = JobDocument.Parse(text, Nova);

        Assert.Equal(2, sections.Count);
        Assert.Null(sections[0].Job);
        Assert.Equal("Broken", sections[0].Title);
        Assert.StartsWith("'fortnightly' is not a time Huddle can read.", sections[0].Problem, StringComparison.Ordinal); // contains-ok: the rest is the grammar list JobRecurrenceTests pins whole.
        Assert.Equal("job-2", sections[1].Job?.Id);
    }

    /// <summary>A misspelt field refuses the section rather than being ignored, so a typo never leaves a Job running.</summary>
    [Fact]
    public void Parse_UnknownField_RefusesTheSection()
    {
        JobSection section = Assert.Single(JobDocument.Parse("## Digest\n- id: job-1\n- when: daily at 08:00\n- pasued: yes\n\nDo it.\n", Nova));

        Assert.Null(section.Job);
        Assert.Equal("'pasued' is not a Job field; use id, when, timezone, by, paused.", section.Problem);
    }

    /// <summary>Each missing part is named.</summary>
    /// <param name="text">A section missing one part.</param>
    /// <param name="expected">The reason.</param>
    [Theory]
    [InlineData("## Digest\n- when: daily at 08:00\n\nDo it.", "A Job needs an 'id' line.")]
    [InlineData("## \n- id: job-1\n- when: daily at 08:00\n\nDo it.", "A Job needs a title after '## '.")]
    [InlineData("## Digest\n- id: job-1\n- when: daily at 08:00\n", "A Job needs an instruction below its fields.")]
    [InlineData("## Digest\n- id: job-1\n- when: daily at 08:00\n- timezone: Mars/Olympus\n\nDo it.", "'Mars/Olympus' is not a time zone this machine knows.")]
    [InlineData("## Digest\n- id job-1\n\nDo it.", "'- id job-1' is not a 'key: value' line.")]
    public void Parse_MissingPart_IsNamed(string text, string expected)
    {
        JobSection section = Assert.Single(JobDocument.Parse(text, Nova));

        Assert.Equal(expected, section.Problem);
    }

    /// <summary>A rewrite keeps a section Huddle could not read exactly as the Human wrote it.</summary>
    [Fact]
    public void Write_KeepsAnUnreadSectionAsWritten()
    {
        IReadOnlyList<JobSection> sections = JobDocument.Parse("# Schedule\n\n## Broken\n- id: job-1\n- when: soon\n\nKeep me.\n", Nova);

        Assert.Equal("# Schedule\n\n## Broken\n- id: job-1\n- when: soon\n\nKeep me.\n", JobDocument.Write(sections));
    }

    /// <summary>The optional fields default to no zone, no author and not paused, and Windows line endings read the same.</summary>
    [Fact]
    public void Parse_OptionalFieldsAbsent_TakeTheirDefaults()
    {
        JobSection section = Assert.Single(JobDocument.Parse("## Digest\r\n- id: job-1\r\n- when: every 2 hours\r\n\r\nDo it.\r\n", JobOwner.Human));

        Assert.Equal(new Job("job-1", JobOwner.Human, "Digest", "Do it.", JobRecurrence.EveryHours(2), null, null, false), section.Job);
    }

    /// <summary>A <c>schedule.md</c> beside a definition is a Job file; the definition of an Agent called Schedule is not.</summary>
    [Fact]
    public void IsSidecar_TellsAJobFileFromAScheduleAgentsDefinition()
    {
        Assert.True(JobFiles.IsSidecar(Path.Combine("Agents", "Nova", "schedule.md")));
        Assert.False(JobFiles.IsSidecar(Path.Combine("Agents", "Schedule", "Schedule.md")));
        Assert.False(JobFiles.IsSidecar(Path.Combine("Agents", "Nova", "Nova.md")));
    }
}

using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests that checked Done bullets lose their checkbox like unchecked ones (Spec 6.2).</summary>
public sealed class ProcessProseCheckboxTests
{
    /// <summary>A <c>[x]</c>, <c>[X]</c> or <c>[ ]</c> prefix is dropped from the Done label; other brackets stay.</summary>
    /// <param name="bullet">The bullet text after the dash.</param>
    /// <param name="label">The expected label.</param>
    [Theory]
    [InlineData("[x] Tests pass", "Tests pass")]
    [InlineData("[X] Tests pass", "Tests pass")]
    [InlineData("[ ] Tests pass", "Tests pass")]
    [InlineData("[y] Tests pass", "[y] Tests pass")]
    public void DefinitionOfDone_CheckboxBullet_StripsOnlyCheckboxes(string bullet, string label)
    {
        Assert.True(ProcessDocument.TryParse("# Home\n## Definition of Done\n- " + bullet + "\n", out ProcessDocument? document));
        Assert.NotNull(document);

        Assert.Equal([label], document.DefinitionOfDone.Select(static item => item.Label).ToArray());
    }
}

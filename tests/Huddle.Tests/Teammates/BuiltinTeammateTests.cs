using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// Guards <see cref="BuiltinTeammate.DefaultText"/> - the embedded Chief of Staff Persona file -
/// against a drift that would break <see cref="BuiltinTeammateSeeder"/> silently: it must always
/// parse as a valid Persona identity and carry the marker and Skill Spec §6.12 requires.
/// </summary>
public sealed class BuiltinTeammateTests
{
    /// <summary>
    /// The embedded default parses cleanly and its identity carries the <c>_builtin: chief-of-staff</c>
    /// marker and the <c>team-building</c> Skill (Spec §6.12).
    /// </summary>
    [Fact]
    public void DefaultText_ParsesWithMarkerAndSkill()
    {
        var parsed = PersonaFrontmatter.TryReadIdentity(BuiltinTeammate.DefaultText, out PersonaIdentity? identity, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(identity);
        Assert.Equal(BuiltinTeammate.ChiefOfStaffMarker, identity.Builtin);
        Assert.Equal(["team-building"], identity.Skills);
    }

    /// <summary>RS §6.11 (RS-T13): the built-in Chief of Staff's own text teaches keeping one memory file per piece of work it coordinates, conditioned on having a memory folder at all (D31 correction 20).</summary>
    [Fact]
    public void ChiefOfStaff_TeachesMemoryFilePerProject()
    {
        Assert.Contains("memory folder", BuiltinTeammate.DefaultText, StringComparison.Ordinal);
        Assert.Contains("project-", BuiltinTeammate.DefaultText, StringComparison.Ordinal);
    }

    /// <summary>RS §6.11: the built-in Chief of Staff says plainly that another Room's own conversation is not visible to it unless reported.</summary>
    [Fact]
    public void ChiefOfStaff_SaysOtherRoomsAreNotVisible()
    {
        Assert.Contains("not visible", BuiltinTeammate.DefaultText, StringComparison.Ordinal);
    }
}

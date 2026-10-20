namespace Agency.Huddle.Tests.Live;

/// <summary>
/// The switch for the paid Helpers lane (Helpers spec §8, plan HL-4): the one place the real <c>AdapterProcessHelperRunner</c>
/// is driven. It has a variable of its own, <c>TEAM_E2E_HELPERS=1</c>, so setting <c>TEAM_E2E</c> does not start it and the
/// reverse holds too.
/// </summary>
internal static class LiveHelpers
{
    /// <summary>Whether the paid Helpers tests may run: <c>TEAM_E2E_HELPERS</c> is <c>1</c>.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("TEAM_E2E_HELPERS") == "1";
}

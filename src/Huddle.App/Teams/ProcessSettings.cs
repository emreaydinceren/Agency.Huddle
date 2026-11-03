using System.Globalization;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// The process settings kept in <c>_team.md</c> (Spec §7.2): gates, work-in-progress limit, the retro and digest cadences,
/// the check command and the template id.
/// </summary>
/// <param name="Gates">The enabled gates, without duplicates.</param>
/// <param name="Wip">The work-in-progress limit, or <see langword="null"/>.</param>
/// <param name="Retro">The raw inline-map text of the retro cadence, or <see langword="null"/>.</param>
/// <param name="Digest">The raw inline-map text of the digest cadence, or <see langword="null"/>.</param>
/// <param name="CheckCommand">The command a checks gate waits for, or <see langword="null"/>.</param>
/// <param name="Template">The process template id, or <see langword="null"/>.</param>
internal sealed record ProcessSettings(
    IReadOnlyList<GateId> Gates,
    int? Wip,
    string? Retro,
    string? Digest,
    string? CheckCommand,
    string? Template)
{
    /// <summary>Settings with nothing set.</summary>
    public static ProcessSettings Empty { get; } = new([], null, null, null, null, null);

    /// <summary>Reads the settings from frontmatter values, sharing the gate and wip reading with <see cref="ProcessDocument"/>.</summary>
    /// <param name="values">The frontmatter values by key, already unquoted.</param>
    /// <returns>The settings; unknown gate ids are dropped.</returns>
    public static ProcessSettings Parse(Dictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ProcessSettings(
            ProcessDocument.ReadGates(values).Gates,
            ProcessDocument.ReadWip(values),
            Value(values, "retro"),
            Value(values, "digest"),
            Value(values, "checkCommand"),
            Value(values, "template"));
    }

    /// <summary>The retro cadence read from <see cref="Retro"/>.</summary>
    /// <returns>The cadence, or <see langword="null"/> when it is absent or unreadable.</returns>
    public RetroCadence? RetroSchedule() =>
        ReadCadence(this.Retro, out int every, out CadenceUnit unit) ? new RetroCadence(every, unit) : null;

    /// <summary>The digest cadence read from <see cref="Digest"/>.</summary>
    /// <returns>The cadence, or <see langword="null"/> when it is absent or unreadable.</returns>
    public DigestCadence? DigestSchedule() =>
        ReadCadence(this.Digest, out int every, out CadenceUnit unit) ? new DigestCadence(every, unit) : null;

    private static bool ReadCadence(string? raw, out int every, out CadenceUnit unit)
    {
        every = 0;
        unit = default;
        if (raw is null || !raw.StartsWith('{') || !raw.EndsWith('}'))
        {
            return false;
        }

        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in raw[1..^1].Split(','))
        {
            int colon = pair.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                fields[pair[..colon].Trim()] = ProcessDocument.Unquote(pair[(colon + 1)..]);
            }
        }

        if (!fields.TryGetValue("every", out string? everyText)
            || !int.TryParse(everyText, NumberStyles.None, CultureInfo.InvariantCulture, out every)
            || every <= 0
            || !fields.TryGetValue("unit", out string? unitText))
        {
            return false;
        }

        switch (unitText)
        {
            case "done-tasks":
                unit = CadenceUnit.DoneTasks;
                return true;
            case "days":
                unit = CadenceUnit.Days;
                return true;
            default:
                return false;
        }
    }

    private static string? Value(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}

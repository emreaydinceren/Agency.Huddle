namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// Every user-facing string of the Human gate on the Task panel, in one place (UI copy corrections
/// rows 1-8 and 28). The Task panel components of the gate share this class.
/// </summary>
internal static class HumanGateCopy
{
    /// <summary>The section chip.</summary>
    public const string Heading = "Human gate";

    /// <summary>The line beside the section chip.</summary>
    public const string Subheading = "You decide whether this is done";

    /// <summary>The label of the gate switch.</summary>
    public const string SwitchLabel = "Requires your acceptance";

    /// <summary>The locked tooltip, also the lock icon's aria-label.</summary>
    public const string LockedTooltip = "This Task names an Action that leaves the team.";

    /// <summary>The label of the Action field.</summary>
    public const string ActionLabel = "What will happen";

    /// <summary>The placeholder of the Action field.</summary>
    public const string ActionPlaceholder = "Nothing leaves the team";

    /// <summary>The label of the Done rule chip row.</summary>
    public const string DoneRuleLabel = "Can move to Done";

    /// <summary>What the assignee reads as when the Task has none.</summary>
    public const string UnassignedAssignee = "the assignee";

    /// <summary>The explanation of a gated Task that names no Action.</summary>
    public const string ExplanationNoAction =
        "Teammates draft, check and track: you decide. Accepting marks it Done; nothing is sent or published. Done also needs the team's Proof to be complete.";

    /// <summary>The helper line under the switch.</summary>
    /// <param name="assignee">The assignee's name, or <see cref="UnassignedAssignee"/>.</param>
    public static string Helper(string assignee) =>
        $"{assignee} can move it to Needs you once every line has Proof. Teammates can't mark it Done.";

    /// <summary>The text of the Done rule chip.</summary>
    /// <param name="human">The Human's display name.</param>
    public static string DoneRuleChip(string human) => $"{human} only";

    /// <summary>The Proof heading of an ordinary Task (row 9).</summary>
    public const string ProofHeadingUngated = "Definition of Done (for information)";

    /// <summary>The Proof heading of a gated Task (row 10).</summary>
    public const string ProofHeadingGated = "Proof";

    /// <summary>The text of the Ask button (row 11).</summary>
    /// <param name="owner">The owner the Ask goes to.</param>
    public static string AskButton(string? owner) => $"Ask {owner}";

    /// <summary>The Ask button's accessible name (row 11).</summary>
    /// <param name="owner">The owner the Ask goes to.</param>
    /// <param name="label">The Proof item's label.</param>
    public static string AskAccessibleName(string? owner, string label) => $"Ask {owner} for {label}";

    /// <summary>The Message an Ask posts (row 12).</summary>
    /// <param name="owner">The owner the Ask goes to.</param>
    /// <param name="taskId">The Task's id.</param>
    /// <param name="label">The Proof item's label.</param>
    public static string AskMessage(string owner, string taskId, string label) =>
        $"@{owner}, {taskId} needs {label} before it can come to me.";

    /// <summary>The snackbar after an Ask that posted (row 13).</summary>
    /// <param name="owner">The owner asked.</param>
    /// <param name="room">The Room's display name.</param>
    public static string AskedSnackbar(string owner, string room) => $"Asked {owner} in {room}.";

    /// <summary>The snackbar after an Ask that could not be posted (row 13).</summary>
    /// <param name="owner">The owner who could not be reached.</param>
    public static string AskFailedSnackbar(string owner) => $"Couldn't reach {owner}.";

    /// <summary>The explanation of a gated Task that names an Action.</summary>
    /// <param name="assignee">The assignee's name, or <see cref="UnassignedAssignee"/>.</param>
    /// <param name="action">The Action text.</param>
    public static string Explanation(string assignee, string action) =>
        $"Teammates draft, check and track: you decide. Accepting lets {assignee} {action}. Done also needs the team's Proof to be complete.";
}

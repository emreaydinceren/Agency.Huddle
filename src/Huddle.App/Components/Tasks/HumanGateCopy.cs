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

    /// <summary>The decision card chip (row 14).</summary>
    public const string DecisionChip = "Your decision";

    /// <summary>The card's first line when every Proof item is met (row 15).</summary>
    public const string AllProofIn = "All Proof is in.";

    /// <summary>The Request changes button (row 19).</summary>
    public const string RequestChangesButton = "Request changes";

    /// <summary>The Accept button (row 19).</summary>
    public const string AcceptButton = "Accept";

    /// <summary>The Decline menu item (row 19).</summary>
    public const string DeclineButton = "Decline";

    /// <summary>The overflow menu's aria-label (row 20).</summary>
    public const string MoreDecisions = "More decisions";

    /// <summary>The notice that blocks a decision while the panel holds unsaved edits (row 24).</summary>
    public const string UnsavedEdits = "Save your changes before deciding.";

    /// <summary>The notice for an acceptance the Task has changed since (row 21).</summary>
    public const string StaleNotice = "Changed since you accepted it.";

    /// <summary>The message for a version conflict on a decision (row 22).</summary>
    public const string VersionConflict = "This Task changed while you were reading it.";

    /// <summary>The reason dialog's field label (row 26).</summary>
    public const string ReasonLabel = "Reason";

    /// <summary>The card's first line when Proof is incomplete (row 15).</summary>
    /// <param name="met">How many items are met.</param>
    /// <param name="total">How many items there are.</param>
    public static string ProofProgress(int met, int total) => $"{met} of {total} Proof items are in.";

    /// <summary>The Accept sentence (row 16): the second sentence of the explanation.</summary>
    /// <param name="assignee">The assignee's name.</param>
    /// <param name="action">The Action, or <see langword="null"/>.</param>
    public static string AcceptSentence(string assignee, string? action) => string.IsNullOrWhiteSpace(action)
        ? "Accepting marks it Done; nothing is sent or published."
        : $"Accepting lets {assignee} {WithoutOwnName(assignee, action.Trim())}.";

    /// <summary>An Action an Agent wrote as "Quinn: run the tests" loses that name, because the sentence already says who.</summary>
    /// <param name="assignee">The assignee's name.</param>
    /// <param name="action">The trimmed Action.</param>
    private static string WithoutOwnName(string assignee, string action) =>
        action.StartsWith(assignee + ":", StringComparison.OrdinalIgnoreCase) ? action[(assignee.Length + 1)..].TrimStart() : action;

    /// <summary>The locked notice (row 17); a missing owner or detail drops its comma, and with both missing the parentheses go.</summary>
    /// <param name="met">How many items are met.</param>
    /// <param name="total">How many items there are.</param>
    /// <param name="label">The open item's label.</param>
    /// <param name="owner">The open item's owner, or <see langword="null"/>.</param>
    /// <param name="detail">The open item's detail, or <see langword="null"/>.</param>
    public static string LockedNotice(int met, int total, string label, string? owner, string? detail)
    {
        string who = string.Join(", ", new[] { owner, detail }.Where(static part => !string.IsNullOrWhiteSpace(part)));
        string open = who.Length == 0 ? string.Empty : $" ({who})";
        return $"Proof {met}/{total} — {label} still open{open}. Accept unlocks when it's in.";
    }

    /// <summary>The line for a valid acceptance whose Action is pending (row 23).</summary>
    /// <param name="human">Who accepted.</param>
    /// <param name="assignee">The assignee's name.</param>
    /// <param name="action">The Action.</param>
    public static string AcceptedWithAction(string human, string assignee, string action) => $"Accepted by {human}. {assignee} can now {action}.";

    /// <summary>The trail (row 25).</summary>
    /// <param name="assignee">The assignee's name.</param>
    /// <param name="checkers">The Evidence authors other than the assignee.</param>
    /// <param name="id">The Task id.</param>
    public static string Trail(string assignee, IReadOnlyList<string> checkers, string id)
    {
        ArgumentNullException.ThrowIfNull(checkers);
        string checkedBy = checkers.Count == 0 ? string.Empty : $" · Checked by {string.Join(", ", checkers)}";
        return $"Drafted by {assignee}{checkedBy} · Tracked as {id}";
    }

    /// <summary>The Request changes dialog title (row 26).</summary>
    /// <param name="id">The Task id.</param>
    public static string RequestChangesTitle(string id) => $"Request changes on {id}";

    /// <summary>The Decline dialog title (row 26).</summary>
    /// <param name="id">The Task id.</param>
    public static string DeclineTitle(string id) => $"Decline {id}";

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

    // --- Needs You on screen (P12, UI copy rows 35-38) ---

    /// <summary>The Needs You page and Tab title (row 37).</summary>
    public const string NeedsYouTitle = "Needs you";

    /// <summary>The title of the Needs You page's empty state (row 38).</summary>
    public const string NeedsYouEmptyTitle = "Nothing needs you";

    /// <summary>The sentence of the Needs You page's empty state (row 38).</summary>
    public const string NeedsYouEmptyText = "Tasks waiting for your decision and Rooms waiting on you appear here.";

    /// <summary>The second line of a Task row in the Needs You card (row 35).</summary>
    /// <param name="met">Proof items met.</param>
    /// <param name="total">Proof items in all.</param>
    public static string NeedsYouTaskDetail(int met, int total) => $"Waiting on you · Proof {met}/{total}";

    /// <summary>The Board chip of a gated card in Needs You that is not accepted (row 29).</summary>
    public const string BoardChipNeedsYou = "Needs you";

    /// <summary>The Board chip of a gated card whose acceptance is valid (row 29).</summary>
    public const string BoardChipAccepted = "Accepted";

    /// <summary>The accessible name of the lock icon on a gated card in any other status (row 29).</summary>
    public const string LockAriaLabel = "Human gate";

    /// <summary>The cancel button of the decision dialog (row 33).</summary>
    public const string CancelButton = "Cancel";

    /// <summary>The refusal for a drop into Done before Needs You (row 30).</summary>
    /// <param name="id">The Task's id.</param>
    /// <param name="met">The Proof items met.</param>
    /// <param name="total">The Proof items in all.</param>
    public static string GoesToNeedsYouFirst(string id, int met, int total) => $"{id} goes to Needs you first (Proof {met}/{total}).";

    /// <summary>The title of the decision dialog (row 32).</summary>
    /// <param name="id">The Task's id.</param>
    public static string DecisionDialogTitle(string id) => $"Your decision on {id}";

    /// <summary>The refusal for a drop into Done when the acceptance is already valid (row 34).</summary>
    /// <param name="id">The Task's id.</param>
    /// <param name="assignee">The assignee's name.</param>
    public static string AlreadyAccepted(string id, string assignee) => $"{id} is accepted: {assignee} moves it to Done after the Action.";
}

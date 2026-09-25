namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// Which side of a conflicting field the Human picked when resolving a <c>TaskResult.Conflict</c>
/// (Spec §13.7). Public (corrections-B5 D14 item 1): bound as a <c>MudRadioGroup&lt;ConflictChoice&gt;</c>'s
/// <c>[Parameter]</c> value in <c>TaskDetail</c>.
/// </summary>
public enum ConflictChoice
{
    /// <summary>Keep the Human's own pending edit for this field.</summary>
    Mine,

    /// <summary>Drop the Human's pending edit for this field, keeping the server's current value.</summary>
    Theirs,
}

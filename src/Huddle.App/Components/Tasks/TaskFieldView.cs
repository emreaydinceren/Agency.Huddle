using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Agency.Huddle.App.Components.Tasks;

/// <summary>
/// The one renderer the Board card and the List share for empty values, tags and the origin Room
/// (UX-047, UX-R07), so the two views word and draw them the same way.
/// </summary>
internal static class TaskFieldView
{
    /// <summary><see cref="Variant.Text"/> boxed once: <c>AddAttribute</c> takes an <see cref="object"/>, so passing the enum would box it again for every chip on every render.</summary>
#pragma warning disable HAA0601 // Boxed once into a static field on purpose: that is the fix for boxing on every render.
    private static readonly object TextVariant = Variant.Text;

    /// <summary><see cref="Size.Small"/> boxed once, for the same reason as <see cref="TextVariant"/>.</summary>
    private static readonly object SmallSize = Size.Small;
#pragma warning restore HAA0601

    /// <summary>The words for an empty value of <paramref name="field"/>, or <see langword="null"/> when it stays blank.</summary>
    /// <param name="field">A field key.</param>
    public static string? EmptyText(string field) => field switch
    {
        "assignee" => "Unassigned",
        "project" => "No project",
        _ => null,
    };

    /// <summary>The display name of a field key (design-system "Interface copy": show a display name, never a slug): <c>due_date</c> reads "Due date".</summary>
    /// <param name="field">A field key such as <c>due_date</c>.</param>
    public static string Label(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        string spaced = field.Replace('_', ' ');
        return string.Concat(char.ToUpperInvariant(spaced[0]).ToString(), spaced.AsSpan(1));
    }

    /// <summary>The origin Room's name, or <see langword="null"/> when there is no origin or the Room is unknown.</summary>
    /// <param name="originRoomId">The Task's origin Room id.</param>
    /// <param name="roomNames">Room names by id.</param>
    public static string? OriginText(string? originRoomId, IReadOnlyDictionary<string, string>? roomNames) =>
        originRoomId is not null && roomNames is not null && roomNames.TryGetValue(originRoomId, out string? name) ? name : null;

    /// <summary>One Small tag chip (the quiet <c>tag-chip</c> look) per tag, with a <c>task-tag-hue-N</c> class that colours its dot.</summary>
    /// <param name="tags">The tags.</param>
    public static RenderFragment Tags(IReadOnlyList<string> tags) => builder =>
    {
        foreach (string tag in tags)
        {
            builder.OpenComponent<MudChip<string>>(0);
            builder.AddAttribute(1, nameof(MudChip<string>.Variant), TextVariant);
            builder.AddAttribute(2, nameof(MudChip<string>.Size), SmallSize);
            builder.AddAttribute(3, nameof(MudChip<string>.Class), TagClasses[TagHue(tag)]);
            builder.AddAttribute(4, nameof(MudChip<string>.ChildContent), (RenderFragment)(content => content.AddContent(0, tag)));
            builder.CloseComponent();
        }
    };

    /// <summary>
    /// A tag chip's classes, one entry per dot colour; <c>app.css</c> has one <c>.task-tag-hue-N</c> rule for each. Spelled
    /// out rather than formatted, so the dead-selector guard finds every class in the source.
    /// </summary>
    internal static readonly string[] TagClasses =
    [
        "task-tag tag-chip task-tag-hue-0",
        "task-tag tag-chip task-tag-hue-1",
        "task-tag tag-chip task-tag-hue-2",
        "task-tag tag-chip task-tag-hue-3",
        "task-tag tag-chip task-tag-hue-4",
        "task-tag tag-chip task-tag-hue-5",
    ];

    /// <summary>
    /// Which of the <see cref="TagClasses"/> a tag wears: an FNV-1a hash of its text, so the same tag has the same dot on
    /// every card and in every process (<see cref="string.GetHashCode()"/> is randomised per process).
    /// </summary>
    /// <param name="tag">The tag's text.</param>
    /// <returns>An index into <see cref="TagClasses"/>.</returns>
    internal static int TagHue(string tag)
    {
        uint hash = 2166136261;
        foreach (char letter in tag)
        {
            hash = unchecked((hash ^ letter) * 16777619);
        }

        return (int)(hash % (uint)TagClasses.Length);
    }
}

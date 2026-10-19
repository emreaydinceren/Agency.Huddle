namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Distinguishes "leave this field alone" (<see langword="default"/>, <see cref="IsSet"/> false)
/// from "set this field to a value, possibly null" (<see cref="Set(T)"/>) - Spec §9.1. A plain
/// nullable can't carry that distinction for a field whose "clear" value is itself
/// <see langword="null"/>, such as <see cref="TaskPatch.Assignee"/>.
/// </summary>
/// <typeparam name="T">The type of value carried when set.</typeparam>
/// <param name="IsSet">Whether this field is being changed at all.</param>
/// <param name="Value">The new value, meaningful only when <paramref name="IsSet"/> is <see langword="true"/>.</param>
internal readonly record struct Optional<T>(bool IsSet, T Value)
{
    /// <summary>Creates a set <see cref="Optional{T}"/> carrying <paramref name="value"/>.</summary>
    /// <param name="value">The value to set the field to.</param>
    public static Optional<T> Set(T value) => new(true, value);
}

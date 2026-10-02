namespace Agency.Huddle.Acp.Abstractions;

/// <summary>Base type for how an <see cref="ElicitationRequest"/> ended: answered, declined or cancelled.</summary>
public abstract record ElicitationResult;

/// <summary>The Human filled the form in and sent it.</summary>
/// <param name="Content">
/// The answers keyed by form field name. Every value must be a plain CLR value the wire serialises
/// as the matching JSON type: <see cref="string"/>, <see cref="long"/>, <see cref="double"/>,
/// <see cref="bool"/> or a <c>string[]</c>. Never a <c>System.Text.Json</c> node: dotacp serialises
/// with Newtonsoft, which cannot write one.
/// </param>
public sealed record ElicitationAccepted(IReadOnlyDictionary<string, object> Content) : ElicitationResult;

/// <summary>The Human saw the form and chose not to answer it (Skip, Dismiss).</summary>
public sealed record ElicitationDeclined : ElicitationResult;

/// <summary>The request ended without the Human answering it: a Stop, a Turn end, a timeout, a disconnect.</summary>
public sealed record ElicitationCancelled : ElicitationResult;

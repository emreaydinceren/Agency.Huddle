namespace Agency.Huddle.App.Teams;

/// <summary>The documents of one <c>outputs/</c> folder the cap left out: the "N more in &lt;folder&gt;" line (Spec §6.3).</summary>
/// <param name="Folder">The folder's path under the Teams root, with <c>/</c> separators, such as <c>B/outputs</c>.</param>
/// <param name="Count">How many documents of the folder are not listed; always above zero.</param>
internal sealed record OutputsOverflow(string Folder, int Count);

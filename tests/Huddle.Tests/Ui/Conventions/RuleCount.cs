namespace Agency.Huddle.Tests.Ui.Conventions;

/// <summary>
/// What one convention rule found in one file: the rule id (<c>CV1</c> to <c>CV11</c>), the
/// repository-relative path with forward slashes, and the number of violations.
/// </summary>
/// <param name="Rule">The rule id, for example <c>CV3</c>.</param>
/// <param name="Path">Repository-relative path, forward slashes.</param>
/// <param name="Count">How many violations of the rule the file holds.</param>
internal sealed record RuleCount(string Rule, string Path, int Count);

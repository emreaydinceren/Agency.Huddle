namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>One checked request of a Verdict. Each case knows the files it reads; the action word is read once, here, into a closed set.</summary>
/// <param name="Reason">Why, in a few words, cut to <see cref="MaxReasonLength"/> characters.</param>
internal abstract record MemoryTidyStep(string Reason)
{
    /// <summary>The longest reason kept, as for a Task Reason.</summary>
    internal const int MaxReasonLength = 200;

    /// <summary>The files the step reads, the kept file first.</summary>
    internal abstract IReadOnlyList<string> Sources { get; }

    /// <summary>The step an action asks for, or the problem with its shape.</summary>
    /// <param name="action">The action as the model wrote it.</param>
    /// <param name="problem">Why the action is the wrong shape, or <see langword="null"/>.</param>
    /// <returns>The step, or <see langword="null"/> when <paramref name="problem"/> is set.</returns>
    internal static MemoryTidyStep? TryParse(MemoryTidyAction action, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(action);

        string word = (action.Action ?? string.Empty).Trim().ToLowerInvariant();
        problem = null;
        MemoryTidyStep? step = word switch
        {
            "merge" => ParseMerge(action, out problem),
            "rewrite" => ParseRewrite(action, out problem),
            "describe" => ParseDescribe(action, out problem),
            "drop" => ParseDrop(action, out problem),
            _ => null,
        };
        if (step is null && problem is null)
        {
            problem = "an action must be merge, rewrite, describe or drop";
        }

        return step;
    }

    /// <summary>Cuts a reason to <see cref="MaxReasonLength"/>: 197 characters and an ellipsis.</summary>
    /// <param name="reason">The reason as written.</param>
    internal static string CutReason(string reason)
    {
        string trimmed = reason.Trim();
        return trimmed.Length <= MaxReasonLength ? trimmed : string.Concat(trimmed.AsSpan(0, MaxReasonLength - 3), "...");
    }

    private static Merge? ParseMerge(MemoryTidyAction action, out string? problem)
    {
        problem = null;
        if (Blank(action.Into) || action.From is null || action.From.Any(Blank) || Blank(action.Reason) || action.Text is null)
        {
            problem = "a merge needs into, from, text and a reason";
            return null;
        }

        if (Blank(action.Text))
        {
            problem = "a merge needs a text that is not empty";
            return null;
        }

        return new Merge(action.Into!.Trim(), [.. action.From.Select(static name => name.Trim())], Optional(action.Description), action.Text, CutReason(action.Reason!));
    }

    private static Rewrite? ParseRewrite(MemoryTidyAction action, out string? problem)
    {
        problem = null;
        if (Blank(action.File) || Blank(action.Reason) || action.Text is null)
        {
            problem = "a rewrite needs file, text and a reason";
            return null;
        }

        if (Blank(action.Text))
        {
            problem = "a rewrite needs a text that is not empty";
            return null;
        }

        return new Rewrite(action.File!.Trim(), Optional(action.Description), action.Text, CutReason(action.Reason!));
    }

    private static Describe? ParseDescribe(MemoryTidyAction action, out string? problem)
    {
        problem = null;
        if (Blank(action.File) || Blank(action.Description) || Blank(action.Reason))
        {
            problem = "a describe needs file, description and a reason";
            return null;
        }

        return new Describe(action.File!.Trim(), action.Description!, CutReason(action.Reason!));
    }

    private static Drop? ParseDrop(MemoryTidyAction action, out string? problem)
    {
        problem = null;
        if (Blank(action.File) || Blank(action.Reason))
        {
            problem = "a drop needs file and a reason";
            return null;
        }

        return new Drop(action.File!.Trim(), CutReason(action.Reason!));
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static string? Optional(string? value) => Blank(value) ? null : value;

    /// <summary>Folds two or more files into one: the kept file's whole new body replaces its own.</summary>
    /// <param name="Into">The file kept.</param>
    /// <param name="From">The files folded in and removed.</param>
    /// <param name="Description">The new description, or <see langword="null"/> to keep the file's own.</param>
    /// <param name="Text">The whole new body.</param>
    /// <param name="Reason">Why.</param>
    internal sealed record Merge(string Into, IReadOnlyList<string> From, string? Description, string Text, string Reason) : MemoryTidyStep(Reason)
    {
        /// <inheritdoc/>
        internal override IReadOnlyList<string> Sources => [this.Into, .. this.From];
    }

    /// <summary>Replaces one file's body.</summary>
    /// <param name="File">The file.</param>
    /// <param name="Description">The new description, or <see langword="null"/> to keep the file's own.</param>
    /// <param name="Text">The whole new body.</param>
    /// <param name="Reason">Why.</param>
    internal sealed record Rewrite(string File, string? Description, string Text, string Reason) : MemoryTidyStep(Reason)
    {
        /// <inheritdoc/>
        internal override IReadOnlyList<string> Sources => [this.File];
    }

    /// <summary>Gives one file a new description.</summary>
    /// <param name="File">The file.</param>
    /// <param name="Description">The new description.</param>
    /// <param name="Reason">Why.</param>
    internal sealed record Describe(string File, string Description, string Reason) : MemoryTidyStep(Reason)
    {
        /// <inheritdoc/>
        internal override IReadOnlyList<string> Sources => [this.File];
    }

    /// <summary>Removes one file.</summary>
    /// <param name="File">The file.</param>
    /// <param name="Reason">Why.</param>
    internal sealed record Drop(string File, string Reason) : MemoryTidyStep(Reason)
    {
        /// <inheritdoc/>
        internal override IReadOnlyList<string> Sources => [this.File];
    }
}

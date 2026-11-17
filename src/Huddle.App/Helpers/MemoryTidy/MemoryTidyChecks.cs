using System.Text;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>
/// Checks A-1 to A-7 of Memory Tidy spec §6.10, as pure functions over a step, the input it was made for and how many drops the Verdict has
/// already counted. They read the step's raw <c>text</c> and <c>description</c>, before any frontmatter echo is stripped, so a key smuggled in a
/// <c>---</c> block is checked like any other text. A-8 to A-10 belong to the writer.
/// </summary>
internal static class MemoryTidyChecks
{
    private const string LockedRefusal = "descriptions do not change in this folder";
    private const string KeptOwnDescription = "it kept its own description";

    /// <summary>Runs A-1 to A-7 in order and stops at the first refusal.</summary>
    /// <param name="step">The step.</param>
    /// <param name="input">The input the Verdict was made for.</param>
    /// <param name="dropsCounted">How many drops the Verdict's earlier steps have counted: explicit drops and heavy rewrites.</param>
    /// <returns>The refusal, or what the writer needs to apply the step.</returns>
    internal static MemoryTidyCheck Run(MemoryTidyStep step, MemoryTidyInput input, int dropsCounted)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(input);

        if (Shown(step, input, out List<MemoryTidyFile> files) is { } notShown)
        {
            return MemoryTidyCheck.Refused(notShown);
        }

        string? refusal = SameFiles(step) ?? Size(step, files) ?? NothingNew(step, files) ?? NothingLost(step, files);
        if (refusal is not null)
        {
            return MemoryTidyCheck.Refused(refusal, files);
        }

        bool heavy = IsHeavy(step, files);
        if (heavy && dropsCounted >= input.Limits.MaxDrops)
        {
            return MemoryTidyCheck.Refused($"it removes most of {files[0].File}, and this pass may drop no more files", files);
        }

        return Descriptions(step, files, input.Limits.DescriptionsLocked, heavy);
    }

    /// <summary>The body of a memory file: everything after its frontmatter, as the writer reads it.</summary>
    /// <param name="text">The file's whole text.</param>
    internal static string BodyOf(string text)
    {
        NoteFrontmatterBlock block = NoteFrontmatter.Locate(text);
        return (block.Text is null ? text : text[block.BodyStart..]).TrimStart('\r', '\n');
    }

    /// <summary>A-1: every file the step names is one shown in full, matched ignoring case.</summary>
    private static string? Shown(MemoryTidyStep step, MemoryTidyInput input, out List<MemoryTidyFile> files)
    {
        files = [];
        foreach (string name in step.Sources)
        {
            MemoryTidyFile? found = input.Files.FirstOrDefault(file => string.Equals(file.File, name, StringComparison.OrdinalIgnoreCase));
            if (found is null)
            {
                return $"it names {name}, which it was not shown";
            }

            files.Add(found);
        }

        return null;
    }

    /// <summary>A-2: a merge folds in at least one file, and not the one it keeps.</summary>
    private static string? SameFiles(MemoryTidyStep step)
    {
        if (step is not MemoryTidyStep.Merge merge)
        {
            return null;
        }

        bool intoIsFrom = merge.From.Any(name => string.Equals(name, merge.Into, StringComparison.OrdinalIgnoreCase));
        return merge.From.Count == 0 || intoIsFrom ? "a merge needs different files" : null;
    }

    /// <summary>A-3: a rewrite is no longer than the body it replaces; a merge is no longer than all its bodies together and no shorter than the largest.</summary>
    private static string? Size(MemoryTidyStep step, List<MemoryTidyFile> files)
    {
        switch (step)
        {
            case MemoryTidyStep.Rewrite rewrite:
                return Bytes(rewrite.Text) > Bytes(BodyOf(files[0].Text)) ? "the new text is longer than what it replaces" : null;
            case MemoryTidyStep.Merge merge:
                long[] old = [.. files.Select(static file => Bytes(BodyOf(file.Text)))];
                long length = Bytes(merge.Text);
                if (length > old.Sum())
                {
                    return "the new text is longer than what it replaces";
                }

                return length < old.Max() ? "the merged text is shorter than one of the files it merges" : null;
            default:
                return null;
        }
    }

    /// <summary>A-4: every checked token of the new text and the new description is in a body or a description of the files the step reads.</summary>
    private static string? NothingNew(MemoryTidyStep step, List<MemoryTidyFile> files)
    {
        List<string> sources = [];
        foreach (MemoryTidyFile file in files)
        {
            sources.Add(BodyOf(file.Text));
            if (MemoryFileRules.DescriptionOf(file.Text) is { } description)
            {
                sources.Add(description);
            }
        }

        foreach (string? written in new[] { NewText(step), NewDescription(step) })
        {
            if (written is not null && MemoryTidyTokens.NotIn(written, sources) is [{ } token, ..])
            {
                return $"the new text holds {token.Text}, which is not in the files it replaces";
            }
        }

        return null;
    }

    /// <summary>A-5: a merge keeps every checked token of every body it reads.</summary>
    private static string? NothingLost(MemoryTidyStep step, List<MemoryTidyFile> files)
    {
        if (step is not MemoryTidyStep.Merge merge)
        {
            return null;
        }

        foreach (MemoryTidyFile file in files)
        {
            if (MemoryTidyTokens.NotIn(BodyOf(file.Text), merge.Text) is [{ } token, ..])
            {
                return $"the merged text leaves out {token.Text} from {file.File}";
            }
        }

        return null;
    }

    /// <summary>A-6: a drop, or a rewrite whose new body is less than half the old body's bytes.</summary>
    private static bool IsHeavy(MemoryTidyStep step, List<MemoryTidyFile> files) => step switch
    {
        MemoryTidyStep.Drop => true,
        MemoryTidyStep.Rewrite rewrite => Bytes(rewrite.Text) * 2 < Bytes(BodyOf(files[0].Text)),
        _ => false,
    };

    /// <summary>A-7: a new description is written only where the kept file has none that works, and never where descriptions are locked.</summary>
    private static MemoryTidyCheck Descriptions(MemoryTidyStep step, List<MemoryTidyFile> files, bool locked, bool heavy)
    {
        string? given = NewDescription(step);
        if (step is MemoryTidyStep.Drop)
        {
            return new MemoryTidyCheck(null, files, heavy, null, null);
        }

        MemoryTidyFile kept = files[0];
        bool ownWorks = MemoryFileRules.DescriptionProblem(MemoryFileRules.DescriptionOf(kept.Text), kept.File) is null;
        if (step is MemoryTidyStep.Describe)
        {
            if (locked)
            {
                return MemoryTidyCheck.Refused(LockedRefusal, files);
            }

            return ownWorks
                ? MemoryTidyCheck.Refused($"the description of {kept.File} is fine as it is", files)
                : new MemoryTidyCheck(null, files, false, given, null);
        }

        if (locked && !ownWorks)
        {
            return MemoryTidyCheck.Refused(LockedRefusal, files);
        }

        bool keepsOwn = given is not null && ownWorks;
        return new MemoryTidyCheck(null, files, heavy, keepsOwn ? null : given, keepsOwn ? KeptOwnDescription : null);
    }

    private static string? NewText(MemoryTidyStep step) => step switch
    {
        MemoryTidyStep.Merge merge => merge.Text,
        MemoryTidyStep.Rewrite rewrite => rewrite.Text,
        _ => null,
    };

    private static string? NewDescription(MemoryTidyStep step) => step switch
    {
        MemoryTidyStep.Merge merge => merge.Description,
        MemoryTidyStep.Rewrite rewrite => rewrite.Description,
        MemoryTidyStep.Describe describe => describe.Description,
        _ => null,
    };

    private static long Bytes(string text) => Encoding.UTF8.GetByteCount(text.Trim());
}

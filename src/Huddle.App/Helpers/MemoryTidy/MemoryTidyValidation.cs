using System.Globalization;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>The whole-Verdict checks of Memory Tidy spec §6.10: a Verdict that fails any of them is thrown away whole.</summary>
internal static class MemoryTidyValidation
{
    /// <summary>Checks a Verdict's shape, its size and that no file is used twice.</summary>
    /// <param name="verdict">The parsed Verdict.</param>
    /// <param name="limits">The caps for this folder.</param>
    /// <returns>The problem, or <see langword="null"/> when the Verdict can be applied step by step.</returns>
    internal static string? Validate(MemoryTidyVerdict verdict, MemoryTidyLimits limits)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(limits);

        if (verdict.Actions is not { } actions)
        {
            return "the answer needs an actions list";
        }

        List<MemoryTidyStep> steps = new(actions.Count);
        foreach (MemoryTidyAction action in actions)
        {
            if (action is null)
            {
                return "an action must be merge, rewrite, describe or drop";
            }

            if (MemoryTidyStep.TryParse(action, out string? shape) is not { } step)
            {
                return shape;
            }

            steps.Add(step);
        }

        return SizeProblem(steps, limits) ?? DuplicateProblem(steps);
    }

    private static string? SizeProblem(List<MemoryTidyStep> steps, MemoryTidyLimits limits)
    {
        if (steps.Count > limits.MaxActions)
        {
            return string.Create(CultureInfo.InvariantCulture, $"the answer holds {steps.Count:N0} actions; the most is {limits.MaxActions:N0}");
        }

        int drops = steps.Count(static step => step is MemoryTidyStep.Drop);
        if (drops > limits.MaxDrops)
        {
            return string.Create(CultureInfo.InvariantCulture, $"the answer holds {drops:N0} drops; the most is {limits.MaxDrops:N0}");
        }

        long chars = steps.Sum(static step => step switch
        {
            MemoryTidyStep.Merge merge => (long)merge.Text.Length,
            MemoryTidyStep.Rewrite rewrite => rewrite.Text.Length,
            MemoryTidyStep.Describe => 0L,
            MemoryTidyStep.Drop => 0L,
            _ => 0L,
        });
        return chars > limits.MaxAnswerChars
            ? string.Create(CultureInfo.InvariantCulture, $"the answer holds {chars:N0} characters of text; the most is {limits.MaxAnswerChars:N0}")
            : null;
    }

    private static string? DuplicateProblem(List<MemoryTidyStep> steps)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (MemoryTidyStep step in steps)
        {
            foreach (string name in step.Sources)
            {
                if (!seen.Add(name))
                {
                    return $"{name} is used in two actions";
                }
            }
        }

        return null;
    }
}

using System.Text.Json.Serialization.Metadata;

namespace Agency.Huddle.App.Helpers;

/// <summary>A Helper that judges one <typeparamref name="TInput"/> and answers with one <typeparamref name="TVerdict"/>.</summary>
/// <typeparam name="TInput">Everything the Helper needs, captured when the event happened.</typeparam>
/// <typeparam name="TVerdict">The typed answer.</typeparam>
internal interface IHelperAgent<TInput, TVerdict> : IHelperAgent
    where TVerdict : class
{
    /// <summary>Source-generated JSON metadata for the Verdict, so parsing needs no reflection.</summary>
    JsonTypeInfo<TVerdict> VerdictType { get; }

    /// <summary>The hash of everything the prompt shows; equal hashes get the same Verdict.</summary>
    /// <param name="input">The input.</param>
    string Fingerprint(TInput input);

    /// <summary>The prompt values for <paramref name="input"/>, rendered through the Helper's prompt keys.</summary>
    /// <param name="input">The input.</param>
    IReadOnlyDictionary<string, string> PromptValues(TInput input);

    /// <summary>Checks a parsed Verdict.</summary>
    /// <param name="verdict">The Verdict.</param>
    /// <returns>The problem, or <see langword="null"/> when it is usable.</returns>
    string? Validate(TVerdict verdict);

    /// <summary>Acts on a Verdict through the domain service. Never throws for an expected refusal.</summary>
    /// <param name="key">What the judgement was about, such as a Task id.</param>
    /// <param name="input">The input that was judged.</param>
    /// <param name="verdict">The Verdict.</param>
    /// <param name="cancellationToken">Cancels the apply.</param>
    Task<HelperApplied> ApplyAsync(string key, TInput input, TVerdict verdict, CancellationToken cancellationToken);
}

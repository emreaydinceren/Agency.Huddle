namespace Agency.Huddle.App.Helpers;

/// <summary>What the dispatcher and the Helpers tab know about any Helper.</summary>
internal interface IHelperAgent
{
    /// <summary>The id, stable across releases: it keys the stored settings and usage.</summary>
    HelperId Id { get; }

    /// <summary>The name on the card and in the Change log, such as "Needs Human".</summary>
    string DisplayName { get; }

    /// <summary>One sentence for the card: what it judges and what it may change.</summary>
    string Description { get; }

    /// <summary>The prompt catalog keys this Helper renders, the system prompt first.</summary>
    IReadOnlyList<string> PromptKeys { get; }

    /// <summary>The code defaults, before appsettings and the Human's choice.</summary>
    HelperDefaults Defaults { get; }

    /// <summary>
    /// Whether <see cref="HelperInvoker{TInput, TVerdict}"/> keeps this Helper's Verdicts by content and reuses them. A Helper whose
    /// Verdicts are large, or whose callers already avoid a second call, answers <see langword="false"/>: the cache is then
    /// neither read nor filled.
    /// </summary>
    bool CachesVerdicts => true;
}

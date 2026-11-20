namespace Agency.Huddle.App.Components.Settings;

/// <summary>
/// Which section the Settings page's left menu is showing. Public for the same reason
/// <c>AgentProfileCardMode</c> is: Razor generates <c>Settings.razor</c>'s component as a public type, and
/// a route parameter or field of an <c>internal</c> enum type would not compile there.
/// </summary>
public enum SettingsTab
{
    /// <summary>The Human's own profile: Name, Alias, Title, Description and Avatar. The first tab, and where a blank system opens.</summary>
    Profile,

    /// <summary>The read-only view of every model-facing prompt, grouped by area.</summary>
    Prompts,

    /// <summary>Helpers: the background agents that make one judgement each, and what they may spend.</summary>
    Helpers,

    /// <summary>What Adapters have reported spending: per Team, Agent and the Helpers, and a Total per currency. Read-only.</summary>
    Spending,

    /// <summary>The global Needs You level and its spend cap (ADR-0088): how much of the Teams' work waits for the Human.</summary>
    NeedsYou,

    /// <summary>Choosing a theme, the accent colour and the Library pane side.</summary>
    Appearance,

    /// <summary>Every Skill this installation currently resolves - what it is, where it came from, and any problems found reading it.</summary>
    Skills,

    /// <summary>Every team type the catalog serves, and the ones it rejected with the reason.</summary>
    TeamTypes,

    /// <summary>The Library settings: pinned roots and the pane side.</summary>
    Library,

    /// <summary>How much a fresh session rereads of a Room (LR-R9).</summary>
    Sessions,

    /// <summary>Remote access: the gateway, pairing requests, devices and the Security check. Admin only (Spec 6.12).</summary>
    RemoteAccess,
}

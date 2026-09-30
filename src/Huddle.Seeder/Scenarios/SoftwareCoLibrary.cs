using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// Library notes, Team and Project memory, and Teammate working files for the <c>software-co</c> scenario.
/// Wikilinks resolve by file name, so <c>Personas</c> gains three backlinks, <c>Architecture</c> gains three,
/// and <c>Legal-Checklist</c> is deliberately left unresolved.
/// </summary>
internal static class SoftwareCoLibrary
{
    /// <summary>The folders the scenario needs even where nothing is written into them yet; see <see cref="SeedTeam"/>.</summary>
    internal static IReadOnlyList<SeedTeam> Teams { get; } =
    [
        new("Platform", ["API v2", "Observability"]),
        new("Growth", ["Onboarding Revamp", "Pricing Page", "Lifecycle Emails"]),
        new("Support", ["Help Center", "Incident Tooling"]),
        new("Sandbox", []),
        new("Archive", []),
    ];

    /// <summary>Every note, memory fact and working file.</summary>
    internal static IReadOnlyList<SeedFile> Files { get; } =
    [
        // ---- Library notes ----
        new("Teams/Platform/Architecture.md", """
            # Platform architecture

            The gateway fronts four services: accounts, billing, notifications and reports. Every request
            is traced end to end; see the [[Runbook]] for what to do when a trace looks wrong.

            The public contract is described in [[API-v2-Design]].
            """),
        new("Teams/Platform/API v2/API-v2-Design.md", """
            # API v2 design

            Resources are plural nouns, IDs are opaque strings, and every list endpoint is paginated
            with a cursor. See [[Pagination]] for the cursor format.

            Rate limits are per API key. The wider system is drawn in [[Architecture]].
            """),
        new("Teams/Platform/API v2/Pagination.md", """
            # Pagination

            A cursor is an opaque, URL-safe string. Clients pass it back as `?after=` and never parse it.

            ## Page size

            The default is 50 and the maximum is 200. Asking for more returns 200 and a warning header.
            """),
        new("Teams/Platform/Observability/Runbook.md", """
            # Observability runbook

            1. Open the p99 dashboard and note the affected service.
            2. Find a slow trace and follow it to the service that owns the span.
            3. Compare with the deploy log. Roll back first, investigate second.

            Service layout: [[Architecture]].
            """),
        new("Teams/Growth/Personas.md", """
            # Growth personas

            ## Solo founder

            Signs up alone, wants a working workspace in ten minutes, and pays with a personal card.

            ## Team lead

            Signs up, then invites four to eight colleagues. Cares about seat pricing and permissions.

            ## Operations manager

            Arrives from a sales call. Needs an invoice and a security questionnaire before anything else.
            """),
        new("Teams/Growth/Onboarding Revamp/Onboarding-Brief.md", """
            # Onboarding revamp brief

            ## Goals

            Raise signup-to-first-project conversion from 31 to 40 percent.

            ## Who it is for

            The three people in [[Personas]], starting with the solo founder. The price they see
            afterwards is in [[Pricing-Research]].
            """),
        new("Teams/Growth/Pricing Page/Pricing-Research.md", """
            # Pricing research

            Twelve competitors compared. Nine charge per seat, three by usage. The team-lead
            persona in [[Personas]] is the one most sensitive to per-seat pricing.

            | Competitor | Model | Entry price |
            | --- | --- | --- |
            | A | per seat | 9 |
            | B | usage | 15 |
            | C | per seat | 12 |
            """),
        new("Teams/Growth/Pricing Page/Pricing-Brief.md", """
            # Pricing page brief

            Three tiers: Starter, Team and Business. Built for [[Personas]] and backed by
            [[Pricing-Research]].

            Before launch, run through the [[Legal-Checklist]] and get sign-off from the Human.
            """),
        new("Teams/Growth/Lifecycle Emails/Email-Ideas.md", """
            # Lifecycle email ideas

            - Day 0: welcome and the one thing to do first.
            - Day 3: a short story about a customer who did that thing.
            - Day 14: you have not been back; here is what changed.
            """),
        new("Teams/Support/Escalation-Guide.md", """
            # Escalation guide

            1. Confirm the customer impact in one sentence.
            2. Page the on-call engineer if more than one customer is affected.
            3. Post in the incident Room and open a Task for the follow-ups.
            """),
        new("Teams/Support/Help Center/Style-Guide.md", """
            # Help centre style guide

            Start with the answer. Use numbered steps for actions and bullets for facts.
            Never say "simply". Link to the [[Escalation-Guide]] where a human is needed.
            """),
        new("Teams/Support/Incident Tooling/Incident-0412.md", """
            # Incident 0412

            **Impact:** the reports service returned errors for 47 minutes.

            **Cause:** a connection pool limit set too low after a deploy. The layout of the
            affected services is in [[Architecture]].

            **Follow-ups:** the status page, the pager rotation and a pool-size alert.
            """),
        new("Teammates/Alan/work/Postmortem-Draft-0412.md", """
            # Postmortem draft: incident 0412

            Private draft, not yet reviewed. Facts and timeline are in [[Incident-0412]].

            ## Timeline (UTC)

            - 09:02 first alert fires on the reports service
            - 09:11 on-call acknowledges
            - 09:49 pool limit raised and errors stop
            """),

        // ---- Team memory ----
        new("Teams/Platform/memory/api-versioning-policy.md", "Breaking changes ship only in a new major API version, never in a minor one.\n"),
        new("Teams/Platform/memory/deploy-freeze-fridays.md", "No production deploys after 12:00 UTC on Fridays.\n"),
        new("Teams/Growth/memory/signup-conversion-target.md", "The signup-to-first-project conversion target is 40 percent.\n"),
        new("Teams/Growth/memory/launch-freeze-last-friday.md", "Marketing launches freeze on the last Friday of each month.\n"),
        new("Teams/Support/memory/first-response-sla.md", "First response to an Urgent ticket is within 30 minutes, around the clock.\n"),

        // ---- Project memory ----
        new("Teams/Platform/API v2/memory/v1-sunset-window.md", "API v1 is retired 180 days after API v2 launches.\n"),
        new("Teams/Growth/Pricing Page/memory/launch-timing.md", "The pricing page launches together with the Q4 campaign, not before it.\n"),

        // ---- Teammate personal memory ----
        new("Teammates/Grace/work/memory/prefers-short-standups.md", "Stand-ups run 15 minutes at most; anything longer moves to its own Room.\n"),
        new("Teammates/Maya/work/memory/brand-voice-rules.md", "Brand voice: plain words, short sentences, no exclamation marks in headlines.\n"),
        new("Teammates/Ben/work/memory/supplier-payment-terms.md", "Suppliers are paid net 30 unless the invoice says otherwise.\n"),
    ];
}

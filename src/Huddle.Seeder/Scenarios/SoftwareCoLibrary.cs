using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// Library notes, Team and Project memory, and Agent working files for the <c>software-co</c> scenario.
/// Wikilinks resolve by file name, so <c>Personas</c> gains three backlinks, <c>Architecture</c> gains three,
/// and <c>Legal-Checklist</c> is deliberately left unresolved.
/// Seeder spec SEED-SC-1, -2 and -6 to -8; new notes and memory go in SoftwareCoNotes (Appendix A.2).
/// </summary>
internal static class SoftwareCoLibrary
{
    private const string Handbook = "Teams/Platform/Engineering-Handbook.md";
    private const string PricingBrief = "Teams/Growth/Pricing Page/Pricing-Brief.md";
    private const string Incident = "Teams/Support/Incident Tooling/Incident-0412.md";

    /// <summary>
    /// The example of the front matter properties spec, character for character: every type, a Mismatch (<c>due</c>), an Unsupported
    /// value (<c>budget</c>), property links, aliases and a multi-line Text. The FP-T1 test reads this same text.
    /// </summary>
    internal const string AllTypesNote = """
        ---
        title: Project Plan
        url: https://example.com/plan
        pages: 320
        reviewed: true
        created: 2024-01-15
        logged: 2024-01-15T09:00:00
        due: next week
        owner: "[[Architecture]]"
        authors:
          - Ada
          - "[[Architecture]]"
        tags:
          - recipe
          - cooking
        aliases: [Plan, Q1 plan]
        cssclasses:
          - wide
        budget:
          amount: 1200
          currency: EUR
        notes: |
          First line.
          Second line.
        empty:
        ---
        """;

    /// <summary>The same properties as JSON front matter, without <c>budget</c> and <c>notes</c>: it draws the same rows as <see cref="AllTypesNote"/>.</summary>
    internal const string AllTypesJsonNote = """
        {
          "title": "Project Plan",
          "url": "https://example.com/plan",
          "pages": 320,
          "reviewed": true,
          "created": "2024-01-15",
          "logged": "2024-01-15T09:00:00",
          "due": "next week",
          "owner": "[[Architecture]]",
          "authors": ["Ada", "[[Architecture]]"],
          "tags": ["recipe", "cooking"],
          "aliases": ["Plan", "Q1 plan"],
          "cssclasses": ["wide"],
          "empty": null
        }
        """;

    /// <summary>The edge cases: a missing link, an unquoted link, a quoted boolean, deprecated and publish-only keys, a malformed entry and a repeated key.</summary>
    internal const string EdgeCasesNote = """
        ---
        reviewer: "[[Nobody]]"
        related: [[Architecture]]
        flag: "true"
        tag: legacy
        cssclass: wide
        publish: true
        broken: "unclosed
        status: draft
        status: final
        ---
        """;

    /// <summary>The types file of the seeded root: <c>pages</c> is a Number and <c>due</c> a Date, in the file's format.</summary>
    internal const string PropertyTypesJson = """
        {
          "types": {
            "pages": "number",
            "due": "date"
          }
        }
        """;

    /// <summary>The folders the scenario needs even where nothing is written into them yet; see <see cref="SeedTeam"/>.</summary>
    internal static IReadOnlyList<SeedTeam> Teams { get; } =
    [
        new("Platform", ["API v2", "Observability"]),
        new("Growth", ["Onboarding Revamp", "Pricing Page", "Lifecycle Emails"]),
        new("Support", ["Help Center", "Incident Tooling"]),
        new("Sandbox", []),
        new("Archive", []),
    ];

    /// <summary>The picture files: a chart in a Team folder (Library image preview). Agent avatars come from the Avatar Library (<c>AvatarWriter</c>).</summary>
    internal static IReadOnlyList<SeedBinaryFile> BinaryFiles { get; } =
    [
        new("Teams/Growth/Pricing Page/competitor-chart.png", SeedImages.Gradient(64, (0x2e, 0x7d, 0x6b), (0xd1, 0x8a, 0x00))),
    ];

    /// <summary>
    /// Files attached in chats, one PNG and one text file in a root Room (<c>dm-owen</c>) and in a Team Room (<c>team-platform</c>).
    /// A Message cites each with the <c>{{upload:name}}</c> token.
    /// </summary>
    internal static IReadOnlyList<SeedUpload> Uploads { get; } =
    [
        new(SoftwareCoRooms.DmOwen, "signup-funnel.png", SeedImages.Gradient(48, (0xd4, 0x60, 0x02), (0x3b, 0x6e, 0xa5))),
        new(SoftwareCoRooms.DmOwen, "price-claims.txt", System.Text.Encoding.UTF8.GetBytes("Claims to check with legal:\n- \"Cheapest in class\"\n- \"No hidden fees\"\n")),
        new(SoftwareCoRooms.TeamPlatform, "gateway-latency.png", SeedImages.Gradient(48, (0x5b, 0x5f, 0x97), (0xc2, 0x45, 0x6b))),
        new(SoftwareCoRooms.TeamPlatform, "oncall-handover.txt", System.Text.Encoding.UTF8.GetBytes("Open alerts: none.\nPromised to a customer: a postmortem by Friday.\n")),
    ];

    /// <summary>
    /// Files the Human uploaded to Tasks: a PNG and a text file on open Tasks, and one on a closed Task, whose folder is still
    /// <c>Uploads/Tasks/&lt;ID&gt;</c> because closing never moves it. <see cref="SoftwareCoTasks"/> lists each in its Task's
    /// <see cref="SeedTask.Attachments"/>.
    /// </summary>
    internal static IReadOnlyList<SeedTaskUpload> TaskUploads { get; } =
    [
        new("GROW-0002", "checklist-mockup.png", SeedImages.Gradient(48, (0x2e, 0x7d, 0x6b), (0xff, 0xff, 0xff))),
        new("SUPP-0008", "action-items.txt", System.Text.Encoding.UTF8.GetBytes("Postmortem actions for incident 0412:\n- Status page (SUPP-0005)\n- Pager rotation (SUPP-0007)\n- Pool-size alert\n")),
        new("SUPP-0006", "postmortem-template.txt", System.Text.Encoding.UTF8.GetBytes("Summary\nImpact\nTimeline (UTC)\nCause\nFollow-ups\n")),
    ];

    /// <summary>
    /// Document History: a long History on the handbook (every kind, two Human saves inside the grouping window and more rows than
    /// the History panel shows), an objection on the pricing brief, a review on the incident, an Agent's private draft and a note the
    /// Human created. Every other note has no History, so "No edits logged." stays testable.
    /// </summary>
    internal static IReadOnlyList<SeedDocEdit> DocHistory { get; } =
    [
        new(Handbook, 21, "09:30", "Grace", DocActorKind.Agent, DocChangeKind.Created, "First draft: working agreements, operations and reference"),
        new(Handbook, 18, "11:05", "Marcus", DocActorKind.Agent, DocChangeKind.Edited, "Added the review checklist and the pagination rule"),
        new(Handbook, 12, "14:20", "Alan", DocActorKind.Agent, DocChangeKind.Edited, "Wrote the On call and Handover notes sections"),
        new(Handbook, 6, "10:00", "You", DocActorKind.Human, DocChangeKind.Edited),
        new(Handbook, 6, "10:03", "You", DocActorKind.Human, DocChangeKind.Edited),
        new(Handbook, 4, "15:40", "Lena", DocActorKind.Agent, DocChangeKind.Reviewed, "Checked the release rules against the Friday deploy freeze"),
        new(Handbook, 2, "09:15", "Marcus", DocActorKind.Agent, DocChangeKind.Objected, "Same-day review is not realistic for contract changes"),
        new(Handbook, 1, "16:30", "Grace", DocActorKind.Agent, DocChangeKind.Edited, "Contract changes now get two working days for review"),
        new(PricingBrief, 9, "09:00", "Maya", DocActorKind.Agent, DocChangeKind.Created, "Three tiers, built on the pricing research"),
        new(PricingBrief, 5, "10:05", "You", DocActorKind.Human, DocChangeKind.Edited),
        new(PricingBrief, 3, "13:45", "Sofia", DocActorKind.Agent, DocChangeKind.Objected, "Starter undercuts Team for groups of three or fewer"),
        new(Incident, 9, "11:00", "Alan", DocActorKind.Agent, DocChangeKind.Created, "Impact, cause and follow-ups from the war room"),
        new(Incident, 8, "09:30", "Dana", DocActorKind.Agent, DocChangeKind.Reviewed, "Customer impact matches the support tickets"),
        new("Agents/Alan/work/Postmortem-Draft-0412.md", 8, "17:10", "Alan", DocActorKind.Agent, DocChangeKind.Created, "Timeline drafted from the alert log"),
        new("Teams/Growth/Lifecycle Emails/Email-Ideas.md", 7, "08:45", "You", DocActorKind.Human, DocChangeKind.Created),
        new("Teams/Platform/memory/deploy-freeze-fridays.md", 3, "12:10", "Team Memory (Helper)", DocActorKind.Helper, DocChangeKind.Created, "Proposed by Alan: a new rule for Friday deploys"),
    ];

    /// <summary>Every note, memory fact and working file.</summary>
    internal static IReadOnlyList<SeedFile> Files { get; } =
    [
        // ---- Library notes ----
        new("Teams/Platform/Architecture.md", """
            ---
            title: Platform architecture
            tags: [platform, architecture]
            ---

            # Platform architecture

            The gateway fronts four services: accounts, billing, notifications and reports. Every request
            is traced end to end; see the [[Runbook]] for what to do when a trace looks wrong.

            The public contract is described in [[API-v2-Design]].
            """),
        new("Teams/Platform/Meeting-Notes-2026-10-14.md", """
            ---
            title: Platform weekly, 14 October
            date: 2026-10-14
            tags: platform, meeting/weekly
            ---

            # Platform weekly, 14 October

            Attendees: Grace, Marcus, Lena and Alan.

            ## Decisions

            - The API v1 sunset is planned for #2026/q4. The plan is in [[Deprecation-Plan]].
            - The pool-size alert from incident 0412 is tracked in #SUPP-0008 and still needs an owner.
            - Paging stays limited to customer impact, see #platform/ops.

            ## Actions

            - Marcus drafts the migration guide #platform/api/v2
            - Alan writes the alert rule (helpdesk ticket #4521)
            """),
        new("Teams/Platform/API v2/Deprecation-Plan.md", """
            ---
            title: API v1 deprecation plan
            tags: ["platform/api", 'v1-sunset', Deprecation]
            ---

            # API v1 deprecation plan

            v1 is retired 180 days after v2 launches. Customers get three notices: at launch, at 90 days and at 150 days.

            ## Timeline

            Launch is targeted for #2026/q4, so the sunset lands in #2027/q2.

            ## Communication

            Notices go out from the status page; the draft is tracked in #SUPP-0005. The discussion is in [[Meeting-Notes-2026-10-14]].
            """),
        new("Teams/Growth/Onboarding Revamp/Interview-Notes.md", """
            ---
            tags:
              - growth/research
              - user-interview
              - müşteri/geri-bildirim
            ---

            # Onboarding interviews

            Five interviews with people who signed up in the last month.

            ## Solo founder

            Wanted to import a project from a spreadsheet and could not find where. #persona/solo-founder #onboarding/import

            ## Team lead

            Got stuck inviting colleagues: the invite button looked disabled. #persona/team-lead #onboarding/invite

            Everyone liked the sample project. #🚀
            """),
        new("Teams/Growth/Pricing Page/Competitor-Teardown.md", """
            # Competitor teardown

            Three competitors, compared on how they show price. The chart is `competitor-chart.png` in this folder.

            Pages were captured from https://competitor.example/pricing#faq on 8 October. The accent colour in the captures is `#1a73e8`.

            ## Per seat #pricing/per-seat

            Competitors A and C charge per seat and hide the annual discount below the fold.

            ## Usage based #pricing/usage

            Competitor B charges by usage and shows a calculator.

            ## Takeaways #growth/pricing

            Show the annual discount next to the price, not below it.
            """),
        new("Teams/Growth/Lifecycle Emails/Welcome-Email-Draft.md", """
            ---
            title: Welcome email, draft 2
            tags: #draft
            ---

            # Welcome email

            Subject: Your workspace is ready

            Hi there,

            Your workspace is ready. The first thing to do is create a project.

            #email/welcome #draft
            """),
        new("Teams/Support/Help Center/Release-Notes-Draft.md", """
            ---
            title: Help centre release notes
            tags: [support/docs, release-notes]
            ...

            # Help centre release notes

            ## October changes

            - New article on resetting two-factor codes. #support/docs
            - Rewrote the billing FAQ.

            <!-- #internal: waiting on legal before we publish the refund wording -->

            %% #todo check the screenshots %%

            Published articles are listed at https://help.example.com/articles#recent.
            """),
        new("Teams/Support/Customer-Feedback-Log.md", """
            # Customer feedback log

            October entries, newest first. Ticket numbers are helpdesk tickets, not Tasks.

            - Export to CSV times out on large workspaces (ticket #4521). #feedback/bug #Reports
            - Please add dark mode to the mobile app. #feedback/feature-request
            - Love the new onboarding checklist! #feedback/praise #onboarding
            - Invoice PDF shows the wrong VAT number (ticket #4533). #feedback/bug #billing
            - Reports page is slow again after the 0412 incident. #feedback/Bug #reports
            """),
        new("Teams/Platform/Engineering-Handbook.md", """
            # Platform engineering handbook

            How the Platform Team works day to day. Long on purpose: it gives the Outline panel four heading levels to show.
            The system itself is drawn in [[Architecture]].

            ## Working agreements

            Small changes, reviewed the same day, released behind a flag when they touch a public contract.

            ### Code review

            Every change has one reviewer from the Team. Contract changes also need the author of [[API v2/API-v2-Design]].

            #### Review checklist

            - The change does one thing.
            - New list endpoints follow [[Pagination#Page size]].
            - Logs carry the request id.

            #### Review etiquette

            Comment on the code, not the person. Approve with comments when the remaining points are small.

            ### Releases

            Releases go out on weekdays before noon UTC; the Friday rule is in Team memory.

            #### Rollback

            Roll back first, investigate second. The on-call steps are in [[Runbook|the on-call runbook]].

            ## Operations

            Everything that keeps the gateway healthy once it is live.

            ### On call

            One engineer is on call per week and hands over on Monday morning.

            #### Handover notes

            Write down open alerts, flaky dashboards and anything promised to a customer.

            ### Alerts

            Page only for customer impact. Everything else goes to the daily summary.

            ## Reference

            Where to look when this page is not enough.

            ### Related notes

            - [[Architecture]] for the service layout
            - [[Pagination]] for cursors

            #platform/process #Platform
            """),
        new("Teams/Platform/Observability/oncall-contacts.txt", """
            On-call contacts (plain text note, not Markdown)

            Primary:   the engineer on the rota this week
            Secondary: the Engineering Lead
            Escalate:  after 15 minutes without an acknowledgement
            """),
        new("Teams/Platform/API v2/API-v2-Design.md", """
            # API v2 design

            Resources are plural nouns, IDs are opaque strings, and every list endpoint is paginated
            with a cursor. See [[Pagination]] for the cursor format.

            Rate limits are per API key. The wider system is drawn in [[Architecture]].

            #platform/api #platform/api/v2
            """),
        new("Teams/Platform/API v2/Pagination.md", """
            # Pagination

            A cursor is an opaque, URL-safe string. Clients pass it back as `?after=` and never parse it.

            ## Page size

            The default is 50 and the maximum is 200. Asking for more returns 200 and a warning header.

            #platform/api
            """),
        new("Teams/Platform/Observability/Runbook.md", """
            # Observability runbook

            1. Open the p99 dashboard and note the affected service.
            2. Find a slow trace and follow it to the service that owns the span.
            3. Compare with the deploy log. Roll back first, investigate second.

            Service layout: [[Architecture]].

            #platform/ops #support/incident
            """),
        new("Teams/Growth/Personas.md", """
            # Growth personas

            ## Solo founder

            Signs up alone, wants a working workspace in ten minutes, and pays with a personal card.

            ## Team lead

            Signs up, then invites four to eight colleagues. Cares about seat pricing and permissions.

            ## Operations manager

            Arrives from a sales call. Needs an invoice and a security questionnaire before anything else.

            #growth/research
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
            ---
            tags:
              - growth/research
              - growth/pricing
            ---

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

            #growth/pricing #Launch
            """),
        new("Teams/Growth/Lifecycle Emails/Email-Ideas.md", """
            # Lifecycle email ideas

            - Day 0: welcome and the one thing to do first.
            - Day 3: a short story about a customer who did that thing.
            - Day 14: you have not been back; here is what changed.

            #growth/email #launch
            """),
        new("Teams/Support/Escalation-Guide.md", """
            # Escalation guide

            1. Confirm the customer impact in one sentence.
            2. Page the on-call engineer if more than one customer is affected.
            3. Post in the incident Room and open a Task for the follow-ups.

            #support/incident #support/escalation
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

            #support/incident #platform/ops
            """),
        new("Agents/Alan/work/Postmortem-Draft-0412.md", """
            # Postmortem draft: incident 0412

            Private draft, not yet reviewed. Facts and timeline are in [[Incident-0412]].

            ## Timeline (UTC)

            - 09:02 first alert fires on the reports service
            - 09:11 on-call acknowledges
            - 09:49 pool limit raised and errors stop

            #support/incident #draft
            """),

        // ---- Front matter properties (docs/Huddle.FrontMatterProperties-Specifications.md, Seeded demo) ----
        new("Teams/Platform/AllTypes.md", AllTypesNote),
        new("Teams/Platform/AllTypes-Json.md", AllTypesJsonNote),
        new("Teams/Platform/Properties-Edge-Cases.md", EdgeCasesNote),
        new("Teams/Platform/Plan-Review.md", """
            # Plan review

            The plan is written up in [[AllTypes|Plan]], which is also found under that alias.

            A bare alias is not a link target: [[Plan]] stays a missing link.
            """),

        // The reference app's settings folder in the Teams root: hidden from the Library tree, but read for the types of the properties above.
        new("Teams/" + LibraryOptions.DefaultPropertyTypesFile, PropertyTypesJson),

        // ---- Team memory ----
        new("Teams/Platform/memory/api-versioning-policy.md", "Breaking changes ship only in a new major API version, never in a minor one.\n"),
        new("Teams/Platform/memory/deploy-freeze-fridays.md", "No production deploys after 12:00 UTC on Fridays.\n"),
        new("Teams/Growth/memory/signup-conversion-target.md", "The signup-to-first-project conversion target is 40 percent.\n"),
        new("Teams/Growth/memory/launch-freeze-last-friday.md", "Marketing launches freeze on the last Friday of each month.\n"),
        new("Teams/Support/memory/first-response-sla.md", "First response to an Urgent ticket is within 30 minutes, around the clock.\n"),

        // ---- Project memory ----
        new("Teams/Platform/API v2/memory/v1-sunset-window.md", "API v1 is retired 180 days after API v2 launches.\n"),
        new("Teams/Growth/Pricing Page/memory/launch-timing.md", "The pricing page launches together with the Q4 campaign, not before it.\n"),

        // ---- Agent personal memory ----
        new("Agents/Grace/work/memory/prefers-short-standups.md", "Stand-ups run 15 minutes at most; anything longer moves to its own Room.\n"),
        new("Agents/Maya/work/memory/brand-voice-rules.md", "Brand voice: plain words, short sentences, no exclamation marks in headlines.\n"),
        new("Agents/Ben/work/memory/supplier-payment-terms.md", "Suppliers are paid net 30 unless the invoice says otherwise.\n"),
    ];
}

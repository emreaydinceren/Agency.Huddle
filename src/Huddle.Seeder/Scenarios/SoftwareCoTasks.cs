using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>The Tasks for the <c>software-co</c> scenario.</summary>
internal static class SoftwareCoTasks
{
    private const string Human = "You";
    private const string Platform = "Platform";
    private const string Growth = "Growth";
    private const string Support = "Support";
    private const string Archive = "Archive";
    private const string ApiV2 = "API v2";
    private const string Observability = "Observability";
    private const string Onboarding = "Onboarding Revamp";
    private const string Pricing = "Pricing Page";
    private const string HelpCenter = "Help Center";
    private const string IncidentTooling = "Incident Tooling";

    /// <summary>All 41 Tasks. Room keys in <see cref="SeedTask.OriginRoom"/> refer to <see cref="SoftwareCoRooms"/>.</summary>
    internal static IReadOnlyList<SeedTask> All { get; } =
    [
        // ---- Platform / API v2 ----
        new()
        {
            Id = "PLAT-0001", Location = new(Platform, ApiV2), Title = "Define the v2 resource model",
            Status = "Done", Priority = "High", Creator = Human, Assignee = "Marcus", Closed = true,
            Tags = ["api", "design"], CreatedDaysAgo = 40, LastTouchedDaysAgo = 30,
            Body = "Agree the resource names, IDs and relationships for API v2 before any endpoint is built.",
        },
        new()
        {
            Id = "PLAT-0002", Location = new(Platform, ApiV2), Title = "Implement pagination for list endpoints",
            Status = "In Progress", Priority = "High", Creator = Human, Assignee = "Marcus",
            Tags = ["api", "pagination"], DueOffset = -4, StartOffset = -20, BlockedBy = ["PLAT-0001"],
            CreatedDaysAgo = 25, LastTouchedDaysAgo = 2,
            ExtraLog = [new("You", "priority: Medium → High")],
            Body = "Cursor-based pagination for every list endpoint. PLAT-0001 is finished, so this Task is not blocked.",
        },
        new()
        {
            Id = "PLAT-0003", Location = new(Platform, ApiV2), Title = "Publish the OpenAPI spec",
            Status = "To Do", Priority = "Medium", Creator = "Grace", Assignee = "Lena",
            Tags = ["api", "docs"], DueOffset = 10, BlockedBy = ["PLAT-0002"],
            CreatedDaysAgo = 12, LastTouchedDaysAgo = 3,
            Body = "Generate and host the OpenAPI document. Waiting on pagination so the schema is final.",
        },
        new()
        {
            Id = "PLAT-0004", Location = new(Platform, ApiV2), Title = "Rate limiting per API key",
            Status = "Review", Priority = "High", Creator = Human, Assignee = "Grace",
            Tags = ["api", "security"], DueOffset = 2, CreatedDaysAgo = 18, LastTouchedDaysAgo = 1,
            Body = "Token-bucket limiter keyed on API key. Marcus opened the change; Grace is reviewing.",
        },
        new()
        {
            Id = "PLAT-0005", Location = new(Platform, ApiV2), Title = "Design the pagination cursor format",
            Status = "To Do", Priority = "Medium", Creator = "Marcus", Assignee = "Marcus",
            Parent = "PLAT-0002", CreatedDaysAgo = 22, LastTouchedDaysAgo = 5,
            Body = "Sub-task of PLAT-0002: opaque, URL-safe cursors that survive re-ordering.",
        },
        new()
        {
            Id = "PLAT-0006", Location = new(Platform, ApiV2), Title = "Plan the deprecation of API v1",
            Status = "Backlog", Priority = "Low", Creator = "Grace", CreatedDaysAgo = 9, LastTouchedDaysAgo = 9,
            Tags = ["api", "planning"],
            Body = """
                Decide how and when API v1 is retired.

                ## Goals

                - No customer is surprised. Every v1 caller is told at least 90 days ahead.
                - The v1 code path is deleted, not just switched off.

                ## Open questions

                1. Do we keep a read-only v1 for one extra quarter for the three largest customers?
                2. Who emails customers, and from which address?
                3. Which metrics prove nobody is calling v1 any more?

                ## Rough plan

                | Phase | What happens | When |
                | --- | --- | --- |
                | Announce | Email, banner in the console, header on every v1 response | Launch of v2 |
                | Warn | `Sunset` header and monthly usage report per customer | +60 days |
                | Freeze | New v1 keys are refused | +120 days |
                | Retire | v1 returns 410 Gone | +180 days |

                See also the API v2 design note and the sunset date in Project memory.
                """,
        },
        new()
        {
            Id = "PLAT-0007", Location = new(Platform, ApiV2), Title = "Add pagination to list endpoints",
            Status = "Duplicate", Priority = "Medium", Creator = Human, DuplicateOf = "PLAT-0002", Closed = true,
            CreatedDaysAgo = 26, LastTouchedDaysAgo = 24,
            Body = "Filed twice by mistake. Tracked in PLAT-0002.",
        },

        // ---- Platform / Observability ----
        new()
        {
            Id = "PLAT-0008", Location = new(Platform, Observability), Title = "Add tracing to the gateway",
            Status = "In Progress", Priority = "Medium", Creator = "Grace", Assignee = "Alan",
            Tags = ["observability", "tracing"], DueOffset = 5, StartOffset = -6, CreatedDaysAgo = 15, LastTouchedDaysAgo = 1,
            Body = "OpenTelemetry spans for every gateway request, sampled at 10 percent.",
        },
        new()
        {
            Id = "PLAT-0009", Location = new(Platform, Observability), Title = "Dashboards for p99 latency",
            Status = "To Do", Priority = "Medium", Creator = "Alan", Assignee = "Alan",
            Tags = ["observability", "dashboards"], DueOffset = 12, CreatedDaysAgo = 10, LastTouchedDaysAgo = 4,
            Body = "One dashboard per service showing p50, p95 and p99 latency over 24 hours and 7 days.",
        },
        new()
        {
            Id = "PLAT-0010", Location = new(Platform, Observability), Title = "Route alerts to the on-call rota",
            Status = "Review", Priority = "High", Creator = Human, Assignee = "Alan",
            Tags = ["observability", "on-call"], DueOffset = 3, CreatedDaysAgo = 16, LastTouchedDaysAgo = 1,
            Body = "Pages go to the current on-call engineer, then escalate to Grace after 15 minutes.",
        },
        new()
        {
            Id = "PLAT-0011", Location = new(Platform, Observability), Title = "Retire the legacy metrics exporter",
            Status = "Cancelled", Priority = "Low", Creator = "Grace", Assignee = "Marcus", Closed = true,
            CreatedDaysAgo = 35, LastTouchedDaysAgo = 20,
            Body = "Cancelled: the exporter is still used by billing and will be replaced with the new pipeline.",
        },
        new()
        {
            Id = "PLAT-0015", Location = new(Platform, Observability), Title = "Set a log retention policy",
            Status = "Rejected", Priority = "Low", Creator = "Alan", Assignee = "Alan", Closed = true,
            CreatedDaysAgo = 28, LastTouchedDaysAgo = 21,
            Body = "Rejected: legal owns retention and has set 400 days already.",
        },

        // ---- Platform team root ----
        new()
        {
            Id = "PLAT-0012", Location = new(Platform, null), Title = "Quarterly capacity review",
            Status = "To Do", Priority = "Medium", Creator = "Grace", Assignee = Human,
            Tags = ["planning"], DueOffset = 14, CreatedDaysAgo = 6, LastTouchedDaysAgo = 6,
            Body = "Review CPU, memory and database growth with Grace and decide what to buy before the next quarter.",
        },
        new()
        {
            Id = "PLAT-0013", Location = new(Platform, null), Title = "Upgrade every service to .NET 10",
            Status = "In Progress", Priority = "High", Creator = Human, Assignee = "Grace",
            Tags = ["maintenance", "dotnet"], DueOffset = 21, StartOffset = -10, CreatedDaysAgo = 30, LastTouchedDaysAgo = 1,
            Body = """
                Move every service to .NET 10 and turn on the new analyzers.

                ## Order of work

                1. Shared libraries first, so services can adopt them one at a time.
                2. Stateless services next: gateway, notifications, reports.
                3. Stateful services last: billing, accounts.

                ## Rules

                - One service per pull request.
                - Warnings are errors; do not add suppressions to finish faster.
                - Each service is deployed to staging for a full day before production.

                ## Done when

                Every service reports the new runtime on its `/health` endpoint and the old SDK image is deleted from the build farm.
                """,
        },
        new()
        {
            Id = "PLAT-0014", Location = new(Platform, null), Title = "Report staging environment costs",
            Status = "Backlog", Priority = "Low", Creator = "Grace", CreatedDaysAgo = 8, LastTouchedDaysAgo = 8,
            Body = "A weekly cost report for staging, split by service. Nobody owns it yet.",
        },

        // ---- Growth / Onboarding Revamp ----
        new()
        {
            Id = "GROW-0001", Location = new(Growth, Onboarding), Title = "Map the current signup funnel",
            Status = "Done", Priority = "High", Creator = "Owen", Assignee = "Owen", Closed = true,
            Tags = ["funnel", "research"], CreatedDaysAgo = 45, LastTouchedDaysAgo = 38,
            Body = "Steps, drop-off per step and the top three reasons people leave, from the last 90 days.",
        },
        new()
        {
            Id = "GROW-0002", Location = new(Growth, Onboarding), Title = "Redesign the welcome checklist",
            Status = "In Progress", Priority = "High", Creator = "Owen", Assignee = "Sofia",
            Tags = ["design", "onboarding"], DueOffset = 4, StartOffset = -5, CreatedDaysAgo = 20, LastTouchedDaysAgo = 1,
            OriginRoom = SoftwareCoRooms.LaunchCrew,
            Body = "Five items, all completable in under ten minutes, with a visible progress bar.",
        },
        new()
        {
            Id = "GROW-0003", Location = new(Growth, Onboarding), Title = "Build the empty-state components",
            Status = "To Do", Priority = "Medium", Creator = "Owen", Assignee = "Lena",
            Tags = ["frontend", "accessibility"], BlockedBy = ["GROW-0002"], CreatedDaysAgo = 14, LastTouchedDaysAgo = 3,
            Body = "Reusable empty, loading and error states. Blocked until the checklist design is final.",
        },
        new()
        {
            Id = "GROW-0004", Location = new(Growth, Onboarding), Title = "Instrument the signup events",
            Status = "In Progress", Priority = "High", Creator = "Owen", Assignee = "Lena",
            Tags = ["analytics"], DueOffset = -5, StartOffset = -14, CreatedDaysAgo = 24, LastTouchedDaysAgo = 2,
            ExtraLog = [new("Owen", "moved: Growth → Growth/Onboarding Revamp"), new("Owen", "description edited")],
            Body = "Emit one event per funnel step so we can measure the redesign against the baseline.",
        },
        new()
        {
            Id = "GROW-0005", Location = new(Growth, Onboarding), Title = "Write the onboarding tooltip copy",
            Status = "To Do", Priority = "Medium", Creator = "Owen", Assignee = "Maya",
            Parent = "GROW-0002", DueOffset = 6, CreatedDaysAgo = 10, LastTouchedDaysAgo = 4,
            Body = "Sub-task of GROW-0002: one sentence per checklist item, in the brand voice.",
        },
        new()
        {
            Id = "GROW-0006", Location = new(Growth, Onboarding), Title = "Spike an A/B testing framework",
            Status = "Backlog", Priority = "Low", Creator = "Owen", Tags = ["experiments"],
            CreatedDaysAgo = 7, LastTouchedDaysAgo = 7,
            Body = "Time-boxed to two days. Do we build, or buy?",
        },
        new()
        {
            Id = "GROW-0007", Location = new(Growth, Onboarding), Title = "Checklist redesign (second copy)",
            Status = "Duplicate", Priority = "Medium", Creator = "Sofia", DuplicateOf = "GROW-0002", Closed = true,
            CreatedDaysAgo = 19, LastTouchedDaysAgo = 18,
            Body = "Created by accident while the first was still being triaged.",
        },

        // ---- Growth / Pricing Page ----
        new()
        {
            Id = "GROW-0008", Location = new(Growth, Pricing), Title = "Research competitor pricing",
            Status = "Done", Priority = "Medium", Creator = "Owen", Assignee = "Owen", Closed = true,
            Tags = ["research", "pricing"], CreatedDaysAgo = 34, LastTouchedDaysAgo = 27,
            Body = "Twelve competitors compared on tiers, limits and price. Findings are in the Pricing Research note.",
        },
        new()
        {
            Id = "GROW-0009", Location = new(Growth, Pricing), Title = "Draft the three-tier structure",
            Status = "Review", Priority = "High", Creator = "Owen", Assignee = "Owen",
            Tags = ["pricing"], DueOffset = 1, CreatedDaysAgo = 17, LastTouchedDaysAgo = 1,
            OriginRoom = SoftwareCoRooms.LaunchCrew,
            Body = "Starter, Team and Business. Limits and price points ready for review.",
        },
        new()
        {
            Id = "GROW-0010", Location = new(Growth, Pricing), Title = "Design the plan comparison table",
            Status = "In Progress", Priority = "High", Creator = "Owen", Assignee = "Sofia",
            Tags = ["design", "pricing"], DueOffset = -3, StartOffset = -9, BlockedBy = ["GROW-0009"],
            CreatedDaysAgo = 15, LastTouchedDaysAgo = 2,
            Body = "Table that stays readable on a phone. Blocked by the tier structure and overdue.",
        },
        new()
        {
            Id = "GROW-0011", Location = new(Growth, Pricing), Title = "Write the pricing page copy",
            Status = "To Do", Priority = "High", Creator = "Owen", Assignee = "Maya",
            Tags = ["copy", "pricing"], DueOffset = 8, CreatedDaysAgo = 11, LastTouchedDaysAgo = 3,
            Body = "Headline, tier descriptions and the FAQ. Two headline options, please.",
        },
        new()
        {
            Id = "GROW-0012", Location = new(Growth, Pricing), Title = "Implement the pricing page",
            Status = "Backlog", Priority = "Medium", Creator = "Owen", Assignee = "Lena",
            BlockedBy = ["GROW-0010", "GROW-0011"], CreatedDaysAgo = 11, LastTouchedDaysAgo = 11,
            Body = "Build once the table design and the copy are both done.",
        },
        new()
        {
            Id = "GROW-0013", Location = new(Growth, Pricing), Title = "Legal review of price claims",
            Status = "To Do", Priority = "Medium", Creator = "Owen", Assignee = Human,
            Tags = ["legal"], DueOffset = 7, CreatedDaysAgo = 5, LastTouchedDaysAgo = 5,
            Body = "Confirm nothing on the page is a promise we cannot keep. See the unresolved Legal Checklist link in the Pricing Brief.",
        },

        // ---- Growth team root ----
        new()
        {
            Id = "GROW-0014", Location = new(Growth, null), Title = "Plan the Q4 launch calendar",
            Status = "In Progress", Priority = "Medium", Creator = "Owen", Assignee = "Owen",
            Tags = ["planning"], DueOffset = 20, CreatedDaysAgo = 13, LastTouchedDaysAgo = 2,
            Body = "Which launches go in which week, and which teams each launch needs.",
        },
        new()
        {
            Id = "GROW-0015", Location = new(Growth, null), Title = "Write the brand voice guide",
            Status = "Review", Priority = "Medium", Creator = "Owen", Assignee = "Maya",
            Tags = ["copy", "brand"], DueOffset = 5, CreatedDaysAgo = 21, LastTouchedDaysAgo = 1,
            Body = "One page: tone, words we use, words we avoid, and three before-and-after examples.",
        },

        // ---- Support / Help Center ----
        new()
        {
            Id = "SUPP-0001", Location = new(Support, HelpCenter), Title = "Audit the top 50 help articles",
            Status = "Done", Priority = "Medium", Creator = "Dana", Assignee = "Dana", Closed = true,
            Tags = ["content"], CreatedDaysAgo = 50, LastTouchedDaysAgo = 41,
            Body = "Each article marked keep, rewrite or delete, with reasons.",
        },
        new()
        {
            Id = "SUPP-0002", Location = new(Support, HelpCenter), Title = "Rewrite the billing FAQ",
            Status = "In Progress", Priority = "Medium", Creator = "Dana", Assignee = "Dana",
            Tags = ["content", "billing"], DueOffset = 3, CreatedDaysAgo = 12, LastTouchedDaysAgo = 1,
            Body = "The most-read article has three outdated answers. Rewrite with Ben's help on VAT.",
        },
        new()
        {
            Id = "SUPP-0003", Location = new(Support, HelpCenter), Title = "Redesign the article template",
            Status = "To Do", Priority = "Medium", Creator = "Dana", Assignee = "Sofia",
            Tags = ["design"], CreatedDaysAgo = 9, LastTouchedDaysAgo = 9,
            Body = "Clear headings, a quick-answer box at the top and related links at the bottom.",
        },
        new()
        {
            Id = "SUPP-0004", Location = new(Support, HelpCenter), Title = "Tune help search relevance",
            Status = "Backlog", Priority = "Medium", Creator = "Dana", CreatedDaysAgo = 6, LastTouchedDaysAgo = 6,
            Body = "The search returns old articles first. Nobody has picked this up yet.",
        },

        // ---- Support / Incident Tooling ----
        new()
        {
            Id = "SUPP-0005", Location = new(Support, IncidentTooling), Title = "Build the public status page",
            Status = "In Progress", Priority = "Urgent", Creator = "Dana", Assignee = "Grace",
            Tags = ["incident", "customer-facing"], DueOffset = -2, StartOffset = -12, CreatedDaysAgo = 14, LastTouchedDaysAgo = 1,
            OriginRoom = SoftwareCoRooms.IncidentWarRoom,
            Body = "During incident 0412 customers had nowhere to check. Overdue and Urgent.",
        },
        new()
        {
            Id = "SUPP-0006", Location = new(Support, IncidentTooling), Title = "Write the postmortem template",
            Status = "Done", Priority = "Medium", Creator = "Grace", Assignee = "Alan", Closed = true,
            Tags = ["incident", "process"], CreatedDaysAgo = 29, LastTouchedDaysAgo = 15,
            Body = "Template used by the incident-postmortem Skill. Alan works in Platform but owns this for Support.",
        },
        new()
        {
            Id = "SUPP-0007", Location = new(Support, IncidentTooling), Title = "Automate the pager rotation",
            Status = "To Do", Priority = "High", Creator = "Dana", Assignee = "Grace",
            Tags = ["on-call"], BlockedBy = ["SUPP-0005"], CreatedDaysAgo = 13, LastTouchedDaysAgo = 4,
            Body = "Rotation should follow the shared calendar. Blocked by the status page.",
        },
        new()
        {
            Id = "SUPP-0008", Location = new(Support, IncidentTooling), Title = "Follow up on incident 0412",
            Status = "Review", Priority = "High", Creator = "Dana", Assignee = "Dana",
            Tags = ["incident", "postmortem"], DueOffset = 4, CreatedDaysAgo = 8, LastTouchedDaysAgo = 1,
            OriginRoom = SoftwareCoRooms.IncidentWarRoom,
            Body = "Every action from the postmortem, with owners. Alan's draft is in the incident Room.",
        },

        // ---- Support team root ----
        new()
        {
            Id = "SUPP-0009", Location = new(Support, null), Title = "Clean up the support macros",
            Status = "To Do", Priority = "Low", Creator = "Dana", Assignee = "Dana",
            Tags = ["macros"], CreatedDaysAgo = 10, LastTouchedDaysAgo = 10,
            Body = "Merge the 14 near-identical refund macros into three.",
        },
        new()
        {
            Id = "SUPP-0010", Location = new(Support, null), Title = "Define the customer escalation SLA",
            Status = "Backlog", Priority = "High", Creator = Human, Assignee = Human,
            Tags = ["sla"], CreatedDaysAgo = 4, LastTouchedDaysAgo = 4,
            Body = "First response and resolution targets per priority, agreed with Dana.",
        },

        // ---- Archive team root ----
        new()
        {
            Id = "ARCH-0001", Location = new(Archive, null), Title = "Archive the 2025 support data",
            Status = "Done", Priority = "Low", Creator = "Dana", Assignee = "Dana", Closed = true,
            Tags = ["retention"], CreatedDaysAgo = 60, LastTouchedDaysAgo = 45,
            Body = "Tickets and macros from 2025 exported to cold storage and removed from the live system.",
        },
    ];
}

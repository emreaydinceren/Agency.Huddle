using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The documents and memory that bring every Team root and Project of <c>software-co</c> up to four documents, and every Team,
/// Project and Agent up to its memory floor (Seeder spec SEED-SC-13, -14). They carry no <c>#tags</c> and link only to notes
/// that exist, so the Tags panel counts and the one unresolved link pinned elsewhere stay as they are (SEED-SC-15).
/// </summary>
internal static class SoftwareCoNotes
{
    /// <summary>The added Library documents, one or more for each folder that had fewer than four.</summary>
    internal static IReadOnlyList<SeedFile> Documents { get; } =
    [
        // ---- Platform ----
        new("Teams/Platform/Release-Calendar.md", """
            # Release calendar

            Releases go out on Tuesdays and Thursdays before 15:00 UTC. Nothing ships on a Friday, see
            the [[Engineering-Handbook]].

            | Train | Contents | Owner |
            | --- | --- | --- |
            | Next Tuesday | Rate limiting per API key (PLAT-0004) | Marcus |
            | Next Thursday | Gateway tracing (PLAT-0008) | Alan |
            | In two weeks | The .NET 10 upgrade, gateway first (PLAT-0013) | Grace |

            A train that misses its slot waits for the next one; it is never squeezed in late.
            """),
        new("Teams/Platform/API v2/Rate-Limiting.md", """
            # Rate limiting

            Every API key gets a token bucket: 100 requests a second, refilled continuously, with a burst
            of 200. The burst size is configurable per key, as Grace asked in review.

            ## When a key is rotated

            The new key starts with a full bucket. The old key keeps working for ten minutes so clients can
            roll over without a gap. See [[API-v2-Design]] for the error body a throttled client gets.
            """),
        new("Teams/Platform/Observability/Alert-Routing.md", """
            # Alert routing

            An alert pages a person only when customers feel it. Everything else goes to the team channel
            and waits for working hours.

            | Signal | Pages? | Goes to |
            | --- | --- | --- |
            | p99 latency above 800 ms for 5 minutes | Yes | The on-call engineer |
            | Error rate above 2 percent | Yes | The on-call engineer |
            | Disk above 80 percent | No | The Platform Team Room |
            | Certificate expires within 14 days | No | The Platform Team Room |

            The rota itself is in `oncall-contacts.txt`; the first response steps are in the [[Runbook]].
            """),
        new("Teams/Platform/Observability/Log-Retention.md", """
            # Log retention

            - Request logs are kept for 30 days, then deleted.
            - Audit logs are kept for one year and are never sampled.
            - Traces are kept for 7 days; a trace attached to an incident is copied into the incident
              note before it expires.

            The policy is tracked in PLAT-0015 and was agreed at the [[Meeting-Notes-2026-10-14]].
            """),

        // ---- Growth ----
        new("Teams/Growth/Growth-Metrics.md", """
            # Growth metrics

            | Metric | Today | Target |
            | --- | --- | --- |
            | Signup to first project | 31 percent | 40 percent |
            | Trial to paid | 9 percent | 12 percent |
            | Week-one retention | 54 percent | 60 percent |

            The funnel behind the first row is mapped in [[Signup-Funnel]]. Each number is read from the
            events defined in [[Event-Taxonomy]] and refreshed every Monday.
            """),
        new("Teams/Growth/Q4-Launch-Calendar.md", """
            # Q4 launch calendar

            | Week | Launch | Owner |
            | --- | --- | --- |
            | 1 | Welcome checklist redesign | Sofia |
            | 3 | New pricing page | Owen |
            | 5 | Lifecycle email refresh | Maya |

            Launches freeze on the last Friday of each month. The pricing page goes out with the Q4
            campaign, not before it; see the [[Pricing-Brief]].
            """),
        new("Teams/Growth/Experiment-Log.md", """
            # Experiment log

            ## Shorter signup form

            Removed the company-size field. Signups rose 6 percent with no drop in activation. Kept.

            ## Social proof on pricing

            Logos above the plan table. No measurable change after two weeks. Removed.

            ## Checklist with progress bar

            Running now as part of GROW-0002. Results go here when it ends, and into [[Growth-Metrics]].
            """),
        new("Teams/Growth/Onboarding Revamp/Signup-Funnel.md", """
            # Signup funnel

            1. Landing page visit
            2. Account created
            3. Workspace named
            4. First project created
            5. First teammate invited

            The largest drop is between steps 2 and 4: people create an account and leave. The welcome
            checklist in the [[Onboarding-Brief]] is aimed at exactly that gap. Interview evidence is in
            [[Interview-Notes]].
            """),
        new("Teams/Growth/Onboarding Revamp/Event-Taxonomy.md", """
            # Event taxonomy

            Events are named `object_action`, in the past tense, lower case.

            | Event | Fired when |
            | --- | --- |
            | `account_created` | The signup form is submitted and verified |
            | `workspace_named` | The workspace name is saved for the first time |
            | `project_created` | Any project is created |
            | `teammate_invited` | An invitation email is sent |

            Instrumentation is GROW-0004. Lena owns the client side.
            """),
        new("Teams/Growth/Pricing Page/Plan-Comparison-Table.md", """
            # Plan comparison table

            | | Starter | Team | Business |
            | --- | --- | --- | --- |
            | Seats | Up to 3 | Up to 25 | Unlimited |
            | Projects | 5 | Unlimited | Unlimited |
            | Single sign-on | No | No | Yes |
            | Support | Email | Email and chat | A named contact |

            Prices sit in the [[Pricing-Brief]]. Every claim in this table goes through legal review
            (GROW-0013) before it is published.
            """),
        new("Teams/Growth/Lifecycle Emails/Email-Calendar.md", """
            # Email calendar

            | Day after signup | Email | Goal |
            | --- | --- | --- |
            | 0 | Welcome | Name the workspace |
            | 1 | Your first project | Create a project |
            | 3 | Bring your team | Invite a teammate |
            | 10 | What you can do next | Upgrade from trial |

            Drafts start from [[Welcome-Email-Draft]]; ideas wait in [[Email-Ideas]].
            """),
        new("Teams/Growth/Lifecycle Emails/Brand-Voice-Guide.md", """
            # Brand voice guide

            - Plain words. Write "use", not "leverage".
            - Short sentences, one idea each.
            - No exclamation marks in headlines.
            - Talk to one reader: "you", never "users".

            This guide is still a draft (GROW-0015). Maya owns it.
            """),

        // ---- Support ----
        new("Teams/Support/Macros.md", """
            # Support macros

            | Macro | Use it when |
            | --- | --- |
            | Billing: refund issued | A refund has been approved and sent |
            | Account: reset sign-in | A customer is locked out and has proved who they are |
            | Bug: logged | A bug is confirmed and a Task exists for it |
            | Incident: we are on it | The status page shows an active incident |

            Macros older than a year are reviewed in SUPP-0009. Escalations follow the
            [[Escalation-Guide]], never a macro.
            """),
        new("Teams/Support/Support-Metrics.md", """
            # Support metrics

            | Metric | Last month | Target |
            | --- | --- | --- |
            | First response, Urgent | 22 minutes | 30 minutes |
            | First response, Normal | 3.5 hours | 4 hours |
            | Resolved in one reply | 41 percent | 50 percent |
            | Satisfaction | 92 percent | 90 percent |

            Recurring themes behind the numbers are in the [[Customer-Feedback-Log]].
            """),
        new("Teams/Support/Help Center/Article-Template.md", """
            # Help article template

            ## Title

            A task, phrased the way a customer would search for it: "Export your data", not "Data export".

            ## Before you start

            What the customer needs first, in one or two lines.

            ## Steps

            Numbered, one action per step, with the exact button names.

            ## If it did not work

            The two most common failures and what to do about each. Follow the [[Style-Guide]].
            """),
        new("Teams/Support/Help Center/Top-50-Audit.md", """
            # Top 50 articles audit

            Progress on SUPP-0001.

            | Status | Articles |
            | --- | --- |
            | Current | 31 |
            | Needs new screenshots | 12 |
            | Wrong since the billing change | 5 |
            | Should be deleted | 2 |

            The five billing articles are rewritten first, because they drive the most tickets. The
            rewrites follow the [[Article-Template]].
            """),
        new("Teams/Support/Incident Tooling/Status-Page-Plan.md", """
            # Status page plan

            The public status page (SUPP-0005) shows four components: the app, the API, sign-in and email
            delivery. Each is Operational, Degraded or Down.

            - An incident is posted within 15 minutes of the first page.
            - Updates go out every 30 minutes until it is resolved.
            - The last update links the postmortem.

            Lessons from [[Incident-0412]] shaped every rule above.
            """),
        new("Teams/Support/Incident Tooling/Pager-Rotation.md", """
            # Pager rotation

            One engineer is on call for a week, Monday 09:00 to Monday 09:00 UTC, with a second as backup.
            Handover happens in the Platform Team Room and lists open alerts and promises made to customers.

            Automating the rota is SUPP-0007. It waits on the status page, because both use the same
            incident list.
            """),
        new("Teams/Support/Incident Tooling/Postmortem-Template.md", """
            # Postmortem template

            ## Summary

            What happened, in two sentences a customer could read.

            ## Impact

            Who was affected, for how long, and how we know.

            ## Timeline (UTC)

            ## Cause

            The conditions that made it possible, not the person who triggered it.

            ## Follow-ups

            Each one a Task with an owner. See [[Incident-0412]] for a finished example.
            """),

        // ---- Archive ----
        new("Teams/Archive/Retention-Schedule.md", """
            # Retention schedule

            | Data | Kept for | Then |
            | --- | --- | --- |
            | Closed support tickets | 2 years | Deleted |
            | Call recordings | 90 days | Deleted |
            | Invoices | 7 years | Moved to cold storage |
            | Product analytics | 25 months | Aggregated, then deleted |
            """),
        new("Teams/Archive/Support-Export-2025.md", """
            # 2025 support export

            The 2025 tickets are exported for ARCH-0001 before they are archived. The export holds ticket
            text, tags and timestamps; attachments stay behind and are deleted on schedule.

            See the [[Retention-Schedule]] for how long each part is kept.
            """),
        new("Teams/Archive/Archive-Index.md", """
            # Archive index

            - [[Support-Export-2025]]: last year's support tickets.
            - [[Retention-Schedule]]: what is kept and for how long.
            - [[Handover-Checklist]]: what to check before a folder moves here.
            """),
        new("Teams/Archive/Handover-Checklist.md", """
            # Handover checklist

            1. Every open Task in the folder is closed or moved.
            2. Personal data is removed or listed in the [[Retention-Schedule]].
            3. Links from active notes are updated.
            4. The owning Team agrees in writing that nothing else needs to move.
            """),
    ];

    /// <summary>The added memory facts: Team, Project and Agent memory, one line each.</summary>
    internal static IReadOnlyList<SeedFile> Memory { get; } =
    [
        // ---- Team memory ----
        new("Teams/Platform/memory/release-days.md", "Releases ship on Tuesdays and Thursdays before 15:00 UTC.\n"),
        new("Teams/Growth/memory/experiment-length.md", "An experiment runs for at least two full weeks before anyone reads the result.\n"),
        new("Teams/Support/memory/escalation-owner.md", "Dana owns every customer escalation until it is handed to an engineer by name.\n"),
        new("Teams/Support/memory/macro-review-yearly.md", "Every support macro is reviewed at least once a year.\n"),
        new("Teams/Archive/memory/retention-before-archive.md", "Nothing moves into Archive until its retention period is written down.\n"),
        new("Teams/Archive/memory/exports-keep-no-attachments.md", "Archive exports keep ticket text and timestamps, never attachments.\n"),
        new("Teams/Archive/memory/archive-is-read-only.md", "Archived notes are read-only; a change starts a new note in the active Team.\n"),

        // ---- Project memory ----
        new("Teams/Platform/Observability/memory/page-only-on-customer-impact.md", "An alert pages someone only when customers feel the problem.\n"),
        new("Teams/Growth/Onboarding Revamp/memory/event-naming.md", "Analytics events are named object_action, past tense, lower case.\n"),
        new("Teams/Growth/Lifecycle Emails/memory/send-window.md", "Lifecycle emails are sent between 09:00 and 11:00 in the reader's time zone.\n"),
        new("Teams/Support/Help Center/memory/task-titles.md", "Help article titles are tasks a customer would search for, such as Export your data.\n"),
        new("Teams/Support/Incident Tooling/memory/status-update-cadence.md", "During an incident the status page is updated every 30 minutes.\n"),

        // ---- Agent personal memory ----
        new("Agents/Grace/work/memory/review-before-friday.md", "Architecture reviews are finished by Thursday so nothing waits over a weekend.\n"),
        new("Agents/Marcus/work/memory/cursor-is-opaque.md", "Pagination cursors are opaque to clients; never document their format.\n"),
        new("Agents/Marcus/work/memory/prefers-integration-tests.md", "Marcus proves an endpoint with an integration test before asking for review.\n"),
        new("Agents/Lena/work/memory/accessibility-first.md", "Every new component is checked with a keyboard and a screen reader before review.\n"),
        new("Agents/Lena/work/memory/analytics-owner.md", "Lena owns the client-side analytics events for onboarding.\n"),
        new("Agents/Owen/work/memory/decision-question.md", "Owen ends a proposal with the question: what would make you say yes?\n"),
        new("Agents/Owen/work/memory/conversion-baseline.md", "The signup conversion baseline is 31 percent, measured in the first week of October.\n"),
        new("Agents/Sofia/work/memory/design-tokens.md", "New screens use the design tokens; no colour is typed by hand.\n"),
        new("Agents/Sofia/work/memory/layout-review-day.md", "Sofia reviews layouts on Wednesday mornings.\n"),
        new("Agents/Dana/work/memory/urgent-means-thirty-minutes.md", "An Urgent ticket gets a first response within 30 minutes, day or night.\n"),
        new("Agents/Dana/work/memory/escalate-with-ticket.md", "An escalation always links the original ticket and the customer's own words.\n"),
        new("Agents/Alan/work/memory/rollback-first.md", "During an incident, roll back first and investigate second.\n"),
        new("Agents/Alan/work/memory/postmortem-within-five-days.md", "A postmortem is published within five working days of an incident.\n"),
        new("Agents/Maya/work/memory/no-superlatives.md", "Launch copy avoids superlatives such as best, fastest or easiest.\n"),
        new("Agents/Ben/work/memory/month-end-close.md", "The books close on the third working day of each month.\n"),
    ];
}

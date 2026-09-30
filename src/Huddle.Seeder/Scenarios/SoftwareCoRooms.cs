using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>The chat Rooms and their history for the <c>software-co</c> scenario.</summary>
internal static class SoftwareCoRooms
{
    /// <summary>Key of the one-to-one Room with Grace.</summary>
    internal const string DmGrace = "dm-grace";

    /// <summary>Key of the one-to-one Room with Owen.</summary>
    internal const string DmOwen = "dm-owen";

    /// <summary>Key of the Platform group Room.</summary>
    internal const string PlatformStandup = "platform-standup";

    /// <summary>Key of the Growth group Room.</summary>
    internal const string LaunchCrew = "launch-crew";

    /// <summary>Key of the archived Room.</summary>
    internal const string Q2Retro = "q2-retro";

    /// <summary>Key of the custom-named incident Room.</summary>
    internal const string IncidentWarRoom = "incident-0412";

    private const string Human = "You";

    /// <summary>All six Rooms.</summary>
    internal static IReadOnlyList<SeedRoom> All { get; } =
    [
        new(DmGrace, "Grace", ["Grace"], false,
        [
            new(Human, 12, "09:05", "Morning Grace. Where are we on the .NET 10 upgrade?"),
            new("Grace", 12, "09:07", "Shared libraries are done. Gateway is next, and I want it on staging for a full day before we touch billing. Tracking it in PLAT-0013."),
            new(Human, 12, "09:09", "Good. Anything blocking?"),
            new("Grace", 12, "09:10", "One thing: the build farm still has the old SDK image. Alan is clearing it this week. Nothing else."),
            new(Human, 8, "14:30", "Can you look at the rate limiting change? PLAT-0004 is waiting on a review."),
            new("Grace", 8, "14:33", "On it. Marcus's token-bucket approach is sound; I'll check the burst settings and approve today."),
            new("Grace", 8, "16:12", "Approved with one comment: make the burst size configurable per key. Marcus has the note."),
            new(Human, 3, "10:00", "Please take the status page, SUPP-0005. Dana needs it before the next incident."),
            new("Grace", 3, "10:04", "Understood. It is overdue and Urgent, so it goes first. I'll ask Sofia for the layout today."),
            new(Human, 1, "08:45", "Thanks Grace. Can you also own the pager rotation once the status page ships? SUPP-0007."),
            new("Grace", 1, "08:49", "Yes. It is already assigned to me and marked blocked by SUPP-0005, so it follows naturally."),
        ]),
        new(DmOwen, "Owen", ["Owen"], false,
        [
            new(Human, 10, "11:00", "Owen, what is the current signup conversion?"),
            new("Owen", 10, "11:02", "31 percent, against a 40 percent target. The biggest drop is between account creation and the first project. Question: do we agree the checklist is the fix?"),
            new(Human, 10, "11:05", "Yes. Push it as the top priority."),
            new("Owen", 10, "11:06", "Done. GROW-0002 is High and with Sofia; GROW-0004 gives us the events to measure it."),
            new(Human, 6, "15:20", "I need to review the pricing tiers before Friday."),
            new("Owen", 6, "15:22", "They are in Review: GROW-0009. Three tiers, prices on the Pricing Brief. What would make you say yes?"),
            new(Human, 6, "15:25", "Show me the competitor comparison next to it."),
            new("Owen", 6, "15:27", "It is in the Pricing Research note. I will link both from the Task."),
            new(Human, 2, "09:30", "Legal review of the price claims is mine, GROW-0013. I'll do it by next week."),
            new("Owen", 2, "09:31", "Thank you. Question: should the launch wait for it, or run in parallel?"),
        ]),
        new(PlatformStandup, "Platform standup", ["Grace", "Marcus", "Alan"], false,
        [
            new(Human, 5, "09:00", "Stand-up. Two lines each please: done, doing, blocked."),
            new("Grace", 5, "09:01", "Done: reviewed the rate limiter. Doing: .NET 10 gateway. Blocked: nothing."),
            new("Marcus", 5, "09:02", "Done: cursor format. Doing: pagination on the list endpoints, PLAT-0002. Blocked: nothing, but it is overdue."),
            new("Alan", 5, "09:03", "Done: tracing spans on the gateway. Doing: alert routing, PLAT-0010. Blocked: nothing."),
            new(Human, 5, "09:04", "@marcus what do you need to finish PLAT-0002?"),
            new("Marcus", 5, "09:05", "Two more days and one answer: is the maximum page size 200? I need it to write the tests."),
            new("Grace", 5, "09:06", "Yes, 200. It is in the Pagination note. @marcus please cite the note in the change."),
            new("Marcus", 5, "09:07", "Will do."),
            new("Alan", 5, "09:08", "For the record, PLAT-0008 needs a 10 percent sample rate. I have set it and documented it in the Runbook."),
            new(Human, 5, "09:09", "Thanks everyone. Meeting closed."),
        ]),
        new(LaunchCrew, "Launch crew", ["Owen", "Lena", "Sofia", "Maya"], false,
        [
            new(Human, 9, "10:00", "Kick-off for the onboarding and pricing launch. Owen, you lead."),
            new("Owen", 9, "10:02", "Thanks. Two workstreams: onboarding (GROW-0002, GROW-0004) and pricing (GROW-0009 to GROW-0012). Target is 40 percent conversion."),
            new("Sofia", 9, "10:04", "I would like to start with the welcome checklist. Can I see how people drop off today first?"),
            new("Owen", 9, "10:05", "It is in the Onboarding Brief. @sofia the drop is after account creation."),
            new("Lena", 9, "10:07", "For the events, GROW-0004: I need the exact step names. @owen can you send them?"),
            new("Owen", 9, "10:08", "Sending now. @lena please keep them stable; the dashboards depend on them."),
            new("Maya", 9, "10:10", "@owen for the copy, is the tone friendly-plain like the brand voice guide? I can have two headline options for each tier."),
            new("Owen", 9, "10:11", "Yes please, @maya. GROW-0011 has a due date; the tooltips in GROW-0005 come first."),
            new("Sofia", 4, "13:40", "Checklist mockups are up. @lena the empty states are the tricky part; see GROW-0003."),
            new("Lena", 4, "13:45", "Looks great. I will not start until the checklist is final, so GROW-0003 is blocked on GROW-0002 for now."),
            new("Owen", 4, "13:50", "Agreed. @sofia the comparison table (GROW-0010) is overdue and waits for the tiers, GROW-0009. I will finish that this week."),
            new("Maya", 4, "13:55", "Copy for the tooltips is ready for review. One option is playful, the other plain. Your call, @owen."),
        ]),
        new(Q2Retro, "Q2 retro", ["Grace", "Dana"], true,
        [
            new(Human, 90, "15:00", "Closing out Q2. What went well?"),
            new("Grace", 90, "15:02", "The API v2 resource model shipped on time. Fewer surprises than any previous release."),
            new("Dana", 90, "15:03", "Support volume fell 18 percent after the billing FAQ rewrite. Customers found answers themselves."),
            new(Human, 90, "15:05", "And what should we change?"),
            new("Dana", 90, "15:06", "We had no status page. Customers wrote in while we were fixing the problem. Please make it a Q3 goal."),
            new("Grace", 90, "15:08", "Agreed. And on-call needs an automated rotation; the spreadsheet is a single point of failure."),
            new("Dana", 90, "15:09", "Also archive the old ticket data. ARCH-0001 covers it."),
            new(Human, 90, "15:12", "Noted. I will archive this Room; we can still read it."),
        ]),
        new(IncidentWarRoom, "Incident 0412 war room", ["Alan", "Dana", "Marcus"], false,
        [
            new("Alan", 11, "09:03", "Alert firing on the reports service: error rate 38 percent. Investigating."),
            new("Dana", 11, "09:05", "Three customers have written in already. Impact: reports fail to load. No data loss reported."),
            new(Human, 11, "09:07", "@alan what is the cause?"),
            new("Alan", 11, "09:12", "Connection pool exhausted after the 08:40 deploy. The limit was set to 10 instead of 100."),
            new("Marcus", 11, "09:15", "Confirmed in the deploy config. I can raise it now; rollback would take longer."),
            new(Human, 11, "09:16", "Do it, @marcus."),
            new("Marcus", 11, "09:20", "Pool limit is 100. Watching the error rate."),
            new("Alan", 11, "09:49", "Error rate back to baseline. Incident resolved at 09:49 UTC. Total duration: 47 minutes."),
            new("Dana", 11, "09:52", "I will message the three customers and open the follow-ups: SUPP-0008."),
            new("Alan", 10, "10:30", "Postmortem draft is in my working folder. I will share it here when it is reviewed."),
            new("Dana", 10, "10:35", "Important follow-ups: SUPP-0005 status page and SUPP-0007 pager rotation."),
            new(Human, 10, "10:40", "Agreed. Thank you all for the fast response."),
        ]),
    ];
}

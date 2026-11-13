using System.Globalization;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The Jobs of <c>software-co</c> (Jobs spec): every timetable preset, Jobs for Agents and for the Human, Jobs scheduled by
/// their owner, by the Human, by a manager for a member of its Team and by the Chief of Staff, a paused one, three time zones,
/// Job Rooms with past runs (one archived, one whose last run found the Budget spent), and two sections Huddle cannot read.
/// Thirteen Jobs, so the sidebar's Show all appears. Seeder spec SEED-SC-19.
/// </summary>
internal static class SoftwareCoJobs
{
    /// <summary>The Agent whose last run found its Job Room's Budget spent.</summary>
    internal const string BudgetSpentJob = "job-4b18e2";

    /// <summary>The paused Job whose Room is archived.</summary>
    internal const string ArchivedRoomJob = "job-c27d90";

    private const string Human = "You";
    private const string ChiefOfStaff = "Chief of Staff";

    /// <summary>The Jobs, measured from <paramref name="today"/> so the one-off is always ahead.</summary>
    /// <param name="today">The run date.</param>
    /// <returns>The Jobs in the order their files list them.</returns>
    internal static IReadOnlyList<SeedJob> All(DateOnly today) =>
    [
        .. Work,
        new("job-08c4ab", "Owen", "Pricing experiment readout", Once(today.AddDays(5), "10:00"), "Read out the pricing experiment: conversion by plan, and whether to keep the new annual discount.")
        {
            By = Human,
        },
        .. Later,
    ];

    /// <summary>Sections Huddle cannot read: a timetable that is no preset, and a field that is no Job field.</summary>
    internal static IReadOnlyList<SeedJobProblem> Problems { get; } =
    [
        new("Dana", """
            ## Fortnightly SLA report
            - id: job-71a0f9
            - when: fortnightly at 09:00
            - by: Dana

            Summarise the fortnight's SLA breaches for the Support Team.
            """),
        new("Theo", """
            ## Pantry stock-take
            - id: job-c0e8b5
            - when: monthly on 1 at 10:00
            - remind: yes

            Count what is left in the pantry and freezer before the month's big shop.
            """),
    ];

    /// <summary>Platform's and Records' Jobs: two with Job Rooms, the last of them finding the Budget spent.</summary>
    private static IReadOnlyList<SeedJob> Work { get; } =
    [
        new("job-a1f3c0", "Alan", "On-call handover", "daily at 09:00", "Post the on-call handover: open alerts, anything paged overnight, and who holds the pager next.")
        {
            TimeZone = "UTC",
            By = Human,
            Runs =
            [
                new(3, "09:00", [new("Alan", 3, "09:04", "Quiet night. One disk-space warning on the log host, cleared by rotation. Pager stays with me.")]),
                new(2, "09:00", [new("Alan", 2, "09:05", "Paged at 02:40 for API latency; a slow query, fixed by a missing index. Marcus is writing it up.")]),
                new(1, "09:00", [new("Alan", 1, "09:03", "No pages. Two alerts tuned down after yesterday. Pager passes to Grace at 18:00.")]),
            ],
        },
        new(BudgetSpentJob, "Marcus", "API error-rate check", "every 4 hours", "Check the API's 5xx rate over the last four hours and say whether anything needs a look.")
        {
            By = "Marcus",
            Runs =
            [
                new(1, "13:00", [new("Marcus", 1, "13:02", "0.04% 5xx, all from one client retrying a bad token. Nothing to do.")]),
                new(1, "17:00", [new("Marcus", 1, "17:03", "0.31% 5xx after the deploy; rolling back the pagination change and watching.")]),
                new(1, "21:00", []),
            ],
            LastOutcome = JobOutcome.BudgetSpent,
        },
        new("job-5e09d7", "Grace", "Release go/no-go", "weekly on tue,thu at 15:00", "Collect the release checklist and say go or no-go for today's release, with the reason.")
        {
            By = "Grace",
        },
        new("job-d3a6f1", "Ben", "Month-end reconciliation", "monthly on 31 at 17:00", "Reconcile this month's invoices against the bank feed and list anything unmatched.")
        {
            By = Human,
        },
    ];

    /// <summary>The rest of the company, the household and the Human, after the one-off.</summary>
    private static IReadOnlyList<SeedJob> Later { get; } =
    [
        new(ArchivedRoomJob, "Dana", "SLA breach sweep", "daily at 08:30", "List every open ticket past its first-response SLA, oldest first, with who holds it.")
        {
            TimeZone = "America/New_York",
            By = "Dana",
            Paused = true,
            Runs = [new(9, "13:30", [new("Dana", 9, "13:36", "Two tickets past SLA, both billing questions waiting on Ben. Pausing this until the new macros are in.")])],
            RoomArchived = true,
        },
        new("job-6f2e15", "Maya", "Newsletter draft", "weekly on fri at 11:00", "Draft next week's customer newsletter from the week's launches and help-centre changes.")
        {
            By = Human,
        },
        new("job-e4b7a3", "Hattie", "Sunday preview", "weekly on sun at 18:00", "Post the week ahead for the family: what is due, who owns it, and anything waiting on someone.")
        {
            TimeZone = "Europe/London",
            By = "Hattie",
        },
        new("job-9a50c8", "Theo", "Grocery list", "weekly on wed at 17:00", "Turn this week's meal plan into the grocery list, and check the pantry first.")
        {
            TimeZone = "Europe/London",
            By = "Hattie",
        },
        new("job-27d1e6", "Ravi", "Seasonal maintenance check", "monthly on 1 at 09:00", "Check the maintenance calendar for this month and turn each job that falls due into a Task.")
        {
            By = "Ravi",
        },
        new("job-b83f4d", "Iris", "Renewal reminders", "weekdays at 07:00", "Check renewals due in the next 30 days and remind whoever owns each one.")
        {
            TimeZone = "Europe/London",
            By = "Iris",
        },
        new("job-f15c62", null, "Daily plan", "daily at 07:30", "Pick the three things that matter today, and clear anything waiting on you first.")
        {
            TimeZone = "UTC",
            By = Human,
            Runs = [new(1, "07:30", [new(Human, 1, "08:10", "Done: the pricing sign-off, the boiler quote, and the insurance call.")])],
        },
        new("job-3c9e07", null, "Weekly review", "weekly on sun at 19:00", "Look back at the week: what shipped, what slipped, and what to drop next week.")
        {
            By = ChiefOfStaff,
        },
    ];

    private static string Once(DateOnly on, string at) =>
        string.Create(CultureInfo.InvariantCulture, $"once on {on:yyyy-MM-dd} at {at}");
}

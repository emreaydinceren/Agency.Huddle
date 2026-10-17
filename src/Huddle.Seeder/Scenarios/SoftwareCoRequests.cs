using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// Requests between Teams for the <c>software-co</c> scenario: Tasks one Team filed with another. One per triage state
/// (untriaged, needs-info, accepted, declined) and one that is Done but not yet confirmed by the requesting Team's QA.
/// </summary>
internal static class SoftwareCoRequests
{
    private const string Platform = "Platform";
    private const string Growth = "Growth";
    private const string Support = "Support";

    /// <summary>The five Requests.</summary>
    internal static IReadOnlyList<SeedTask> All { get; } =
    [
        new()
        {
            Id = "PLAT-0016", Location = new(Platform, null), Title = "Rate limiter rejects valid keys right after rotation",
            Status = "Backlog", Priority = "High", Creator = "Owen", Tags = ["request", "bug"],
            CreatedDaysAgo = 6, LastTouchedDaysAgo = 6,
            Request = new("bug", Growth, "Owen", string.Empty),
            Body = """
                ## Steps to reproduce

                1. Rotate an API key in the dashboard.
                2. Call any list endpoint with the new key within a minute.

                ## Expected and actual

                Expected: the call succeeds. Actual: it returns 429 for about a minute, which breaks our signup emails.
                """,
        },
        new()
        {
            Id = "PLAT-0017", Location = new(Platform, null), Title = "Webhook for finished exports",
            Status = "Backlog", Priority = "Medium", Creator = "Dana", Tags = ["request", "feature"],
            CreatedDaysAgo = 9, LastTouchedDaysAgo = 4,
            Request = new("feature", Support, "Dana", "needs-info"),
            Body = """
                ## Problem

                Support waits on large exports and has no way to know when one finishes.

                ## Desired outcome

                A webhook or notification when an export is ready. Which formats must it cover? Platform asked for more detail.
                """,
            ExtraLog = [new("Grace", "triage edited")],
        },
        new()
        {
            Id = "SUPP-0011", Location = new(Support, null), Title = "How long do we keep incident recordings?",
            Status = "To Do", Priority = "Low", Creator = "Grace", Assignee = "Dana", Tags = ["request", "question"],
            CreatedDaysAgo = 8, LastTouchedDaysAgo = 2,
            Request = new("question", Platform, "Grace", "accepted"),
            Body = """
                ## Question

                Platform wants to know how long Support keeps incident call recordings, so the postmortem template can say so.
                """,
            ExtraLog = [new("Dana", "triage edited")],
        },
        new()
        {
            Id = "GROW-0016", Location = new(Growth, null), Title = "Rewrite every onboarding email in the help centre tone",
            Status = "Rejected", Priority = "Low", Creator = "Dana", Closed = true, Tags = ["request", "feedback"],
            CreatedDaysAgo = 20, LastTouchedDaysAgo = 12,
            Request = new("feedback", Support, "Dana", "declined", "Onboarding emails follow the brand voice guide, not the help centre style."),
            Body = """
                ## What you saw

                The onboarding emails read differently from the help centre articles. Customers have mentioned it twice.
                """,
            ExtraLog = [new("Owen", "triage edited")],
        },
        new()
        {
            Id = "PLAT-0018", Location = new(Platform, null), Title = "Add a request id header to every API response",
            Status = "Done", Priority = "Medium", Creator = "Maya", Assignee = "Marcus", Tags = ["request", "feature"],
            CreatedDaysAgo = 25, LastTouchedDaysAgo = 3,
            Request = new("feature", Growth, "Maya", "accepted"),
            Body = """
                ## Problem

                When a customer reports an error we cannot match it to a request in the logs.

                ## Desired outcome

                Every response carries a request id header. Done by Platform; Growth QA has not confirmed it yet, so the Request stays open.
                """,
            ExtraLog = [new("Grace", "triage edited")],
        },
    ];
}

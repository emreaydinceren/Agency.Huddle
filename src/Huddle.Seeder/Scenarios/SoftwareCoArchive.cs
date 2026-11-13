using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// What <c>software-co</c> has already archived (Seeder spec SEED-SC-16 to -18), so the Archived page's Teams, Projects and
/// Chats tabs each have something to restore: the Team Legacy Site with its two Projects, Growth's Project Spring Promo, and
/// four archived Team chats. Everything is written as the app's <c>ArchiveService</c> leaves it, never through it.
/// </summary>
/// <remarks>
/// No Tasks sit in an archived folder: a Task restored with a Team or Project would carry an id the live allocator never
/// counted. No Feed item says that anything was archived, because the Feed's counts are pinned (SEED-SC-11).
/// </remarks>
internal static class SoftwareCoArchive
{
    /// <summary>The archived Team.</summary>
    internal const string LegacySite = "Legacy Site";

    /// <summary>The Project of <see cref="LegacySite"/> one of its chats belongs to.</summary>
    internal const string CheckoutV1 = "Checkout v1";

    /// <summary>Growth's archived Project.</summary>
    internal const string SpringPromo = "Spring Promo";

    /// <summary>Key of Legacy Site's Team Room.</summary>
    internal const string TeamLegacySite = "team-legacy-site";

    /// <summary>Key of Legacy Site's chat in its Checkout v1 Project.</summary>
    internal const string ChatLegacyCheckout = "chat-legacy-checkout";

    /// <summary>Key of Platform's archived chat in its live Observability Project.</summary>
    internal const string ChatPlatformLogSpike = "chat-platform-log-spike";

    /// <summary>Key of Growth's archived chat in its archived Spring Promo Project.</summary>
    internal const string ChatGrowthSpringPromo = "chat-growth-spring-promo";

    private const string Human = "You";

    /// <summary>
    /// The archived Team: blank, two Projects, three members who are all still in live Teams. The roster is in ordinal order,
    /// as <c>ArchiveService</c> writes it.
    /// </summary>
    internal static IReadOnlyList<SeedArchivedTeam> Teams { get; } =
    [
        new(LegacySite, [CheckoutV1, "Old Marketing Site"], ["Lena", "Owen", "Sofia"]),
    ];

    /// <summary>The archived notes: two at each archived Team root and Project, and a memory fact at the Team root, in Checkout v1 and in Spring Promo.</summary>
    internal static IReadOnlyList<SeedFile> Files { get; } =
    [
        new("Archive/Legacy Site/Retirement-Plan.md", """
            # Retirement plan

            The old website and its checkout were retired once the new pricing page and the Stripe checkout had run for a
            full month without a rollback.

            1. Freeze content on the old site.
            2. Point every old URL at its new page (see the redirect map in Old Marketing Site).
            3. Move the last subscriptions off Checkout v1.
            4. Archive this Team. Owen, Sofia and Lena carry on in Growth and Platform.
            """),
        new("Archive/Legacy Site/Site-Inventory.md", """
            # Site inventory

            | Area | Pages | Owner when retired |
            | --- | --- | --- |
            | Marketing pages | 42 | Owen |
            | Blog | 118 | Owen |
            | Checkout | 6 | Sofia |
            | Help pages | 23 | moved to the Help Center |
            """),
        new("Archive/Legacy Site/memory/redirects-stay-a-year.md", "Redirects from the old site stay in place for at least a year after retirement.\n"),

        new("Archive/Legacy Site/Checkout v1/Payment-Flow.md", """
            # Payment flow (v1)

            Cart, address, card, confirm: four steps, each a full page load. The card step posted straight to the old
            payment gateway, which is why the new checkout replaced it rather than fixed it.
            """),
        new("Archive/Legacy Site/Checkout v1/Sunset-Checklist.md", """
            # Sunset checklist

            - [x] Last new subscription taken on v1
            - [x] Remaining subscriptions migrated
            - [x] Gateway credentials revoked
            - [x] Refund window closed
            """),
        new("Archive/Legacy Site/Checkout v1/memory/no-new-payment-methods.md", "Checkout v1 took no new payment methods after its sunset was announced.\n"),

        new("Archive/Legacy Site/Old Marketing Site/Redirect-Map.md", """
            # Redirect map

            | Old path | New page |
            | --- | --- |
            | /pricing.html | /pricing |
            | /features/* | /product |
            | /blog/* | /blog/* (same slug) |
            | /support/* | Help Center search |
            """),
        new("Archive/Legacy Site/Old Marketing Site/Content-Freeze.md", """
            # Content freeze

            From the freeze date nothing new was published on the old site. Fixes for broken links were the one exception,
            and each was copied to the new site the same day.
            """),

        new("Archive/Growth/Spring Promo/Promo-Brief.md", """
            # Spring promo brief

            Twenty percent off the first three months of the Team plan, for six weeks in spring. Paid search and the
            newsletter only; no discounts on annual plans.
            """),
        new("Archive/Growth/Spring Promo/Results.md", """
            # Results

            Sign-ups rose 18 percent against the same weeks last year. Three-month retention of promo customers matched
            full-price customers, so the discount did not attract people who leave early.
            """),
        new("Archive/Growth/Spring Promo/memory/discount-cap.md", "A launch discount never goes above twenty percent or past three months.\n"),
    ];

    /// <summary>The four archived Team chats; each has 8 to 15 Messages, as every seeded Room does (SEED-SC-9).</summary>
    internal static IReadOnlyList<SeedRoom> Rooms { get; } =
    [
        new(TeamLegacySite, LegacySite, ["Owen", "Sofia", "Lena"], true,
        [
            new("Owen", 64, "09:00", "Content freeze starts today. Nothing new goes on the old site from now on."),
            new("Sofia", 64, "09:04", "Checkout v1 stays open until the last subscriptions move. I'll post the count each Monday."),
            new("Lena", 64, "09:10", "I'll keep the old templates building until the redirects are live."),
            new(Human, 63, "10:00", "Thanks all. What is the earliest we could switch the old site off?"),
            new("Owen", 63, "10:05", "Once every old URL has a redirect. The map is about half done."),
            new("Sofia", 57, "09:00", "Subscriptions left on v1: 41."),
            new("Sofia", 50, "09:00", "Subscriptions left on v1: 6. The rest move this week."),
            new("Lena", 47, "16:20", "Redirects are live. The old site now only answers with redirects."),
            new(Human, 46, "09:30", "Great work. Let's archive the Team once checkout is closed."),
        ], Team: LegacySite, IsTeamRoom: true),
        new(ChatLegacyCheckout, "Checkout v1 sunset", ["Sofia", "Owen"], true,
        [
            new("Sofia", 52, "11:00", "Plan for the last week of Checkout v1: stop new sign-ups, migrate, revoke the gateway keys."),
            new("Owen", 52, "11:03", "Is the notice to customers out?"),
            new("Sofia", 52, "11:05", "Sent on Monday. Two replies, both just thanks."),
            new(Human, 51, "14:00", "Any customer we cannot migrate automatically?"),
            new("Sofia", 51, "14:04", "Three, all on invoices. I'll move them by hand."),
            new("Sofia", 48, "10:00", "All three moved. Checkout v1 has no live subscriptions."),
            new("Owen", 48, "10:02", "Then I'll take the checkout pages down with the next redirect batch."),
            new("Sofia", 47, "09:15", "Gateway keys revoked. The sunset checklist is complete."),
        ], Team: LegacySite, Project: CheckoutV1),
        new(ChatPlatformLogSpike, "Log shipping spike", ["Grace", "Alan", "Marcus"], true,
        [
            new("Alan", 34, "08:10", "Log volume tripled overnight. The shipper is dropping lines from the API pods."),
            new("Grace", 34, "08:12", "Customer impact?"),
            new("Alan", 34, "08:15", "None yet. It's debug logging left on after yesterday's deploy."),
            new("Marcus", 34, "08:20", "That's mine. Turning it off now."),
            new("Marcus", 34, "08:31", "Off. Volume is back to normal."),
            new(Human, 34, "09:00", "Can we stop that happening again?"),
            new("Alan", 34, "09:05", "I'll add an alert on log volume per service, and a deploy check that refuses debug level in production."),
            new("Grace", 33, "10:00", "Both are in. Archiving this chat; the follow-up lives in Observability."),
        ], Team: "Platform", Project: "Observability"),
        new(ChatGrowthSpringPromo, "Spring promo wrap-up", ["Owen", "Maya"], true,
        [
            new("Maya", 40, "10:00", "The spring promo ended last night. Final numbers tomorrow."),
            new("Owen", 39, "10:00", "Sign-ups up 18 percent on last year. Retention looks the same as full price so far."),
            new(Human, 39, "10:15", "Did the newsletter or paid search do more?"),
            new("Maya", 39, "10:20", "Newsletter, by a wide margin. Paid search barely paid for itself."),
            new("Owen", 39, "10:25", "Then next time: newsletter first, and a smaller search budget."),
            new(Human, 39, "10:30", "Agreed. Write it up and archive the Project."),
            new("Maya", 38, "16:00", "Results are written up in the Project."),
            new("Owen", 38, "16:05", "Archiving Spring Promo now."),
        ], Team: "Growth", Project: SpringPromo),
    ];
}

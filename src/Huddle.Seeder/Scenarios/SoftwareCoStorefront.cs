using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The Storefront Team of <c>software-co</c>: an agent development team for an online shop, set up as if it had been hired from the
/// built-in <c>development</c> team type (default process: lightweight flow), with a workspace repository, a Team Room that shows the
/// flow from a request to a merge waiting for the Human, and Tasks that carry Evidence, an Action and a blocker. Seeder spec §9A
/// (SEED-DEV-1 to -8). Its Library and memory are in <see cref="SoftwareCoStorefrontNotes"/>.
/// </summary>
internal static class SoftwareCoStorefront
{
    /// <summary>The Team's name; its Task prefix is <c>STOR</c>.</summary>
    internal const string Team = "Storefront";

    /// <summary>The Team Room.</summary>
    internal const string TeamRoom = "team-storefront";

    /// <summary>The Task whose Proof is complete and whose merge waits for the Human's Accept (Needs You).</summary>
    internal const string MergeTask = "STOR-0001";

    /// <summary>The QA Task that carries the Evidence lines.</summary>
    internal const string EvidenceTask = "STOR-0003";

    /// <summary>The Task that is blocked by another.</summary>
    internal const string BlockedTask = "STOR-0006";

    /// <summary>The branch the Needs You Task's Action merges; named after the Task id, as the <c>accept-before-merge</c> gate asks.</summary>
    internal const string MergeBranch = "feat/STOR-0001-coupon-field";

    private const string Human = "You";
    private const string Checkout = "Checkout";
    private const string Accessibility = "Accessibility";
    private const string Priya = "Priya";
    private const string Jonas = "Jonas";
    private const string Kenji = "Kenji";
    private const string Amara = "Amara";
    private const string Rhys = "Rhys";
    private const string Noor = "Noor";
    private const string Development = "development/";
    private const string Version = "@1.0.0";

    /// <summary>The Team: typed <c>development@1.0.0</c>, managed by Priya, with a repository as its workspace (SEED-DEV-1, -2, -6).</summary>
    internal static SeedTeam Definition { get; } = new(Team, [Checkout, Accessibility])
    {
        TeamType = "development@1.0.0",
        Manager = Priya,
        Owns = "The online shop's web app: checkout and payments, and making the storefront accessible, built and shipped from one repository.",
        Workspace = "storefront",
        WorkspaceFiles = SoftwareCoStorefrontNotes.WorkspaceFiles,
    };

    /// <summary>The six development Agents, one in each role the Team hires (SEED-DEV-2, §9A.2).</summary>
    internal static IReadOnlyList<SeedAgent> Agents { get; } =
    [
        new(
            Priya,
            "Engineering Manager",
            "breaking requests into Tasks, assigning work, moving finished Tasks to Done",
            [Team],
            [],
            """
            You are Priya, the engineering manager of the Storefront Team.

            Voice: clear and brief. You state the acceptance line first, then who owns the Task.

            How you work:
            - You turn every request into Tasks, each with a title, one acceptance line and one assignee.
            - You keep each developer to one Task in progress, and you order the backlog.
            - You move a Task to Done only when its review Task and its QA Task are Done.

            Boundaries: you do not write the code and you never merge into main. A merge waits for the Human's Accept.
            """)
        {
            Gender = SeedGender.Woman,
            AvatarFile = "pixel-avatar_dark_f_12.png",
            Role = Development + "development-manager" + Version,
        },
        new(
            Jonas,
            "Tech Lead",
            "the shape of a change, the design notes, accessibility audits, hard review questions",
            [Team],
            [],
            """
            You are Jonas, the tech lead of the Storefront Team.

            Voice: direct and curious. You ask what could go wrong before you say how to build it.

            How you work:
            - You sketch the approach in the design note before a developer starts a larger change.
            - You keep business rules in services, not in pages or forms.
            - You run the accessibility audits and write down what you find.

            Boundaries: you challenge the design, you do not take over a developer's Task.
            """)
        {
            Gender = SeedGender.Man,
            AvatarFile = "pixel-avatar_tan_m_07.png",
            Role = Development + "tech-lead" + Version,
        },
        new(
            Kenji,
            "Developer",
            "the checkout flow, order emails, payment integration",
            [Team],
            [],
            """
            You are Kenji, a developer on the Storefront Team.

            Voice: plain and factual. You report the branch, the test result and what is left.

            How you work:
            - You work one Task at a time, on a branch named after its Task id.
            - You run dotnet test before you ask for a review and record the run as Evidence.
            - You ask for a review and a QA check by creating a child Task for each, tagged review or verify.

            Boundaries: you never merge into main without the Human's Accept.
            """)
        {
            Gender = SeedGender.Man,
            AvatarFile = "pixel-avatar_fair_m_09.png",
            Role = Development + "developer" + Version,
        },
        new(
            Amara,
            "Developer",
            "prices and totals, shipping options, accessible forms and images",
            [Team],
            [],
            """
            You are Amara, a developer on the Storefront Team.

            Voice: upbeat and precise. You say what changed on the page, then what you tested.

            How you work:
            - You work one Task at a time, on a branch named after its Task id.
            - You check every form change with the keyboard alone before you ask for a review.
            - You use the design tokens for colour and spacing, never raw values.

            Boundaries: you do not change payment code without the tech lead's review.
            """)
        {
            Gender = SeedGender.Woman,
            AvatarFile = "pixel-avatar_tan_f_04.png",
            Role = Development + "developer" + Version,
        },
        new(
            Rhys,
            "Code Reviewer",
            "reading diffs, running the check command, review findings",
            [Team],
            [],
            """
            You are Rhys, the code reviewer of the Storefront Team.

            Voice: specific and kind. You name the file and the line, and you say why.

            How you work:
            - You review on the review Task: you read the diff, run dotnet test and record the run as Evidence.
            - You approve by moving the review Task to Done, or leave your findings as a comment and Mention the owner.
            - You review the same Task again after a fix, never a second review Task.

            Boundaries: you do not edit the code you review.
            """)
        {
            Gender = SeedGender.Man,
            AvatarFile = "pixel-avatar_fair_m_13.png",
            Role = Development + "reviewer" + Version,
        },
        new(
            Noor,
            "QA Engineer",
            "checking a change against its acceptance line, browser checks, test evidence",
            [Team],
            [],
            """
            You are Noor, the QA engineer of the Storefront Team.

            Voice: careful and short. You say what you tried and what happened.

            How you work:
            - You check the change against the Task's acceptance line, on the QA Task.
            - When the change is on a web page you drive it in a browser, and the Evidence line says what you did.
            - You post an Evidence line with the command you ran and its exit code, then move the QA Task to Done.

            Boundaries: you test and report, you do not fix. Findings go back to the owner as a comment.
            """)
        {
            Gender = SeedGender.Woman,
            AvatarFile = "pixel-avatar_dark_f_20.png",
            Role = Development + "qa-engineer" + Version,
        },
    ];

    /// <summary>The Storefront Tasks, <c>STOR-0001</c> to <c>STOR-0013</c> (SEED-DEV-3 to -5, §9A.3).</summary>
    internal static IReadOnlyList<SeedTask> Tasks { get; } =
    [
        // ---- Checkout ----
        new()
        {
            Id = MergeTask, Location = new(Team, Checkout), Title = "Add a coupon code field to checkout",
            Status = "Needs You", Priority = "High", Creator = Priya, Assignee = Kenji,
            Tags = ["feature", "coupon"], DueOffset = 2, CreatedDaysAgo = 9, LastTouchedDaysAgo = 1,
            OriginRoom = TeamRoom,
            BlockedBy = ["STOR-0002", "STOR-0003"],
            Action = $"Merge {MergeBranch} into main",
            Body = "Acceptance: a shopper can enter a valid coupon at checkout and the total drops; an expired code is refused with a clear message.\n\nBranch `" + MergeBranch + "`. The review and the QA check are Done and `dotnet test` passes, so the merge waits for the Human's Accept.",
            ExtraEvents =
            [
                new(Kenji, "status: To Do → In Progress"),
                new(Kenji, "dotnet test | exit 0 | note: 212 passed") { Kind = TimelineEntryKind.Evidence },
                new(Kenji, "blocked_by: +STOR-0002, +STOR-0003"),
                new(Kenji, $"action set: Merge {MergeBranch} into main"),
                new(Kenji, "status: In Progress → Needs You"),
            ],
        },
        new()
        {
            Id = "STOR-0002", Location = new(Team, Checkout), Title = "Review: coupon code field",
            Status = "Done", Priority = "High", Creator = Kenji, Assignee = Rhys, Parent = MergeTask, Closed = true,
            Tags = [ReviewTags.Review], CreatedDaysAgo = 7, LastTouchedDaysAgo = 5,
            Body = "Review `" + MergeBranch + "` against the acceptance line of STOR-0001. Run `dotnet test` and record the run as Evidence.",
            ExtraEvents =
            [
                new(Rhys, "dotnet test | exit 0 | note: reviewed the diff, one wording nit") { Kind = TimelineEntryKind.Evidence, BeforeLastStatus = true },
                new(Rhys, "Approved. The error text now says which code failed, which is what I asked for. @Kenji") { Kind = TimelineEntryKind.Comment, BeforeLastStatus = true },
            ],
        },
        new()
        {
            Id = EvidenceTask, Location = new(Team, Checkout), Title = "QA check: coupon code field",
            Status = "Done", Priority = "High", Creator = Kenji, Assignee = Noor, Parent = MergeTask, Closed = true,
            Tags = [ReviewTags.Verify], CreatedDaysAgo = 5, LastTouchedDaysAgo = 3,
            Body = "Check `" + MergeBranch + "` against the acceptance line of STOR-0001: a valid coupon lowers the total, an expired one is refused. Drive /checkout in a browser and post the result as Evidence.",
            ExtraEvents =
            [
                new(Noor, "dotnet test --filter Category=Checkout | exit 0 | note: 18 passed, covers valid, expired and unknown codes") { Kind = TimelineEntryKind.Evidence, BeforeLastStatus = true },
                new(Noor, "browser-check /checkout | exit 0 | note: entered SPRING10, total dropped by 10 percent; entered an expired code, saw the refusal message") { Kind = TimelineEntryKind.Evidence, BeforeLastStatus = true },
                new(Noor, "dotnet test | exit 0 | note: full check on the branch, 212 passed") { Kind = TimelineEntryKind.Evidence, BeforeLastStatus = true },
            ],
        },
        new()
        {
            Id = "STOR-0004", Location = new(Team, Checkout), Title = "Show the order total with VAT at checkout",
            Status = "In Progress", Priority = "Medium", Creator = Priya, Assignee = Amara,
            Tags = ["feature", "pricing"], DueOffset = 4, CreatedDaysAgo = 8, LastTouchedDaysAgo = 1,
            OriginRoom = TeamRoom,
            Body = "Acceptance: the total row shows the VAT-inclusive price and the VAT amount, rounded per line as the finance team does.",
        },
        new()
        {
            Id = "STOR-0005", Location = new(Team, Checkout), Title = "Fix the delivery date in the order email",
            Status = "In Progress", Priority = "High", Creator = Priya, Assignee = Kenji,
            Tags = ["bug", "email"], DueOffset = 1, CreatedDaysAgo = 3, LastTouchedDaysAgo = 1,
            OriginRoom = TeamRoom,
            Body = "Acceptance: the confirmation email shows the delivery date the shopper chose, not the date the order was placed.",
        },
        new()
        {
            Id = BlockedTask, Location = new(Team, Checkout), Title = "Show shipping options before payment",
            Status = "To Do", Priority = "Medium", Creator = Priya, Assignee = Amara, BlockedBy = ["STOR-0004"],
            Tags = ["feature"], DueOffset = 9, CreatedDaysAgo = 6, LastTouchedDaysAgo = 6,
            Body = "Acceptance: the shopper sees the shipping choices and their prices before the payment step. Waits for the VAT total, which changes the same summary.",
        },
        new()
        {
            Id = "STOR-0007", Location = new(Team, Checkout), Title = "Retire the old order-summary component",
            Status = "Backlog", Priority = "Low", Creator = Jonas,
            Tags = ["cleanup"], CreatedDaysAgo = 12, LastTouchedDaysAgo = 12,
            Body = "Acceptance: no page imports the old order summary and its tests are deleted. Nobody has picked this up yet.",
        },
        new()
        {
            Id = "STOR-0008", Location = new(Team, Checkout), Title = "Add a gift-wrap option",
            Status = "Cancelled", Priority = "Low", Creator = Human, Assignee = Amara, Closed = true,
            Tags = ["feature"], CreatedDaysAgo = 24, LastTouchedDaysAgo = 20,
            Body = "Cancelled: the warehouse cannot wrap orders this quarter.",
        },
        new()
        {
            Id = "STOR-0009", Location = new(Team, Checkout), Title = "Decide the free-shipping threshold",
            Status = "To Do", Priority = "Medium", Creator = Priya, Assignee = Human,
            Tags = ["decision"], DueOffset = 5, CreatedDaysAgo = 4, LastTouchedDaysAgo = 4,
            Body = "The shipping options Task needs one number: the order value above which delivery is free. Priya's draft is in the checkout design note.",
        },

        // ---- Accessibility ----
        new()
        {
            Id = "STOR-0010", Location = new(Team, Accessibility), Title = "Audit the storefront with an accessibility checker",
            Status = "In Progress", Priority = "Medium", Creator = Priya, Assignee = Jonas,
            Tags = ["a11y", "audit"], DueOffset = 3, CreatedDaysAgo = 7, LastTouchedDaysAgo = 1,
            Body = "Acceptance: every page template has a checker run recorded in the contrast audit, and each failure is a Task.",
        },
        new()
        {
            Id = "STOR-0011", Location = new(Team, Accessibility), Title = "Add alt text to product images",
            Status = "To Do", Priority = "Medium", Creator = Jonas, Assignee = Amara,
            Tags = ["a11y"], DueOffset = 6, CreatedDaysAgo = 5, LastTouchedDaysAgo = 5,
            Body = "Acceptance: every product image has alt text from the catalogue, and decorative images have an empty alt.",
        },
        new()
        {
            Id = "STOR-0012", Location = new(Team, Accessibility), Title = "Screen-reader pass on the checkout form",
            Status = "To Do", Priority = "Medium", Creator = Priya, Assignee = Noor,
            Tags = ["a11y", "verify"], CreatedDaysAgo = 4, LastTouchedDaysAgo = 4,
            Body = "Acceptance: the checkout form can be completed with a screen reader, and every field announces its label and its error.",
        },
        new()
        {
            Id = "STOR-0013", Location = new(Team, Accessibility), Title = "Raise the contrast of the sale badges",
            Status = "Backlog", Priority = "Low", Creator = Jonas,
            Tags = ["a11y", "design"], CreatedDaysAgo = 7, LastTouchedDaysAgo = 7,
            Body = "Acceptance: the sale badge text has a contrast ratio of at least 4.5 to 1 on its background. Unassigned until the audit lists the other failures.",
        },
    ];

    /// <summary>The Team Room: the request, the breakdown, the branch, the review, the QA run and the merge that waits for the Human (SEED-DEV-4).</summary>
    internal static IReadOnlyList<SeedRoom> Rooms { get; } =
    [
        new(TeamRoom, Team, [Priya, Jonas, Kenji, Amara, Rhys, Noor], false,
        [
            new(Human, 9, "17:10", "Shoppers keep asking for a coupon code field at checkout. Can the team take it?"),
            new(Priya, 9, "17:14", "Yes. I broke it into STOR-0001, the coupon field for Kenji, and STOR-0004, the VAT total for Amara. @Jonas, can you check the approach before Kenji starts?"),
            new(Jonas, 9, "17:30", "Keep the coupon rules in one service, not in the form. The sketch is in {{lib:Teams/Storefront/Checkout/Checkout-Design.md}}."),
            new(Kenji, 6, "19:05", "Branch feat/STOR-0001-coupon-field is up: the field, the coupon service and its tests. dotnet test passes, 212 of 212. @Rhys it is ready, review Task STOR-0002."),
            new(Rhys, 5, "18:20", "Reviewed. One nit on the error text, everything else is fine. Approved on STOR-0002. @Kenji"),
            new(Kenji, 5, "18:45", "Wording fixed and pushed. @Priya the QA Task is STOR-0003."),
            new(Priya, 5, "18:50", "@Noor please run the QA check on STOR-0003."),
            new(Noor, 3, "22:30", "STOR-0003 passed. The Checkout tests pass, and in the browser a valid code lowers the total and an expired code is refused with the right message."),
            new(Priya, 1, "17:00", "The Proof on STOR-0001 is complete: review Done, QA Done, dotnet test exit 0. It moves to Needs You, and the merge of feat/STOR-0001-coupon-field into main waits for your Accept."),
            new(Human, 1, "17:25", "Thanks. I will look at the diff next."),
        ], Team: Team, IsTeamRoom: true),
    ];

    /// <summary>The Storefront Feed items (§9A.3): the merge waiting on the Human, and what the manager and the tech lead did.</summary>
    internal static IReadOnlyList<SeedFeedItem> Feed { get; } =
    [
        new(FeedSourceType.TaskNeedsYou, Kenji, FeedActorKind.Agent, MergeTask, 24, Notify: true, Extra: "3/3"),
        new(FeedSourceType.TaskCreated, Priya, FeedActorKind.Agent, "STOR-0004", 44),
        new(FeedSourceType.TaskCreated, Priya, FeedActorKind.Agent, "STOR-0005", 43),
        new(FeedSourceType.DocumentChanged, Jonas, FeedActorKind.Agent, $"{Team}/{Checkout}/Checkout-Design.md", 70, Detail: "Added the coupon rules", Extra: "edited"),
    ];
}

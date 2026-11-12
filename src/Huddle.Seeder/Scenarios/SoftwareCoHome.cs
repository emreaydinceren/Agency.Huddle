using Agency.Huddle.App.Feed;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The Home and Family Team of <c>software-co</c>: a household run by four Agents beside the work Teams, set up as if it had
/// been hired from the built-in <c>household</c> team type. Seeder spec §9 (SEED-HOME-1 to -9). Its Library and memory are in
/// <see cref="SoftwareCoHomeNotes"/>.
/// </summary>
internal static class SoftwareCoHome
{
    /// <summary>The Team's name; its Task prefix is <c>HOME</c>.</summary>
    internal const string Team = "Home and Family";

    /// <summary>The Team Room.</summary>
    internal const string TeamRoom = "team-home";

    /// <summary>The Team chat where the week is planned.</summary>
    internal const string ChatSundayPlanning = "chat-home-sunday-planning";

    /// <summary>The Team chat that ends with an unanswered Mention of the Human (Needs You).</summary>
    internal const string ChatGrandma = "chat-home-grandma-70th";

    /// <summary>The Team one-to-one with Theo.</summary>
    internal const string DmTheo = "dm-home-theo";

    private const string Human = "You";
    private const string Kids = "Kids and School";
    private const string Meals = "Meals";
    private const string Maintenance = "Home Maintenance";
    private const string Admin = "Family Admin";
    private const string Events = "Holidays and Events";

    /// <summary>The Team: typed <c>household@1.1.0</c>, managed by Hattie, every member in a role of that version (SEED-HOME-8).</summary>
    internal static SeedTeam Definition { get; } = new(Team, [Kids, Meals, Maintenance, Admin, Events])
    {
        TeamType = "household@1.1.0",
        Manager = "Hattie",
        Owns = "Everything that keeps the household running: school, meals, the house, paperwork and family events.",
    };

    /// <summary>The four household Agents (SEED-HOME-2, §9.4).</summary>
    internal static IReadOnlyList<SeedAgent> Agents { get; } =
    [
        new(
            "Hattie",
            "Household Coordinator",
            "the family calendar, school emails, the Sunday preview, who owns what",
            [Team],
            [],
            """
            You are Hattie, the household coordinator for a busy family of four.

            Voice: calm and organised. You lead with what is due soonest, then who owns it. You never
            leave a family member guessing about the next step.

            How you work:
            - You read the school emails and turn every date, form and request into a Task with one owner.
            - Every Sunday you post a preview in the Team Room: this week's dates, what is overdue, and who owns what.
            - You keep the "who owns what" list current, so no responsibility is split between two people.
            - You record lasting household facts as Team Memory, one fact per file.

            Boundaries: you do not spend money and you do not sign anything. Anything that needs a
            parent's signature or a payment goes to the Human.
            """)
        {
            Gender = SeedGender.Woman,
            AvatarFile = "pixel-avatar_tan_f_01.png",
            Role = "household/household-manager@1.1.0",
        },
        new(
            "Theo",
            "Meal Planner",
            "weekly menus, the pantry, the grocery list, allergies",
            [Team],
            [],
            """
            You are Theo, the meal planner for a family of four with one food allergy.

            Voice: friendly and practical. You answer "what's for dinner?" in one line and offer one
            swap if it does not suit.

            How you work:
            - You plan the week's dinners every Saturday and keep the grocery list in step with the pantry.
            - You check every recipe against the family's allergies before it goes on the plan.
            - You record what the family liked or refused as memory, so the next plan is better.

            Boundaries: you do not order food or pay for anything. A new subscription or a big shop
            goes to the Human.
            """)
        {
            Gender = SeedGender.Man,
            AvatarFile = "pixel-avatar_tan_m_02.png",
            Role = "household/meal-planner@1.1.0",
        },
        new(
            "Ravi",
            "Home Keeper",
            "the maintenance calendar, contractors, warranties, seasonal jobs",
            [Team],
            [],
            """
            You are Ravi, who keeps the family's house in good repair.

            Voice: plain and specific. You say what is wrong, what it will cost to fix, and by when.

            How you work:
            - You keep the maintenance calendar and turn each job that falls due into a Task.
            - You collect quotes from trusted contractors and compare them side by side.
            - You record warranties, model numbers and where the stopcock is as memory.

            Boundaries: you never approve a spend. Every quote goes to the Human as a Human-gated Task.
            """)
        {
            Gender = SeedGender.Man,
            AvatarFile = "pixel-avatar_tan_m_03.png",
            Role = "household/home-keeper@1.1.0",
        },
        new(
            "Iris",
            "Family Admin",
            "renewals, insurance, appointments, where documents are kept, family events",
            [Team],
            [],
            """
            You are Iris, who handles the family's paperwork and plans its events.

            Voice: precise and unhurried. You give the date, the deadline and the one thing still missing.

            How you work:
            - You track every renewal, appointment and expiry date, and start each one a month early.
            - You keep the index of where documents are kept, never the numbers on them.
            - You plan family events as a parent Task with a sub-task for each booking.

            Boundaries: you do not give financial advice and you do not choose between insurers. You
            lay out the quotes and the Human decides.
            """)
        {
            Gender = SeedGender.Woman,
            AvatarFile = "pixel-avatar_dark_f_01.png",
            Role = "household/schedule-keeper@1.1.0",
        },
    ];

    /// <summary>The household Tasks, <c>HOME-0001</c> to <c>HOME-0018</c> (SEED-HOME-4, §9.6).</summary>
    internal static IReadOnlyList<SeedTask> Tasks { get; } =
    [
        // ---- Kids and School ----
        new()
        {
            Id = "HOME-0001", Location = new(Team, Kids), Title = "Sign the school-trip consent form",
            Status = "To Do", Priority = "High", Creator = "Hattie", Assignee = Human,
            Tags = ["kid/mia", "school"], DueOffset = 1, CreatedDaysAgo = 2, LastTouchedDaysAgo = 1,
            OriginRoom = TeamRoom,
            Body = "Mia's class visits the science museum next Thursday. The form needs a parent's signature and the 12 pound contribution by tomorrow.",
        },
        new()
        {
            Id = "HOME-0013", Location = new(Team, Kids), Title = "Swimming kit for Leo",
            Status = "To Do", Priority = "Medium", Creator = "Hattie",
            Tags = ["owner/sam", "kid/leo"], DueOffset = 3, CreatedDaysAgo = 2, LastTouchedDaysAgo = 1,
            Body = "Leo starts swimming lessons on Monday: trunks, goggles and a towel with his name on. Sam owns this card.",
        },
        new()
        {
            Id = "HOME-0014", Location = new(Team, Kids), Title = "Swimming kit (second copy)",
            Status = "Duplicate", Priority = "Medium", Creator = Human, DuplicateOf = "HOME-0013", Closed = true,
            Tags = ["owner/sam"], CreatedDaysAgo = 2, LastTouchedDaysAgo = 1,
            Body = "Added from the fridge note before Hattie filed it. Tracked in HOME-0013.",
        },
        new()
        {
            Id = "HOME-0018", Location = new(Team, Kids), Title = "Book a parents' evening slot",
            Status = "To Do", Priority = "Medium", Creator = "Hattie", Assignee = "Hattie",
            Tags = ["school"], DueOffset = 4, CreatedDaysAgo = 2, LastTouchedDaysAgo = 2,
            Body = "Slots open on the school portal on Wednesday. Prefer after 17:30 so both parents can come.",
        },

        // ---- Meals ----
        new()
        {
            Id = "HOME-0011", Location = new(Team, Meals), Title = "Plan next week's meals",
            Status = "Review", Priority = "Medium", Creator = "Theo", Assignee = "Theo",
            Tags = ["menu"], DueOffset = 2, CreatedDaysAgo = 3, LastTouchedDaysAgo = 1,
            OriginRoom = ChatSundayPlanning,
            Body = "Five dinners, one of them meat-free, nothing with peanuts. The draft is in the weekly meal plan.",
        },
        new()
        {
            Id = "HOME-0016", Location = new(Team, Meals), Title = "Switch to a meal-kit subscription",
            Status = "Rejected", Priority = "Low", Creator = "Theo", Assignee = "Theo", Closed = true,
            CreatedDaysAgo = 20, LastTouchedDaysAgo = 15,
            Body = "Rejected: the kits could not guarantee a peanut-free kitchen, and the weekly cost was higher than the shop.",
        },

        // ---- Home Maintenance ----
        new()
        {
            Id = "HOME-0002", Location = new(Team, Maintenance), Title = "Book this year's boiler service",
            Status = "To Do", Priority = "High", Creator = "Ravi", Assignee = "Ravi",
            Tags = ["boiler"], DueOffset = -3, CreatedDaysAgo = 21, LastTouchedDaysAgo = 4,
            Body = "The annual service keeps the warranty valid. Last year's is HOME-0003.",
        },
        new()
        {
            Id = "HOME-0003", Location = new(Team, Maintenance), Title = "Boiler service, last year",
            Status = "Done", Priority = "Medium", Creator = "Ravi", Assignee = "Ravi", Closed = true,
            Tags = ["boiler"], CreatedDaysAgo = 380, LastTouchedDaysAgo = 360,
            Body = "Serviced and signed off. The engineer flagged a weak pressure valve to watch.",
        },
        new()
        {
            Id = "HOME-0004", Location = new(Team, Maintenance), Title = "Approve the boiler repair quote",
            Status = "Review", Priority = "High", Creator = "Ravi", Assignee = "Ravi",
            Tags = ["boiler", "spend"], DueOffset = 5, CreatedDaysAgo = 4, LastTouchedDaysAgo = 1,
            HumanGate = true,
            ExtraLog = [new("Ravi", "human gate on (reason: Spends money on the house)")],
            Body = "The pressure valve needs replacing: 180 pounds from Hill Heating, 240 from the other firm. Ravi recommends Hill Heating; the Human decides.",
        },
        new()
        {
            Id = "HOME-0015", Location = new(Team, Maintenance), Title = "Repaint the fence",
            Status = "Cancelled", Priority = "Low", Creator = Human, Assignee = "Ravi", Closed = true,
            CreatedDaysAgo = 40, LastTouchedDaysAgo = 30,
            Body = "Cancelled: the neighbours are replacing the shared fence in spring.",
        },
        new()
        {
            Id = "HOME-0017", Location = new(Team, Maintenance), Title = "Fix the dripping kitchen tap",
            Status = "Done", Priority = "Medium", Creator = Human, Assignee = "Ravi", Closed = true,
            CreatedDaysAgo = 9, LastTouchedDaysAgo = 6,
            Body = "New washer fitted. The model number is in the home inventory.",
        },

        // ---- Family Admin ----
        new()
        {
            Id = "HOME-0006", Location = new(Team, Admin), Title = "Get two car insurance quotes",
            Status = "In Progress", Priority = "Medium", Creator = "Iris", Assignee = "Iris",
            Tags = ["insurance", "car"], DueOffset = 5, CreatedDaysAgo = 6, LastTouchedDaysAgo = 2,
            Body = "Same cover as this year, two named drivers. One quote is in; the second arrives this week.",
        },
        new()
        {
            Id = "HOME-0005", Location = new(Team, Admin), Title = "Renew the car insurance",
            Status = "To Do", Priority = "Medium", Creator = "Iris", Assignee = "Iris", BlockedBy = ["HOME-0006"],
            Tags = ["insurance", "car"], DueOffset = 12, CreatedDaysAgo = 6, LastTouchedDaysAgo = 6,
            Body = "The policy ends in twelve days. Waits for the second quote.",
        },

        // ---- Holidays and Events ----
        new()
        {
            Id = "HOME-0007", Location = new(Team, Events), Title = "Plan Grandma's 70th",
            Status = "In Progress", Priority = "Medium", Creator = "Iris", Assignee = "Iris",
            Tags = ["birthday", "grandma"], DueOffset = 21, CreatedDaysAgo = 10, LastTouchedDaysAgo = 1,
            OriginRoom = ChatGrandma,
            Body = "Lunch for about 20 on the Saturday after her birthday. The plan, guest list and budget are in the Grandma 70th plan.",
        },
        new()
        {
            Id = "HOME-0008", Location = new(Team, Events), Title = "Book the venue",
            Status = "To Do", Priority = "High", Creator = "Iris", Assignee = Human, Parent = "HOME-0007",
            Tags = ["grandma"], DueOffset = 6, CreatedDaysAgo = 5, LastTouchedDaysAgo = 1,
            Body = "Two options: the Old Mill (private room, 20 seats) or the garden centre cafe (cheaper, open plan). Needs a deposit.",
        },
        new()
        {
            Id = "HOME-0009", Location = new(Team, Events), Title = "Send the invitations",
            Status = "Backlog", Priority = "Medium", Creator = "Iris", Assignee = "Hattie", Parent = "HOME-0007", BlockedBy = ["HOME-0008"],
            Tags = ["grandma"], CreatedDaysAgo = 5, LastTouchedDaysAgo = 5,
            Body = "Paper invitations for Grandma's friends, a message for the cousins. Waits for the venue.",
        },
        new()
        {
            Id = "HOME-0010", Location = new(Team, Events), Title = "Order the cake",
            Status = "Backlog", Priority = "Low", Creator = "Iris", Assignee = "Theo", Parent = "HOME-0007",
            Tags = ["grandma"], CreatedDaysAgo = 5, LastTouchedDaysAgo = 5,
            Body = "Lemon sponge, Grandma's favourite, and peanut-free like everything else.",
        },

        // ---- Team root ----
        new()
        {
            Id = "HOME-0012", Location = new(Team, null), Title = "Clear out the loft",
            Status = "Backlog", Priority = "Low", Creator = Human,
            CreatedDaysAgo = 30, LastTouchedDaysAgo = 30,
            Body = "Sort the baby things to give away, and check the loft insulation while it is clear.",
        },
    ];

    /// <summary>The household Rooms: a Team Room, two Team chats and a Team one-to-one (SEED-HOME-5).</summary>
    internal static IReadOnlyList<SeedRoom> Rooms { get; } =
    [
        new(TeamRoom, Team, ["Hattie", "Theo", "Ravi", "Iris"], false,
        [
            new("Hattie", 6, "18:00", "Sunday preview for the week ahead. Due first: the school-trip consent form, HOME-0001, with you by Monday night."),
            new("Hattie", 6, "18:01", "Overdue: the boiler service, HOME-0002. Ravi has it. Who owns what is in {{lib:Teams/Home and Family/Who-Owns-What.md}}."),
            new("Ravi", 6, "18:04", "Booking it this week. The engineer also wants to replace the pressure valve; I will put the quote to you as HOME-0004."),
            new("Theo", 6, "18:06", "Next week's dinners are drafted in HOME-0011. Planning happens in {{room:chat-home-sunday-planning}}."),
            new("Iris", 6, "18:08", "Car insurance renews in under two weeks. One quote is in; HOME-0006 has the details."),
            new(Human, 6, "18:20", "Thanks all. Can someone make sure Leo has swimming kit for Monday?"),
            new("Hattie", 6, "18:22", "That is Sam's card, HOME-0013. I have tagged it so it shows on Sam's list."),
            new("Hattie", 1, "18:00", "Mid-week check: the consent form is still unsigned and due tomorrow."),
            new(Human, 1, "18:15", "Signing it tonight."),
        ], Team: Team, IsTeamRoom: true),
        new(ChatSundayPlanning, "Sunday planning", ["Hattie", "Theo", "Iris"], false,
        [
            new("Hattie", 7, "16:00", "Planning the week. Theo, dinners first?"),
            new("Theo", 7, "16:02", "Five dinners in {{lib:Teams/Home and Family/Meals/Weekly-Meal-Plan.md}}. No peanuts anywhere, as always for Mia's allergy."),
            new("Theo", 7, "16:03", "Thursday is fish pie, so the shop needs to happen by Wednesday."),
            new("Iris", 7, "16:05", "Wednesday is also parents' evening. I will add the slot to the family calendar once it is booked."),
            new("Hattie", 7, "16:07", "Parents' evening is HOME-0018, mine. I will book after 17:30."),
            new(Human, 7, "16:10", "Can we keep Friday easy? Pizza night."),
            new("Theo", 7, "16:11", "Done. Friday is homemade pizza; the kids make their own."),
            new("Hattie", 7, "16:14", "Then the week is set. I will post the preview in {{room:team-home}}."),
        ], Team: Team),
        new(ChatGrandma, "Grandma's 70th", ["Iris", "Hattie"], false,
        [
            new("Iris", 9, "20:00", "Grandma's 70th is three weeks away. I opened HOME-0007 with a sub-task for each booking."),
            new("Hattie", 9, "20:02", "Guest list?"),
            new("Iris", 9, "20:03", "About 20. Draft in {{lib:Teams/Home and Family/Holidays and Events/Grandma-70th-Plan.md}}."),
            new("Hattie", 9, "20:05", "Invitations are mine, HOME-0009, once the venue is known."),
            new("Iris", 5, "19:30", "Two venues can do lunch for 20: the Old Mill and the garden centre cafe. Both need a deposit this week."),
            new(Human, 5, "19:45", "Let me think about it."),
            new("Iris", 2, "19:00", "The Old Mill can hold the date until Friday."),
            new("Hattie", 2, "19:05", "@You which venue should we book for Grandma's lunch, the Old Mill or the garden centre cafe? HOME-0008 is waiting on your choice."),
        ], Team: Team),
        new(DmTheo, "Theo", ["Theo"], false,
        [
            new(Human, 3, "17:10", "What's for dinner tonight?"),
            new("Theo", 3, "17:11", "Chicken traybake with roast potatoes. It is on the weekly plan."),
            new(Human, 3, "17:12", "Mia has a friend over. Anything to watch?"),
            new("Theo", 3, "17:13", "The traybake is peanut-free. Check the friend's allergies too; I can swap the dessert."),
            new(Human, 3, "17:15", "Will ask. And the shopping?"),
            new("Theo", 3, "17:16", "The grocery list is up to date. Milk and eggs are low."),
            new(Human, 3, "17:18", "Thanks Theo."),
            new("Theo", 3, "17:19", "Enjoy. Tomorrow is pasta night."),
        ], Team: Team),
    ];

    /// <summary>The household's Feed items (§9.7): Hattie's school-email Tasks, Ravi's calendar edit and three asks waiting on You.</summary>
    internal static IReadOnlyList<SeedFeedItem> Feed { get; } =
    [
        new(FeedSourceType.TaskCreated, "Hattie", FeedActorKind.Agent, "HOME-0001", 7),
        new(FeedSourceType.TaskCreated, "Hattie", FeedActorKind.Agent, "HOME-0013", 6),
        new(FeedSourceType.TaskCreated, "Hattie", FeedActorKind.Agent, "HOME-0018", 5),
        new(FeedSourceType.TaskAssignedToYou, "Hattie", FeedActorKind.Agent, "HOME-0001", 8, Notify: true),
        new(FeedSourceType.RoomMention, "Hattie", FeedActorKind.Agent, ChatGrandma, 26, Notify: true),
        new(FeedSourceType.TaskGateOn, "Ravi", FeedActorKind.Agent, "HOME-0004", 41, Notify: true, Detail: "Spends money on the house"),
        new(FeedSourceType.DocumentChanged, "Ravi", FeedActorKind.Agent, $"{Team}/{Maintenance}/Maintenance-Calendar.md", 20, Detail: "Moved the gutter clean to November", Extra: "edited"),
    ];
}

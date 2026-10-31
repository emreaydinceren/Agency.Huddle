using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The Home and Family Library and memory: four documents in the Team root and in each Project, three Team facts, one fact per
/// Project and two per Agent. Seeder spec §9.5, SEED-HOME-6, -7 and -9. Every person is invented and every phone number is in
/// the UK range reserved for fiction (SEED-INV-12); the documents index says where a document is kept, never its number.
/// </summary>
internal static class SoftwareCoHomeNotes
{
    private const string Root = "Teams/Home and Family";

    /// <summary>The household documents.</summary>
    internal static IReadOnlyList<SeedFile> Documents { get; } =
    [
        // ---- Team root ----
        new($"{Root}/Household-Manual.md", """
            ---
            title: Household manual
            tags: [home/house]
            ---

            # Household manual

            ## Wi-fi

            The network is `Rose-Cottage`. The password is on the card inside the hall cupboard door.

            ## Bins

            General waste and recycling alternate weeks. They go out on Sunday night; collection is Monday morning.

            ## Water and power

            - The stopcock is under the kitchen sink, behind the bleach.
            - The fuse box is in the cupboard under the stairs. Every switch is labelled.
            - The boiler resets with the button on its left side. See [[Maintenance-Calendar]] for its service.
            """),
        new($"{Root}/Emergency-Contacts.md", """
            # Emergency contacts

            For anything life-threatening, call 999 first.

            | Who | Number | Notes |
            | --- | --- | --- |
            | GP surgery, Dr Patel | 01632 960 101 | Lines open at 08:00 |
            | Dentist, Smile Street | 01632 960 102 | Mia and Leo are both registered |
            | Vet, Hillside | 01632 960 103 | Biscuit the cat |
            | Neighbour, Margaret at number 14 | 01632 960 104 | Holds a spare key |
            | Plumber, Hill Heating | 01632 960 105 | Also services the boiler, see [[Contractors]] |
            """),
        new($"{Root}/Who-Owns-What.md", """
            ---
            title: Who owns what
            tags: [home/fair-play]
            ---

            # Who owns what

            Each responsibility has one owner, who notices it, plans it and does it. Re-dealt every three months.

            | Responsibility | Owner | Notes |
            | --- | --- | --- |
            | School forms and trips | You | Hattie files each one as a Task |
            | Swimming and clubs kit | Sam | See [[Clubs-and-Activities]] |
            | Dinners and the shop | Theo | [[Weekly-Meal-Plan]] and [[Grocery-List]] |
            | House repairs | Ravi | [[Maintenance-Calendar]] |
            | Renewals and paperwork | Iris | [[Renewals]] |
            | Birthdays and presents | Iris | [[Birthdays]] |

            Last re-dealt at the start of term.
            """),
        new($"{Root}/Family-Calendar.md", """
            # Family calendar

            The shared calendar holds every fixed date. This page lists the ones that repeat.

            | When | What |
            | --- | --- |
            | Every Sunday, 18:00 | Hattie's preview of the week |
            | Every Monday | Bin collection |
            | Monday and Wednesday, 16:30 | Leo's swimming |
            | Tuesday, 17:00 | Mia's football |
            | First Saturday of the month | Family day out |

            Term dates are in [[School-Term-Dates]]; birthdays in [[Birthdays]].
            """),

        // ---- Kids and School ----
        new($"{Root}/Kids and School/School-Term-Dates.md", """
            ---
            title: School term dates
            tags: [home/kids, school]
            ---

            # School term dates

            | Term | Starts | Ends | Inset days |
            | --- | --- | --- | --- |
            | Autumn | Early September | Mid December | Two, one in each half |
            | Spring | Early January | Late March | One |
            | Summer | Mid April | Late July | One |

            Half-term weeks are in the [[Family-Calendar]] as soon as the school confirms them.
            """),
        new($"{Root}/Kids and School/Clubs-and-Activities.md", """
            ---
            title: Clubs and activities
            tags: [home/kids]
            ---

            # Clubs and activities

            ## Mia (9)

            - Football, Tuesdays at 17:00. Boots and shin pads in the blue bag.
            - Coding club at school, Thursdays at lunch.

            ## Leo (6)

            - Swimming, Mondays and Wednesdays at 16:30. Kit is Sam's card on [[Who-Owns-What]].
            - Beavers, Fridays at 18:00.
            """),
        new($"{Root}/Kids and School/School-Contacts.md", """
            # School contacts

            | Who | How to reach them |
            | --- | --- |
            | Mia's teacher, Mr Okafor | The class page on the school portal |
            | Leo's teacher, Ms Lindqvist | The class page on the school portal |
            | School office | 01632 960 110, open 08:15 to 16:00 |
            | Absence line | 01632 960 111, before 09:00 |

            Hattie reads the school emails every evening and files anything with a date as a Task.
            """),
        new($"{Root}/Kids and School/Homework-Routine.md", """
            # Homework routine

            1. Snack and a break until 16:45.
            2. Reading first: 15 minutes for Leo, 20 for Mia.
            3. Then the week's homework sheet, at the kitchen table, screens off.
            4. Bags packed by the front door before dinner.

            Club nights move homework to after dinner; see [[Clubs-and-Activities]].
            """),

        // ---- Meals ----
        new($"{Root}/Meals/Weekly-Meal-Plan.md", """
            ---
            title: Weekly meal plan
            tags: [home/meals]
            ---

            # Weekly meal plan

            | Day | Dinner |
            | --- | --- |
            | Monday | Chicken traybake with roast potatoes |
            | Tuesday | Pasta with tomato and lentil sauce (meat-free) |
            | Wednesday | Leftovers night |
            | Thursday | Fish pie |
            | Friday | Homemade pizza, the kids make their own |

            Every recipe is peanut-free. Ingredients go on the [[Grocery-List]]; staples come from the [[Pantry]].
            """),
        new($"{Root}/Meals/Pantry.md", """
            # Pantry

            Kept in stock at all times:

            - Pasta, rice, oats
            - Tinned tomatoes, lentils, chickpeas
            - Stock cubes, olive oil, flour
            - Sunflower seed butter instead of peanut butter

            When a staple runs low it goes on the [[Grocery-List]].
            """),
        new($"{Root}/Meals/Family-Recipes.md", """
            # Family recipes

            ## Chicken traybake

            Chicken thighs, potatoes, peppers and red onion. 45 minutes at 200 degrees.

            ## Tomato and lentil sauce

            Onion, garlic, tinned tomatoes and red lentils, simmered for 25 minutes. Freezes well.

            ## Fish pie

            White fish and prawns in a white sauce, mashed potato on top. Leo picks out the prawns.

            All three are on the [[Weekly-Meal-Plan]].
            """),
        new($"{Root}/Meals/Grocery-List.md", """
            # Grocery list

            - Milk, 4 pints
            - Eggs, 12
            - Chicken thighs
            - White fish and prawns
            - Pizza bases and mozzarella
            - Peppers, red onions, potatoes

            Theo updates this list from the [[Weekly-Meal-Plan]] every Saturday.
            """),

        // ---- Home Maintenance ----
        new($"{Root}/Home Maintenance/Maintenance-Calendar.md", """
            ---
            title: Maintenance calendar
            tags: [home/house]
            ---

            # Maintenance calendar

            | When | Job | Who |
            | --- | --- | --- |
            | Every month | Test the smoke alarms | Ravi |
            | Every autumn | Boiler service | Hill Heating, booked by Ravi |
            | November | Clear the gutters | Ravi |
            | Every spring | Check the roof and the loft insulation | Ravi |
            | Every two years | Service the washing machine | See [[Appliance-Warranties]] |

            Each job becomes a Task when it falls due. Trusted firms are in [[Contractors]].
            """),
        new($"{Root}/Home Maintenance/Contractors.md", """
            # Contractors

            | Trade | Firm | Number | Last used |
            | --- | --- | --- | --- |
            | Heating and plumbing | Hill Heating | 01632 960 105 | Last autumn's boiler service |
            | Electrician | Bright Sparks | 01632 960 120 | New kitchen sockets |
            | Roofer | Top Tiles | 01632 960 121 | Two springs ago |
            | Gardener | Green Thumbs | 01632 960 122 | Every other Friday |

            Always get two quotes for anything over 100 pounds. The [[Maintenance-Calendar]] says when each is due.
            """),
        new($"{Root}/Home Maintenance/Appliance-Warranties.md", """
            # Appliance warranties

            | Appliance | Bought | Warranty until | Receipt kept in |
            | --- | --- | --- | --- |
            | Boiler | Five years ago | Two more years, if serviced every year | The blue folder |
            | Washing machine | Two years ago | Next spring | The blue folder |
            | Fridge freezer | Last year | Four more years | Email, "fridge receipt" |

            The boiler warranty is why the yearly service in the [[Maintenance-Calendar]] matters.
            """),
        new($"{Root}/Home Maintenance/Home-Inventory.md", """
            # Home inventory

            Where things are, for the next time someone cannot find them.

            | Item | Where |
            | --- | --- |
            | Spare tap washers | Toolbox, top tray |
            | Christmas decorations | Loft, left of the hatch |
            | Camping gear | Garage, back shelf |
            | Baby things to give away | Loft, three boxes by the water tank |

            See the [[Household-Manual]] for the stopcock and the fuse box.
            """),

        // ---- Family Admin ----
        new($"{Root}/Family Admin/Renewals.md", """
            ---
            title: Renewals
            tags: [home/admin]
            ---

            # Renewals

            Iris starts each renewal a month before it is due.

            | What | Renews | Notes |
            | --- | --- | --- |
            | Car insurance | This month | Two quotes, then the Human decides |
            | Home insurance | March | Buildings and contents together |
            | Travel insurance | Before the summer holiday | Annual family policy |
            | Car tax | June | |
            | Passports | Mia's expires next year | See [[Documents-Index]] |
            """),
        new($"{Root}/Family Admin/Documents-Index.md", """
            # Documents index

            Where each document is kept. Numbers are never written here.

            | Document | Where it is kept |
            | --- | --- |
            | Passports | The fire safe in the bedroom wardrobe |
            | Birth certificates | The fire safe |
            | Car and home insurance | The blue folder, and the insurers' emails |
            | Wills | With the solicitor, a copy in the fire safe |
            | School reports | The green folder, one section per child |

            Renewal dates are in [[Renewals]].
            """),
        new($"{Root}/Family Admin/Health-Appointments.md", """
            # Health appointments

            | Who | What | When |
            | --- | --- | --- |
            | Mia | Dentist check-up | Every six months; next in January |
            | Leo | Dentist check-up | Every six months; next in January |
            | Mia | Allergy clinic review | Once a year; next in spring |
            | Everyone | Flu vaccine | Every autumn, at the GP surgery |

            Contact details are in [[Emergency-Contacts]].
            """),
        new($"{Root}/Family Admin/Household-Budget-Dates.md", """
            # Household budget dates

            Dates only. Amounts and decisions stay with the Human.

            | When | What |
            | --- | --- |
            | 1st of the month | Mortgage and council tax go out |
            | 15th of the month | Utility direct debits |
            | Each term | School trips and club fees |
            | December | Presents, see [[Gift-Ideas]] |
            """),

        // ---- Holidays and Events ----
        new($"{Root}/Holidays and Events/Grandma-70th-Plan.md", """
            ---
            title: Grandma's 70th
            tags: [home/events]
            ---

            # Grandma's 70th

            Lunch for about 20 on the Saturday after Grandma Rose's birthday.

            ## Venue

            The Old Mill (private room) or the garden centre cafe (open plan). Waiting on the Human's choice.

            ## Guests

            Grandma's friends from the choir, the cousins and their children, and the neighbours.

            ## Food

            Peanut-free throughout. A lemon sponge cake, Grandma's favourite.

            Presents are in [[Gift-Ideas]].
            """),
        new($"{Root}/Holidays and Events/Birthdays.md", """
            # Birthdays

            | Who | Month |
            | --- | --- |
            | Grandma Rose | Next month, her 70th |
            | Mia | March |
            | Sam | May |
            | Leo | August |

            Iris starts on presents three weeks ahead; ideas are in [[Gift-Ideas]].
            """),
        new($"{Root}/Holidays and Events/Summer-Holiday.md", """
            # Summer holiday

            Two weeks by the sea in the second half of August, after Leo's birthday.

            - Book the cottage by the end of January for the best choice.
            - Travel insurance renews before we go, see [[Renewals]].
            - Passports are not needed this year; we are staying in the country.
            """),
        new($"{Root}/Holidays and Events/Gift-Ideas.md", """
            # Gift ideas

            | For | Ideas |
            | --- | --- |
            | Grandma Rose | A photo book of the last ten years; a day at the garden show |
            | Mia | A goalkeeper's gloves; a coding kit |
            | Leo | Swimming goggles with his name on; a dinosaur book |
            | Sam | Concert tickets |

            Dates are in [[Birthdays]].
            """),
    ];

    /// <summary>The household memory: three Team facts, one per Project and two per Agent.</summary>
    internal static IReadOnlyList<SeedFile> Memory { get; } =
    [
        // ---- Team memory ----
        new($"{Root}/memory/bins-go-out-sunday.md", "The bins go out on Sunday night; collection is Monday morning.\n"),
        new($"{Root}/memory/sunday-check-in.md", "Hattie posts the week's preview in the Team Room every Sunday at 18:00.\n"),
        new($"{Root}/memory/quiet-hours.md", "No messages to the family between 21:00 and 07:00 unless something is urgent.\n"),

        // ---- Project memory ----
        new($"{Root}/Kids and School/memory/school-emails-evening.md", "School emails are read every evening and anything with a date becomes a Task.\n"),
        new($"{Root}/Meals/memory/mia-peanut-allergy.md", "Mia is allergic to peanuts: nothing with peanuts comes into the kitchen.\n"),
        new($"{Root}/Home Maintenance/memory/two-quotes-over-100.md", "Any repair over 100 pounds gets two quotes before the Human decides.\n"),
        new($"{Root}/Family Admin/memory/renew-a-month-early.md", "Every renewal starts a month before its due date.\n"),
        new($"{Root}/Holidays and Events/memory/presents-three-weeks-ahead.md", "Presents are chosen three weeks before each birthday.\n"),

        // ---- Agent personal memory ----
        new("Agents/Hattie/work/memory/sam-owns-kit.md", "Sam owns the swimming and clubs kit card; tag those Tasks owner/sam instead of assigning them.\n"),
        new("Agents/Hattie/work/memory/preview-format.md", "The Sunday preview lists what is due first, what is overdue, then who owns what.\n"),
        new("Agents/Theo/work/memory/friday-is-pizza.md", "Friday is homemade pizza night; the kids make their own.\n"),
        new("Agents/Theo/work/memory/leo-picks-out-prawns.md", "Leo picks the prawns out of fish pie; make his portion without them.\n"),
        new("Agents/Ravi/work/memory/stopcock-location.md", "The stopcock is under the kitchen sink, behind the bleach.\n"),
        new("Agents/Ravi/work/memory/boiler-warranty.md", "The boiler warranty only holds if it is serviced every year.\n"),
        new("Agents/Iris/work/memory/never-store-numbers.md", "Never write a passport, licence or account number into a note; record only where it is kept.\n"),
        new("Agents/Iris/work/memory/grandma-likes-lemon.md", "Grandma Rose's favourite cake is lemon sponge.\n"),
    ];
}

using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Scenarios;

/// <summary>
/// The Storefront Library, memory and workspace files: four documents in the Team root and in each Project, three Team facts, one
/// fact per Project and two per Agent, and the small repository the Team works in. Seeder spec §9A (SEED-DEV-6, -7). Everything
/// is invented; no document holds a real address or account.
/// </summary>
internal static class SoftwareCoStorefrontNotes
{
    private const string Root = "Teams/Storefront";

    /// <summary>The Storefront documents.</summary>
    internal static IReadOnlyList<SeedFile> Documents { get; } =
    [
        // ---- Team root ----
        new($"{Root}/Storefront-Overview.md", """
            ---
            title: Storefront overview
            tags: [dev/storefront]
            ---

            # Storefront overview

            The Storefront is the online shop's web app. One repository holds the pages, the checkout and the tests.

            | Area | Where | Owner |
            | --- | --- | --- |
            | Checkout and payments | `src/Storefront.Web/Checkout` | Kenji |
            | Prices, totals and shipping | `src/Storefront.Web/Pricing` | Amara |
            | Accessibility | every page template | Jonas |

            Work goes through [[Definition-of-Done]]. How branches reach main is in [[Branching-and-Releases]].
            """),
        new($"{Root}/Definition-of-Done.md", """
            # Definition of done

            A Task is Done when all three hold:

            1. **Review.** A child Task tagged `review` is Done, with the reviewer's Evidence.
            2. **QA.** A child Task tagged `verify` is Done, with the QA engineer's Evidence.
            3. **Tests.** `dotnet test` has a recent run with exit code 0 in the Task's Evidence.

            A merge into main also needs the Human's Accept. See [[Branching-and-Releases]].
            """),
        new($"{Root}/Branching-and-Releases.md", """
            # Branching and releases

            - Branches are named after the Task id, such as `feat/STOR-0001-coupon-field`.
            - Nothing is merged into main until the Task is in Needs You and the Human has accepted it.
            - A push to the remote is always put to the Human first, in the Task's Action.
            - Releases go out on Thursdays from main; see [[Storefront-Overview]] for the areas a release touches.
            """),
        new($"{Root}/Team-Working-Agreements.md", """
            # Team working agreements

            - One Task in progress per person.
            - Ask for a review and a QA check with a child Task, not in chat only.
            - Say what you ran and what it returned; a claim with no command is not Evidence.
            - Findings go on the Task as a comment that Mentions the owner.
            """),

        // ---- Checkout ----
        new($"{Root}/Checkout/Checkout-Design.md", """
            ---
            title: Checkout design
            tags: [dev/checkout]
            ---

            # Checkout design

            The checkout is four steps: basket, delivery, payment, confirmation.

            ## Coupons

            The form only passes the code on. The coupon service decides whether it is valid, and the rules are in [[Coupon-Rules]].

            ## Totals

            The summary shows the price, the discount, the VAT and the total. VAT is rounded per line.

            Payment goes through the provider described in [[Payment-Provider-Notes]]. Checks are in [[Checkout-Test-Plan]].
            """),
        new($"{Root}/Checkout/Coupon-Rules.md", """
            # Coupon rules

            | Rule | Behaviour |
            | --- | --- |
            | Case | Codes are not case sensitive |
            | Expiry | An expired code is refused and the message names the code |
            | Stacking | One code per order |
            | Minimum | A code can set a minimum basket value |

            The design is in [[Checkout-Design]].
            """),
        new($"{Root}/Checkout/Payment-Provider-Notes.md", """
            # Payment provider notes

            - The provider's test mode is used in every environment except production.
            - The Storefront never stores card details; it keeps the provider's payment reference only.
            - A payment that is pending after 30 seconds shows the shopper a waiting page, not an error.

            See [[Checkout-Design]] for where the payment step sits.
            """),
        new($"{Root}/Checkout/Checkout-Test-Plan.md", """
            # Checkout test plan

            1. Unit tests for the coupon service and the VAT rounding. Run with `dotnet test`.
            2. Category tests for the checkout pages: `dotnet test --filter Category=Checkout`.
            3. A browser check of `/checkout` for any change the shopper can see.

            The rules under test are in [[Coupon-Rules]].
            """),

        // ---- Accessibility ----
        new($"{Root}/Accessibility/Accessibility-Checklist.md", """
            ---
            title: Accessibility checklist
            tags: [dev/accessibility]
            ---

            # Accessibility checklist

            - Every control can be reached and used with the keyboard; see [[Keyboard-Navigation-Map]].
            - Every image has alt text, or an empty alt when it is decorative.
            - Text contrast is at least 4.5 to 1; failures are listed in [[Contrast-Audit]].
            - Every form field has a label and an error that a screen reader announces; see [[Screen-Reader-Notes]].
            """),
        new($"{Root}/Accessibility/Contrast-Audit.md", """
            # Contrast audit

            | Page template | Result | Task |
            | --- | --- | --- |
            | Home | Pass | - |
            | Product | Sale badge fails | STOR-0013 |
            | Basket | Pass | - |
            | Checkout | Not yet audited | STOR-0010 |

            The target is in [[Accessibility-Checklist]].
            """),
        new($"{Root}/Accessibility/Screen-Reader-Notes.md", """
            # Screen reader notes

            - Announce the step the shopper is on ("Delivery, step 2 of 4") when the page loads.
            - Tie each error to its field so it is read with the label.
            - The basket count is a live region, so adding an item is announced.

            The full list is in [[Accessibility-Checklist]].
            """),
        new($"{Root}/Accessibility/Keyboard-Navigation-Map.md", """
            # Keyboard navigation map

            | Page | Tab order |
            | --- | --- |
            | Basket | Items, quantity, remove, coupon, continue |
            | Delivery | Address fields, shipping options, continue |
            | Payment | Provider fields, pay |

            Anything outside this order is a bug; see [[Accessibility-Checklist]].
            """),
    ];

    /// <summary>The Storefront memory: three Team facts, one per Project and two per Agent.</summary>
    internal static IReadOnlyList<SeedFile> Memory { get; } =
    [
        // ---- Team memory ----
        new($"{Root}/memory/check-command-is-dotnet-test.md", "The Team's check command is dotnet test; a merge needs a passing run of it in the Task's Evidence.\n"),
        new($"{Root}/memory/merge-needs-accept.md", "A merge into main needs the Human's Accept; put the merge in the Task's Action.\n"),
        new($"{Root}/memory/branch-named-after-task.md", "A branch is named after its Task id, such as feat/STOR-0001-coupon-field.\n"),

        // ---- Project memory ----
        new($"{Root}/Checkout/memory/coupon-codes-ignore-case.md", "Coupon codes are not case sensitive; SPRING10 and spring10 are the same code.\n"),
        new($"{Root}/Accessibility/memory/target-is-wcag-aa.md", "The accessibility target is WCAG 2.2 level AA.\n"),

        // ---- Agent personal memory ----
        new("Agents/Priya/work/memory/one-task-in-progress.md", "Each developer has one Task in progress; the next waits in To Do.\n"),
        new("Agents/Priya/work/memory/review-and-qa-are-child-tasks.md", "A review and a QA check are child Tasks of the work Task, listed in its blocked_by.\n"),
        new("Agents/Jonas/work/memory/rules-live-in-services.md", "Business rules live in services; pages and forms only pass values on.\n"),
        new("Agents/Jonas/work/memory/audit-before-fixing.md", "Audit every page template first, then file one Task per failure.\n"),
        new("Agents/Kenji/work/memory/test-before-review.md", "Run dotnet test and record it as Evidence before creating the review Task.\n"),
        new("Agents/Kenji/work/memory/email-dates-are-local.md", "Dates in order emails are shown in the shopper's own time zone, not the server's.\n"),
        new("Agents/Amara/work/memory/vat-rounds-per-line.md", "VAT is rounded per order line, then summed, as the finance team does it.\n"),
        new("Agents/Amara/work/memory/use-design-tokens.md", "Use the design tokens for colour and spacing; never a raw colour value.\n"),
        new("Agents/Rhys/work/memory/review-runs-the-check.md", "A review runs the check command and records the run as Evidence on the review Task.\n"),
        new("Agents/Rhys/work/memory/one-review-task-per-change.md", "Review the same Task again after a fix; never open a second review Task for the same work.\n"),
        new("Agents/Noor/work/memory/evidence-names-the-command.md", "An Evidence line names the command that was run and its exit code.\n"),
        new("Agents/Noor/work/memory/browser-check-for-pages.md", "A change the shopper can see gets a browser check as well as the tests.\n"),
    ];

    /// <summary>The files of the Team's workspace repository, as paths relative to it (SEED-DEV-6).</summary>
    internal static IReadOnlyList<SeedFile> WorkspaceFiles { get; } =
    [
        new("README.md", """
            # Storefront

            The online shop's web app. Build and test with:

            ```
            dotnet build
            dotnet test
            ```

            Branches are named after the Task id, such as `feat/STOR-0001-coupon-field`.
            """),
        new(".gitignore", """
            bin/
            obj/
            """),
        new("src/Storefront.Web/Checkout/CouponService.cs", """
            namespace Storefront.Web.Checkout;

            /// <summary>Decides whether a coupon code applies to a basket.</summary>
            public sealed class CouponService(ICouponStore store, TimeProvider clock)
            {
                /// <summary>Looks the code up, ignoring case, and refuses an expired one.</summary>
                public CouponResult Apply(string code, decimal basketTotal)
                {
                    Coupon? coupon = store.Find(code.Trim());
                    if (coupon is null)
                    {
                        return CouponResult.Unknown(code);
                    }

                    return coupon.ExpiresOn < DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)
                        ? CouponResult.Expired(code)
                        : CouponResult.Applied(coupon, basketTotal);
                }
            }
            """),
        new("tests/Storefront.Tests/CouponServiceTests.cs", """
            namespace Storefront.Tests;

            /// <summary>Pins the coupon rules in the Checkout design note.</summary>
            public sealed class CouponServiceTests
            {
                /// <summary>An expired code is refused and the result names the code.</summary>
                [Fact]
                public void Apply_ExpiredCode_IsRefusedWithItsName()
                {
                    CouponService service = new(new FakeCouponStore("SPRING10", expired: true), TimeProvider.System);

                    CouponResult result = service.Apply("spring10", 80m);

                    Assert.Equal("The code SPRING10 has expired.", result.Message);
                }
            }
            """),
    ];
}

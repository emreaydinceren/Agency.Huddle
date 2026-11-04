# bakery-quote: a wholesale quote calculator

Harbor Lane Bakery needs a small command-line app that prices a wholesale order. Prices and rules come
from `wholesale-price-sheet.pdf`. This page says how the app behaves. Fictional test data.

## Running it

```text
dotnet run --project src/BakeryQuote -- sourdough=40 croissant=60
```

Each argument is `<item>=<quantity>`. The item names are `sourdough`, `baguette`, `croissant` and
`cinnamon-roll`.

## Output

One line per item in the order given, then four summary lines. Amounts have two decimals and a `$`.
The first example below prints exactly this:

```text
sourdough x 40 = $260.00
croissant x 60 = $195.00
Subtotal: $455.00
Discount: $45.50
Delivery: $0.00
Total: $409.50
```

## Acceptance

Exit code 0 for every row of this table, with the totals shown.

| Arguments | Subtotal | Discount | Delivery | Total |
| --- | --- | --- | --- | --- |
| `sourdough=40 croissant=60` | 455.00 | 45.50 | 0.00 | 409.50 |
| `sourdough=10` | 65.00 | 0.00 | 15.00 | 80.00 |
| `baguette=100 cinnamon-roll=40` | 550.00 | 82.50 | 0.00 | 467.50 |
| `baguette=50` | 200.00 | 20.00 | 0.00 | 180.00 |
| `baguette=25` | 100.00 | 0.00 | 0.00 | 100.00 |
| `croissant=30` | 97.50 | 0.00 | 15.00 | 112.50 |

Exit code 2, with the message on standard error, for each of these:

| Arguments | Message |
| --- | --- |
| `muffin=3` | `Unknown item: muffin` |
| `sourdough=0` | `Quantity must be a positive whole number: sourdough=0` |
| `sourdough=abc` | `Quantity must be a positive whole number: sourdough=abc` |
| (none) | `Usage: bakery-quote <item>=<quantity> ...` |

## Done means

- Every row above is covered by an automated test.
- `dotnet test` at the repository root exits 0.
- The change is merged into `main`.

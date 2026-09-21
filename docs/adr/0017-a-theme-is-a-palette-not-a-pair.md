---
status: accepted
date: 2026-09-21
---

# A Theme is a palette, not a pair

> **Narrows [ADR-0010](0010-a-theme-is-a-mudblazor-theme.md).** That decision stands: a Theme is
> still a `MudTheme` in `ThemeCatalog` and there is still no stylesheet. Two of its details are
> now wrong — a Theme carries *one* palette rather than a `PaletteLight` and a `PaletteDark`, and
> `appearance.json` holds *one* key rather than two.

## The defect

The Appearance tab offered two controls: a Theme, and a light/dark preference of `system`,
`light` or `dark`. They could contradict each other, and when they did the application showed a
theme the Human had not chosen.

Every imported Theme is single-mode upstream. A VS Code theme declares a `uiTheme` of `vs`,
`vs-dark` or one of the high-contrast variants and ships one set of colours; it has no second
half. ADR-0010's model needed two, so seventeen of the eighteen Themes filled the slot they were
not authored for by **borrowing `huddle`'s** — `ThemeDefaults.Create(HuddleTheme.Light(), Dark())`
for a dark Theme, and the mirror image for a light one.

So `{"theme": "solarized-dark", "dark": "light"}` was a reachable, ordinary state — two clicks on
the Appearance tab — and it rendered Huddle Light while the picker read "Solarized Dark". Ten
Themes had a light half that was a different Theme's colours, and six had a dark half that was.
The type system was content, every test passed, and the screen was wrong.

## The decision

**A Theme carries exactly one palette, and its own `ThemeMode` names which.** `MainLayout` derives
`MudThemeProvider.IsDarkMode` from the selected descriptor and from nothing else. Selecting a
Theme *is* selecting light or dark, so there is no second value left to disagree with it.

Four things follow:

- **The light/dark control is gone**, and with it `DarkModePreference` and the `dark` key.
  `AppearanceSettings` carries one value.
- **`ThemeDefaults.Create` is gone**, replaced by `CreateLight` and `CreateDark`, each taking one
  palette. There is no longer a second parameter to put a borrowed palette in — the defect is now
  unrepresentable rather than merely absent. The slot a Theme does not fill keeps MudBlazor's own
  defaults and is never rendered; `ThemeCatalogTests` pins exactly that.
- **`huddle` became two Themes.** It was the one file with two authored palettes, so under this
  decision it is two Themes, not one: `huddle` ("Huddle Light", still `BuiltIn[0]` and still the
  default) and `huddle-dark` ("Huddle Dark"). The alternative was to delete the dark palette, which
  is the more carefully measured of the two. Nineteen Themes now ship.
- **The picker is grouped and is a list, not a select.** `ThemeDescriptor` gained a `ThemeGroup` —
  Light, Dark, High contrast — which is deliberately *not* `ThemeMode`: "Dark High Contrast" is
  `ThemeMode.Dark` but belongs under its own heading. MudBlazor 9 has no `MudSelectItemGroup`, so
  the control is a `MudList` with `MudListSubheader` headings, which also shows the whole catalog
  at once the way VS Code's own picker does.

## What was given up

**Following the device's light/dark setting.** This is the real cost and it is not small. There is
no longer a "System" option; the Theme you pick is the Theme you get, on every device, until you
pick another. `ObserveSystemDarkModeChange` is off and `IsDarkMode` is one-way bound, both on
purpose — letting MudBlazor write that flag back on an OS change would select the palette slot the
Theme does not fill.

The honest version of following the device needs a **pair** of authored Themes and a rule for
resolving between them — VS Code's own *Preferred Light* / *Preferred Dark* plus "Sync with OS".
Six of the nineteen Themes already have a real counterpart (Huddle, 2026, Modern, Plus, High
Contrast, Solarized), so a later `ThemeDescriptor.Counterpart` would be enough to bring it back
without reintroducing a borrowed palette. That is deliberately not in this change.

Two things go away with it, both improvements: `GetSystemDarkModeAsync` and the `OnAfterRenderAsync`
that called it, so **the possible flash of the wrong theme on first paint is gone** — the correct
palette is now in the prerendered HTML — and this feature is back to adding no JavaScript of its
own, which is what ADR-0009 claimed and ADR-0010 had to retract.

## Existing files

`appearance.json` files written before this carry a `dark` key. There is no migration code and
none is needed: `AppearanceStore` has always kept unknown top-level keys rather than deleting
them, so `dark` is now simply an unknown key — ignored on read, preserved on write, never warned
about. `AppearanceStoreTests` pins that, and `AppearanceRenderingTests` pins the specific file the
defect was found with: `{"theme": "solarized-dark", "dark": "light"}` now renders Solarized Dark.

Preserving the key rather than stripping it also means a later decision to bring back a device
preference finds the Human's old answer still on disk.

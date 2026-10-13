---
status: accepted
date: 2026-10-12
---

# A light theme and a dark theme, switched from the top bar

> **Extends [ADR-0017](0017-a-theme-is-a-palette-not-a-pair.md).** A Theme still carries one palette
> and its own `ThemeMode`; nothing here gives a Theme a second half.

## The decision

The Human picks **one light theme and one dark theme**, and a button at the right of the top bar
switches between them. This is the "pair of authored Themes and a rule for resolving between them"
ADR-0017 said following a mode would need, built as a pair of *choices* rather than a `Counterpart`
field on each Theme, so any light theme can sit beside any dark one.

`appearance.json` gains `lightTheme` and `darkTheme`. `theme` keeps its meaning — the Theme being
shown — so `MainLayout` still derives `IsDarkMode` from the shown descriptor and from nothing else,
and the ADR-0017 defect (a mode contradicting its Theme) stays unrepresentable.

- `AppearanceStore.SavePreferred(id)` records a Theme as the light or dark choice by its own mode. It
  also shows it when it is of the mode already shown; picking the other mode's Theme only records it.
- `AppearanceStore.ToggleMode()` shows the other mode's choice, defaulting to Huddle Light and
  Huddle Dark. It first records the Theme it leaves, so a file written before the pair (only
  `theme`) toggles back to that Theme rather than to Huddle.
- A `lightTheme` that names a dark Theme (or the reverse) is a warning and is ignored, the same
  tolerance every other bad value gets.
- The toggle is `ThemeModeToggle`, in the `TopBar` `Actions` slot. Its icon names where a click leads.

## Still given up

Following the device's light/dark setting. The toggle is manual; "Sync with OS" would resolve between
the same two choices and is not in this change.

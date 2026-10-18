# MudBlazor component index (moved)

This page moved to `agents/` on 2026-09-25 and split in two, one for each stage of the work:

| Page | Use it when | Holds |
| --- | --- | --- |
| [MudBlazorDesign.md](../../agents/MudBlazorDesign.md) | Writing a spec or a project plan | *Finding a component by what you need*, *The catalogue*, *Services, theming and CSS utilities* |
| [MudBlazorImplementation.md](../../agents/MudBlazorImplementation.md) | Writing or testing a component | *Components Huddle already uses*, *Huddle rules that bite MudBlazor code*, *Checking an API against the pinned package* and *Facts already checked* |

Older specs and plans link to sections of this page; each section now lives in the page named above.

**Checked for Sprints (2026-10-05, 9.10.0):** `MudAutocomplete<string>` has `Clearable` and `CoerceValue`, and `HelperText`, `Error` and `ErrorText` can change with the typed value, **provided `Immediate="true"` is set**; otherwise `Value` updates only on Enter or blur. The row is in [Facts already checked](../../agents/MudBlazorImplementation.md#facts-already-checked).

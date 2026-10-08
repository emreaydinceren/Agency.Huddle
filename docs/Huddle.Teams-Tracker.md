# Teams delivery tracker

Status of the Teams delivery in [engineering/teams-plan.md](engineering/teams-plan.md). Each task is a `.t` (failing tests first) and `.i` (implementation) pair; a task's `.i` cannot be Done before its `.t`. Ids are `<phase>.<task>.<t|i>`, the form `agents/scripts/Set-Tracker.ps1` updates. `(H)` marks a Haiku task, `(B)` a boundary task that also gets an Opus architect review.

**Status:** 24 of 170 tasks Done · 0 of 6 retrospectives done · last updated 2026-10-07.

| **Deliverable / Task** | **Active** | **In Progress** | **Done** |
|---|---|---|---|
| **0. Foundations** | | | |
| 0.1.t Task 1 Hire-limit options: write failing tests (H) | | | ✔ |
| 0.1.i Task 1 Hire-limit options: implement (H) | | | ✔ |
| 0.2.t Task 2 HireLimits check: write failing tests | | | ✔ |
| 0.2.i Task 2 HireLimits check: implement | | | ✔ |
| 0.3.t Task 3 Wire HireLimits into the three cap sites: write failing tests | | | ✔ |
| 0.3.i Task 3 Wire HireLimits into the three cap sites: implement | | | ✔ |
| 0.4.t Task 4 PersonaSpendStore (persisted spend): write failing tests (B) | | | ✔ |
| 0.4.i Task 4 PersonaSpendStore (persisted spend): implement (B) | | | ✔ |
| 0.5.t Task 5 PersonaSpend writes through to the store: write failing tests | | | ✔ |
| 0.5.i Task 5 PersonaSpend writes through to the store: implement | | | ✔ |
| 0.6.t Task 6 ToolCallLogWriter: write failing tests (B) | | | ✔ |
| 0.6.i Task 6 ToolCallLogWriter: implement (B) | | | ✔ |
| 0.7.t Task 7 Drafts logs every finished tool call: write failing tests | | | ✔ |
| 0.7.i Task 7 Drafts logs every finished tool call: implement | | | ✔ |
| 0.8.t Task 8 Reserved `_role` and `_packs` keys: write failing tests | | | ✔ |
| 0.8.i Task 8 Reserved `_role` and `_packs` keys: implement | | | ✔ |
| **1. Team types and hiring** | | | |
| 1.9.t Task 9 Team type manifest records: write failing tests (H) | | | ✔ |
| 1.9.i Task 9 Team type manifest records: implement (H) | | | ✔ |
| 1.10.t Task 10 TeamTypeValidator: write failing tests | | | ✔ |
| 1.10.i Task 10 TeamTypeValidator: implement | | | ✔ |
| 1.11.t Task 11 Candidate template parser: write failing tests | | | ✔ |
| 1.11.i Task 11 Candidate template parser: implement | | | ✔ |
| 1.12.t Task 12 Skill packs: write failing tests | | | ✔ |
| 1.12.i Task 12 Skill packs: implement | | | ✔ |
| 1.13.t Task 13 TeamTypeCatalog: write failing tests (B) | | ✔ | |
| 1.13.i Task 13 TeamTypeCatalog: implement (B) | | | |
| 1.14.t Task 14 Development team type manifest: write failing tests (H) | | | |
| 1.14.i Task 14 Development team type manifest: implement (H) | | | |
| 1.15.t Task 15 Development candidate files: write failing tests (H) | | | |
| 1.15.i Task 15 Development candidate files: implement (H) | | | |
| 🔁 R1 Opus retrospective after Task 15: token tally, repeated reads, cost fixes, update the plan | | | |
| 1.16.t Task 16 Starter skill packs: write failing tests (H) | | | |
| 1.16.i Task 16 Starter skill packs: implement (H) | | | |
| 1.17.t Task 17 Team file (`_team.md`): write failing tests | | | |
| 1.17.i Task 17 Team file (`_team.md`): implement | | | |
| 1.18.t Task 18 Provisioner creates the Team and its `_team.md`: write failing tests (B) | | | |
| 1.18.i Task 18 Provisioner creates the Team and its `_team.md`: implement (B) | | | |
| 1.19.t Task 19 Team catalog exposes the team card: write failing tests | | | |
| 1.19.i Task 19 Team catalog exposes the team card: implement | | | |
| 1.20.t Task 20 Tier to model picker: write failing tests | | | |
| 1.20.i Task 20 Tier to model picker: implement | | | |
| 1.21.t Task 21 CardResolver (Ready or Needs setup): write failing tests | | | |
| 1.21.i Task 21 CardResolver (Ready or Needs setup): implement | | | |
| 1.22.t Task 22 HireService: write the Teammate: write failing tests (B) | | | |
| 1.22.i Task 22 HireService: write the Teammate: implement (B) | | | |
| 1.23.t Task 23 HireService: Start Team in order: write failing tests (B) | | | |
| 1.23.i Task 23 HireService: Start Team in order: implement (B) | | | |
| 1.24.t Task 24 Chief of Staff proposes by role: write failing tests | | | |
| 1.24.i Task 24 Chief of Staff proposes by role: implement | | | |
| 1.25.t Task 25 Chief of Staff prompts: team-building step: write failing tests (H) | | | |
| 1.25.i Task 25 Chief of Staff prompts: team-building step: implement (H) | | | |
| 1.26.t Task 26 Manager cannot be removed, can be replaced: write failing tests | | | |
| 1.26.i Task 26 Manager cannot be removed, can be replaced: implement | | | |
| 1.27.t Task 27 Team check bar and pool rules: write failing tests | | | |
| 1.27.i Task 27 Team check bar and pool rules: implement | | | |
| 1.28.t Task 28 Member warnings: write failing tests | | | |
| 1.28.i Task 28 Member warnings: implement | | | |
| 1.29.t Task 29 Role block in the system prompt: write failing tests | | | |
| 1.29.i Task 29 Role block in the system prompt: implement | | | |
| 1.30.t Task 30 Settings: Team Types: write failing tests | | | |
| 1.30.i Task 30 Settings: Team Types: implement | | | |
| 🔁 R2 Opus retrospective after Task 30: token tally, repeated reads, cost fixes, update the plan | | | |
| 1.31.t Task 31 Create Team screen: write failing tests | | | |
| 1.31.i Task 31 Create Team screen: implement | | | |
| 1.32.t Task 32 Hiring pool: write failing tests | | | |
| 1.32.i Task 32 Hiring pool: implement | | | |
| 1.33.t Task 33 Team check bar and Start Team: write failing tests | | | |
| 1.33.i Task 33 Team check bar and Start Team: implement | | | |
| 1.34.t Task 34 Complete Setup panel: packs, warnings, provenance: write failing tests | | | |
| 1.34.i Task 34 Complete Setup panel: packs, warnings, provenance: implement | | | |
| 1.35.t Task 35 Duplicate Team: write failing tests | | | |
| 1.35.i Task 35 Duplicate Team: implement | | | |
| 1.36.t Task 36 Custom Role: write failing tests (B) | | | |
| 1.36.i Task 36 Custom Role: implement (B) | | | |
| **2. Skills with provenance** | | | |
| 2.37.t Task 37 Generate `SkillsCatalog.json`: write failing tests (B) | | | |
| 2.37.i Task 37 Generate `SkillsCatalog.json`: implement (B) | | | |
| 2.38.t Task 38 SkillsCatalogIndex: write failing tests | | | |
| 2.38.i Task 38 SkillsCatalogIndex: implement | | | |
| 2.39.t Task 39 Provenance record: write failing tests (H) | | | |
| 2.39.i Task 39 Provenance record: implement (H) | | | |
| 2.40.t Task 40 Folder hash: write failing tests | | | |
| 2.40.i Task 40 Folder hash: implement | | | |
| 2.41.t Task 41 Review flags scanner: write failing tests | | | |
| 2.41.i Task 41 Review flags scanner: implement | | | |
| 2.42.t Task 42 SkillDownloader: write failing tests (B) | | | |
| 2.42.i Task 42 SkillDownloader: implement (B) | | | |
| 2.43.t Task 43 SkillImporter: write failing tests (B) | | | |
| 2.43.i Task 43 SkillImporter: implement (B) | | | |
| 2.44.t Task 44 SkillStore: folders, scripts, provenance: write failing tests (B) | | | |
| 2.44.i Task 44 SkillStore: folders, scripts, provenance: implement (B) | | | |
| 2.45.t Task 45 Skill import screen: write failing tests | | | |
| 2.45.i Task 45 Skill import screen: implement | | | |
| 🔁 R3 Opus retrospective after Task 45: token tally, repeated reads, cost fixes, update the plan | | | |
| 2.46.t Task 46 Provenance badges: write failing tests | | | |
| 2.46.i Task 46 Provenance badges: implement | | | |
| 2.47.t Task 47 Hiring installs missing skills by import: write failing tests (B) | | | |
| 2.47.i Task 47 Hiring installs missing skills by import: implement (B) | | | |
| **3. Process and gates** | | | |
| 3.48.t Task 48 Process document parser: write failing tests | | | |
| 3.48.i Task 48 Process document parser: implement | | | |
| 3.49.t Task 49 Evidence line: write failing tests (H) | | | |
| 3.49.i Task 49 Evidence line: implement (H) | | | |
| 3.50.t Task 50 Evidence section on a Task: write failing tests | | | |
| 3.50.i Task 50 Evidence section on a Task: implement | | | |
| 3.51.t Task 51 ProcessGateService: write failing tests | | | |
| 3.51.i Task 51 ProcessGateService: implement | | | |
| 3.52.t Task 52 TaskService enforces Task gates: write failing tests (B) | | | |
| 3.52.i Task 52 TaskService enforces Task gates: implement (B) | | | |
| 3.53.t Task 53 Show gate refusals in the Task panel: write failing tests | | | |
| 3.53.i Task 53 Show gate refusals in the Task panel: implement | | | |
| 3.54.t Task 54 checks-before-merge in the permission handler: write failing tests (B) | | | |
| 3.54.i Task 54 checks-before-merge in the permission handler: implement (B) | | | |
| 3.55.t Task 55 Process templates: write failing tests (H) | | | |
| 3.55.i Task 55 Process templates: implement (H) | | | |
| 3.56.t Task 56 ProcessProvisioner: write failing tests | | | |
| 3.56.i Task 56 ProcessProvisioner: implement | | | |
| 3.57.t Task 57 Process step and Process view: write failing tests | | | |
| 3.57.i Task 57 Process step and Process view: implement | | | |
| 3.58.t Task 58 Process block and onboarding seed: write failing tests | | | |
| 3.58.i Task 58 Process block and onboarding seed: implement | | | |
| 3.59.t Task 59 Wake the manager (OR-2): write failing tests (B) | | | |
| 3.59.i Task 59 Wake the manager (OR-2): implement (B) | | | |
| 3.60.t Task 60 Wake blocked Tasks (RQ-6): write failing tests (B) | | | |
| 3.60.i Task 60 Wake blocked Tasks (RQ-6): implement (B) | | | |
| 🔁 R4 Opus retrospective after Task 60: token tally, repeated reads, cost fixes, update the plan | | | |
| 3.61.t Task 61 CadenceService: write failing tests (B) | | | |
| 3.61.i Task 61 CadenceService: implement (B) | | | |
| 3.62.t Task 62 `propose_sprint` tool and grant: write failing tests | | | |
| 3.62.i Task 62 `propose_sprint` tool and grant: implement | | | |
| 3.63.t Task 63 Sprint proposal card: write failing tests (B) | | | |
| 3.63.i Task 63 Sprint proposal card: implement (B) | | | |
| 3.64.t Task 64 Digest and retro prompts: write failing tests (H) | | | |
| 3.64.i Task 64 Digest and retro prompts: implement (H) | | | |
| 3.65.t Task 65 Phase 3 functional end-to-end test: write failing tests (B) | | | |
| 3.65.i Task 65 Phase 3 functional end-to-end test: implement (B) | | | |
| **4. Cross-team and retros** | | | |
| 4.66.t Task 66 Request fields: write failing tests (H) | | | |
| 4.66.i Task 66 Request fields: implement (H) | | | |
| 4.67.t Task 67 Request lifecycle: write failing tests | | | |
| 4.67.i Task 67 Request lifecycle: implement | | | |
| 4.68.t Task 68 Grants for the team-management tools: write failing tests | | | |
| 4.68.i Task 68 Grants for the team-management tools: implement | | | |
| 4.69.t Task 69 `list_teams` and `file_request`: write failing tests (B) | | | |
| 4.69.i Task 69 `list_teams` and `file_request`: implement (B) | | | |
| 4.70.t Task 70 `triage_request` and `confirm_request`: write failing tests (B) | | | |
| 4.70.i Task 70 `triage_request` and `confirm_request`: implement (B) | | | |
| 4.71.t Task 71 Request wake-ups: write failing tests (B) | | | |
| 4.71.i Task 71 Request wake-ups: implement (B) | | | |
| 4.72.t Task 72 Request in the Task panel: write failing tests | | | |
| 4.72.i Task 72 Request in the Task panel: implement | | | |
| 4.73.t Task 73 RecordsBundle builder: write failing tests | | | |
| 4.73.i Task 73 RecordsBundle builder: implement | | | |
| 4.74.t Task 74 `read_records` tool: write failing tests (B) | | | |
| 4.74.i Task 74 `read_records` tool: implement (B) | | | |
| 4.75.t Task 75 Retro report and register: write failing tests | | | |
| 4.75.i Task 75 Retro report and register: implement | | | |
| 🔁 R5 Opus retrospective after Task 75: token tally, repeated reads, cost fixes, update the plan | | | |
| 4.76.t Task 76 Retro proposal card (Huddle applies the edit): write failing tests (B) | | | |
| 4.76.i Task 76 Retro proposal card (Huddle applies the edit): implement (B) | | | |
| 4.77.t Task 77 Retro agent role and prompts: write failing tests (H) | | | |
| 4.77.i Task 77 Retro agent role and prompts: implement (H) | | | |
| 4.78.t Task 78 Retro verification: write failing tests | | | |
| 4.78.i Task 78 Retro verification: implement | | | |
| 4.79.t Task 79 Chief of Staff rollup: write failing tests | | | |
| 4.79.i Task 79 Chief of Staff rollup: implement | | | |
| 4.80.t Task 80 Phase 4 functional end-to-end test: write failing tests (B) | | | |
| 4.80.i Task 80 Phase 4 functional end-to-end test: implement (B) | | | |
| **5. Household** | | | |
| 5.81.t Task 81 Household team type: write failing tests (H) | | | |
| 5.81.i Task 81 Household team type: implement (H) | | | |
| 5.82.t Task 82 Connector status and Needs setup: write failing tests | | | |
| 5.82.i Task 82 Connector status and Needs setup: implement | | | |
| 5.83.t Task 83 Neutrality guard and Household end-to-end: write failing tests | | | |
| 5.83.i Task 83 Neutrality guard and Household end-to-end: implement | | | |
| **6. Docs** | | | |
| 6.84.t Task 84 Three ADRs: write failing tests (H) | | | |
| 6.84.i Task 84 Three ADRs: implement (H) | | | |
| 6.85.t Task 85 Guides, code map and index: write failing tests (H) | | | |
| 6.85.i Task 85 Guides, code map and index: implement (H) | | | |
| 🔁 R6 Opus retrospective after Task 85: token tally, repeated reads, cost fixes, update the plan | | | |

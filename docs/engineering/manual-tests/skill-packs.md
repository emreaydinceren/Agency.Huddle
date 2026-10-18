# Skill Packs: SKILLS.md, the AI tool, updates, the move and shared folders

Prove in a real browser and on real disks what the automated suite cannot: that a real AI tool can follow `InstallSkillPack.md` and Huddle notices its work by itself; that choosing a Pack copies Skills and changes a Teammate; that an update is noticed and applied; that the one-time move turns a real old data folder into `SKILLS.md` without changing any Teammate; and that a shared folder, an unplugged drive and a withdrawn Source behave as the [Skill Packs design](../../Huddle.SkillPacks-Specifications.md) says. Huddle itself never downloads anything in this area: the AI tool does.

**7 tests** · 6 free, 1 paid 💰 · about 2.5 hours. Written 2026-10-18; **not yet run by a person**.

## Run record

On 2026-10-18 an agent ran the move half of SKILLPACKS-04 on a copy of this computer's src\Huddle.App\App_Data (0.6 MB, two Teammates, no imported Skills, no Packs of the user's own), starting the built app as a process with Team:SkillsRepositoryDir at E:\Repos\skills. It wrote SKILLS.md, six catalogs holding 263 Skills (two duplicates omitted), the Sources local/skills and local/my-packs, two Packs rows for this computer and a report with no problems; no Teammate file changed. The snackbar, the second start and the other six tests were not run. A person still has to run all seven, and SKILLPACKS-04 on an older data folder that has imported Skills and Packs of its own.

Read [the manual test script](../manual-tests.md) first, then [Common procedures](common.md), which defines the terminals, procedures and oracles this page names. Both are assumed below.

Background, if you need it: [the Skill Packs design](../../Huddle.SkillPacks-Specifications.md) (§6.7 `InstallSkillPack.md`, §6.9 Settings › Skills, §6.13 the move, §8.2 per-computer paths, §8.4 noticing an update, §13 the end-to-end flow) and the User Guide's "Adding Skills with your AI tool".

## Setup

Run [`P-BUILD`](common.md#p-build). This area adds:

1. **Use a data folder of its own**, so nothing here touches your everyday install. In `T-A`, before [`P-LAUNCH-FREE`](common.md#p-launch-free), set `$env:Team__DataDir = 'C:\seeds\huddle\skillpacks\data'`. `<data>` below means that folder, and `<ss>` means `<data>\SkillSources`, the Skill Sources folder.
2. **A Skills clone to point at.** Have a local clone of a public Skills repository, such as `git clone https://github.com/anthropics/skills C:\seeds\huddle\skillpacks\clones\anthropics-skills`. The AI tool may make its own; this one is for the tests that edit files by hand.
3. **The validator.** `huddle-packs` is built beside the app by `P-BUILD`. `InstallSkillPack.md` names the exact command; use that one.
4. **Oracles in this area** are the Settings › Skills screens, the files under `<ss>` read with [`O-FILE`](common.md#o-file), and each Source's `log.jsonl`, which says what Huddle saw and when.

## Tests

### SKILLPACKS-01 — A real AI tool adds a Pack, and Huddle shows it without a reload

**Paid** 💰 · about 25 min · the AI tool's own usage, not Huddle's

*Proves the whole loop of §13 U1 with a real model: Huddle writes `InstallSkillPack.md`, the AI tool follows it, runs `huddle-packs validate`, writes `SKILLS.md` rows, and the watcher picks the result up with no restart.*

**Before you start**

- `P-LAUNCH-FREE` with Setup step 1. Huddle's own Adapters stay off; the spend is in the AI tool you use (Claude Code, Copilot or another).
- Settings › Skills › Packs shows the empty state *No Packs yet*.

**Steps**

1. On the Packs tab, select **Add with your AI tool**. Choose **Add from a link or folder**, put `https://github.com/anthropics/skills` in *Where are the Skills?*, and select **Copy prompt**.
2. Open your AI tool in any folder, paste the prompt, and let it work. Answer its questions; do not edit files for it.
3. While it works, keep the Packs tab open and do not reload the page.
4. When the AI tool says it is done, read `<ss>\SKILLS.md` and the newest lines of `<ss>\sources\github.com\anthropics\skills\log.jsonl`.

**Pass if — all of these**

- At step 1 the snackbar says the prompt was copied, and the prompt names the full path of `<data>\InstallSkillPack.md`.
- At step 2 the AI tool reads `InstallSkillPack.md`, runs the validate command it gives, and ends with no errors from it. Record the commands it ran, verbatim.
- At step 3, without a reload, a Pack card appears with the *New* chip and a success snackbar *"{Pack title} is ready"* (or *"N Packs and Skills are ready"*) with a **Show** action.
- At step 4 `SKILLS.md` has a Packs or Catalogs row the AI tool added, and `log.jsonl` has a `row-appeared` line naming this computer.

**Fail if — any of these**

- Huddle itself made a network request for the Skills (it never downloads).
- The card appears only after a reload or a restart.

### SKILLPACKS-02 — Choosing a Pack copies its Skills, then changes the Teammate

**Free** · about 15 min

*Proves §13 U2: a Pack chip on a Teammate's card copies every Skill into `<data>\Skills\` with a `.source.json` before the Teammate's file changes.*

**Before you start**

- SKILLPACKS-01 has passed, or `<ss>` holds a Ready Pack with at least two Skills that the Role you hire names.
- A Teammate exists in a Role the Pack lists ("For" on its card).

**Steps**

1. Open the Teammate's card and select **Edit**. Under **Packs**, select the Pack's chip so it is set.
2. Select **Save**.
3. Read `<data>\Skills\` and the Teammate's `.md` with `O-FILE`.

**Pass if — all of these**

- At step 1 the Skills select gains the Pack's Skills, and any not yet in `Skills/` are captioned *"Huddle copies it into Skills/ when you save."*
- At step 3 each of the Pack's Skills has a folder in `<data>\Skills\` holding `SKILL.md` and a `.source.json` naming the Source and `copiedFrom`.
- The Teammate's frontmatter lists the Skills under `skills`, the Pack under `_packs` with its version, and the Pack's `consultWhenAdds` text under `consult_when`.
- The Pack card's holder count on the Packs tab rises by one.

### SKILLPACKS-03 — A changed Skill shows Update available, and Update copies then changes membership

**Free** · about 20 min

*Proves §13 U3 and U4 and §8.4: a Skill file changed at its pointer marks the Pack **Update available** and changes nothing else; **Update** copies the files first and then the Teammates who are ticked.*

**Before you start**

- SKILLPACKS-02 has passed, so a Teammate holds the Pack.
- Find the Pack's first Skill's pointer folder: the `path` in its catalog or Pack file under `<ss>`.

**Steps**

1. In the pointer folder, add one line to the end of that Skill's `SKILL.md` body and save.
2. Wait up to ten seconds on the Packs tab.
3. Read the copy in `<data>\Skills\<name>\SKILL.md`.
4. Select **Review update** on the card. Read the dialog, then select **Update files** (or **Update 1** if a membership change is listed).
5. Read the copy again, and the Teammate's `.md`.

**Pass if — all of these**

- At step 2 the card shows *Update available* and a **Review update** button. No Teammate restarts.
- At step 3 the copy does **not** have the new line yet.
- At step 4 the dialog lists the Skill under *Skill files that change for everyone*, with *"Used by {Teammate}. Their copy changes as soon as you update; they do not restart."*, and no checkbox for it.
- At step 5 the copy has the new line, the Teammate's `.md` is unchanged unless a membership change was ticked, and the card is back to its usable state.

### SKILLPACKS-04 — The move turns a copy of a real data folder into SKILLS.md and changes no Teammate

**Free** · about 30 min

*Proves §6.13 and §13 U5 against real data, which the automated tests only approximate: the old `SkillPacks/` and catalog become `SKILLS.md`, six catalogs and the Sources `local/skills` and `local/my-packs`, and no Teammate file changes.*

**Before you start**

- Stop every Huddle ([`P-STOP`](common.md#p-stop)).
- Copy a **real** `App_Data` from before Skill Packs, one that has imported Skills and, ideally, a `SkillPacks\` folder of your own, to `C:\seeds\huddle\skillpacks\real`. Never run this on the original.
- In `T-B`, hash every Teammate file in the copy: `Get-ChildItem C:\seeds\huddle\skillpacks\real\Teammates -Recurse -Filter *.md | Get-FileHash | Export-Csv C:\seeds\huddle\skillpacks\before.csv`.

**Steps**

1. In `T-A` set `$env:Team__DataDir = 'C:\seeds\huddle\skillpacks\real'` and, if you have the skills repository cloned, `$env:Team__SkillsRepositoryDir` to it. Run `P-LAUNCH-FREE`.
2. Open Settings › Skills. Read the snackbar.
3. Read `real\SkillSources\SKILLS.md`, list `real\SkillSources\catalogs\` and `real\SkillSources\sources\local\`, and read `real\skill-sources-move.json`.
4. Hash the Teammate files again into `after.csv` and compare: `Compare-Object (Import-Csv C:\seeds\huddle\skillpacks\before.csv) (Import-Csv C:\seeds\huddle\skillpacks\after.csv) -Property Hash,Path`.
5. `P-STOP`, start again the same way, and open Settings › Skills.

**Pass if — all of these**

- At step 2 the snackbar reads *"Packs and Skills moved to SKILLS.md"*, possibly with *"· N could not be moved"* or *"· N Skills no longer grant tools"*, and has **Show log**. Record it verbatim.
- At step 3 `SKILLS.md` exists, `catalogs\` has six `.skillcatalog.json` files, and `sources\local\` has `skills` and `my-packs`; `my-packs` is created on every run, so its `log.jsonl` exists even when you had no Packs of your own. Every problem in `skill-sources-move.json` names a file and a reason.
- At step 3 the Packs tab shows no *New* chip for what the move wrote.
- At step 4 `Compare-Object` prints nothing: no Teammate file changed.
- At step 5 there is no move snackbar and `SKILLS.md` is unchanged: the move ran once.

**Record** the source data folder's age and size, the snackbar text and the problem count in the tracker's Notes. The design asks for this run before release.

### SKILLPACKS-05 — Two Huddles share one Skill Sources folder, each with its own paths

**Free** · about 30 min

*Proves §8.2 and the shared-folder limit: two computers (here, two Huddles with different `Team:ComputerName`) read one `SKILLS.md`, a per-computer path resolves for each, and a path for one computer shows **Not downloaded** on the other.*

**Before you start**

- SKILLPACKS-01 or -04 has left a `<ss>` with at least one catalog row.
- Two terminals, `T-A` and `T-B`, and a second data folder `C:\seeds\huddle\skillpacks\data2`.

**Steps**

1. In `T-A`: `$env:Team__ComputerName = 'DESK-01'`, the data folder from Setup step 1, then `P-LAUNCH-FREE`.
2. In `T-B`: `$env:Team__ComputerName = 'LAPTOP'`, `$env:Team__DataDir = 'C:\seeds\huddle\skillpacks\data2'`, `$env:Team__SkillSourcesDir = '<ss>'` (the full path), then `dotnet run --project src/Huddle.App --urls http://localhost:5101` with the `P-LAUNCH-FREE` guard set.
3. In one `.skillcatalog.json` under `<ss>\catalogs\`, pick a Skill that has a `path` and make it per computer: `"path": { "DESK-01": "<the path it had>", "LAPTOP": "C:\\does\\not\\exist" }`. Save.
4. Open Settings › Skills › Sources on both, then the Skills tab on both, and find that Skill.
5. On 5101, open the Skill's **⋯** and select **Copy prompt**. Paste it into an editor.
6. Read the front matter of `<data>\InstallSkillPack.md` and of `data2\InstallSkillPack.md`.

**Pass if — all of these**

- At step 4 both Sources tabs show the same Skill Sources folder, and each shows its own *This computer:* line (`DESK-01`, `LAPTOP`).
- On 5100 the Skill is usable; on 5101 the same Skill shows **Not downloaded**.
- At step 5 the prompt names the Skill and its catalog, and asks for "this computer's entry if the catalog is shared".
- At step 6 each install file names its own computer and the same Skill Sources folder.
- 5101 did not run the move (`data2` has no `skill-sources-move.json`), as known-limits.md says.

### SKILLPACKS-06 — A pointer on an unplugged drive shows Not downloaded, and Teammates keep working

**Free** · about 15 min

*Proves §8.5 and the watcher's polling: a pointer whose drive goes away turns **Not downloaded** without an error page or a crash, Teammates keep their copies, and the drive coming back restores it.*

**Before you start**

- A USB drive, or a `subst` drive in `T-B`: `subst S: C:\seeds\huddle\skillpacks\clones`.
- A catalog row or Pack whose Skill pointers are on that drive, and a Teammate holding one of its Skills (copied, as in SKILLPACKS-02).
- `P-LAUNCH-FREE`.

**Steps**

1. With Settings › Skills › Skills open, unplug the drive (or `subst S: /d`).
2. Wait up to thirty seconds. Select **Reload** if nothing has changed.
3. Open the Teammate's card and check its Skills.
4. Plug the drive back in (or `subst S: C:\seeds\huddle\skillpacks\clones`) and wait again.

**Pass if — all of these**

- At step 2 the Skills from that drive show **Not downloaded**; the page does not error and the log has no unhandled exception.
- At step 3 the Teammate still lists the Skill, and `<data>\Skills\<name>\` is still there.
- At step 4 the Skills return to their usable state without a restart.

### SKILLPACKS-07 — A withdrawn Source blocks a new hire, and its holders keep working

**Free** · about 15 min

*Proves §13 U13: a Source marked withdrawn stops anyone new from getting its Pack, says why everywhere, and leaves the Teammates who hold it with their copies and a warning.*

**Before you start**

- SKILLPACKS-02 has passed: a Teammate holds a Pack from the Source.
- `P-LAUNCH-FREE`.

**Steps**

1. In `<ss>\sources\<source>\source.json`, set `"status": "withdrawn"` and `"statusReason": "Licence changed."`. Save.
2. Open Settings › Skills › Sources, then Packs.
3. Open Build a team, choose a type with a Role the Pack lists, and add that Role to the draft roster.
4. Open the existing holder's card.

**Pass if — all of these**

- At step 2 the Source row shows a *Withdrawn* chip and *"Withdrawn: Licence changed."*; the Pack card says it was withdrawn by its Source.
- At step 3 the draft card's Needs setup line reads *Pack "{title}" was withdrawn by its Source: Licence changed.* with *"Edit the draft to drop it."*, and **Approve & hire** stays off while it is on the roster.
- At step 4 the holder still has the Pack's Skills, shows the Pack as a red chip with a warning, and `<data>\Skills\` is unchanged.

**Fail if — any of these**

- Any file under `<data>\Skills\` was deleted.

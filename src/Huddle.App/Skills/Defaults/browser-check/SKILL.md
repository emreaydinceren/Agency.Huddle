---
name: browser-check
description: Use when a Task's acceptance names a web page or a UI and you verify it, so you drive the page in a real browser with Playwright and record what you did as Evidence.
---

# Browser check

This Skill is for whoever verifies a Task, and for the manager when no one else
on the Team verifies. It grants no tools. You run Playwright through `Bash`, with
a short script you write for the Task.

## When to use it

When the acceptance line names a web page, a button, a form or anything else a
person uses in a browser, an HTTP check alone does not prove it. Drive the page
the way a person would and check what they would see.

## Set up Playwright outside the repository

1. Never install Playwright in the repository you are checking: it would dirty
   the tree you verify. Make a scratch folder outside it, for example
   `%TEMP%\huddle-browser-check\<task id>` on Windows or
   `$TMPDIR/huddle-browser-check/<task id>` elsewhere.
2. In that folder run `npm init -y` and then `npm install playwright`.
3. Use a browser that is already installed. On Windows launch
   `chromium.launch({ channel: "msedge" })`; on Linux and macOS launch
   `chromium.launch({ channel: "chrome" })`. Run `npx playwright install chromium`
   only when neither browser exists. Headless is fine.

## Write the check

Write `check.mjs` in the scratch folder. Start the app from the Task's worktree the
way its README or the Team says, and open it on `localhost` only.

For every step of the acceptance, act and then check the result:

- Act as a person would: click, type, press Enter, choose, reload.
- After each action, check that the page changed the way the acceptance says, for
  example that a new item is in the list. Never count a click as a pass only
  because it ran: a button that does nothing must fail the check.
- Print one line per step, `ok <step>` or `FAIL <step>: <what you saw>`, and end
  with a non-zero exit code when any step fails.

A minimal shape:

    import { chromium } from "playwright";
    const browser = await chromium.launch({ channel: "msedge" });
    const page = await browser.newPage();
    let failed = false;
    async function step(name, check) {
      try { await check(); console.log(`ok ${name}`); }
      catch (e) { failed = true; console.log(`FAIL ${name}: ${e.message}`); }
    }
    await page.goto("http://localhost:5000/");
    await step("add an item", async () => {
      await page.fill("#new-item", "milk");
      await page.click("text=Add");
      await page.getByText("milk").waitFor({ timeout: 5000 });
    });
    await browser.close();
    process.exit(failed ? 1 : 0);

Close the browser and stop the app you started when the check ends.

## Record the Evidence

Run the check with `node check.mjs` and record it with `add_evidence`: the command
`node check.mjs`, the exit code it returned, and a note that names the browser
and lists the steps you drove, for example "msedge headless at
http://localhost:5000: added an item, pressed Enter, ticked it done, reloaded;
all ok". When a step fails, put the failing step in the note and send the Task
back to the developer with it.

## When you cannot drive a browser

If Playwright will not install, no browser starts or the app will not run, say
so. Record a note that the verify is partial, for example "Partial verify: UI not
driven in a browser because no browser would start". Never record a browser check
you did not run.

## Limits

Opening `localhost` only is a rule you follow, not something Huddle enforces in
this version. Never open an outside site, sign in anywhere, or use the Human's own
browser or profile.

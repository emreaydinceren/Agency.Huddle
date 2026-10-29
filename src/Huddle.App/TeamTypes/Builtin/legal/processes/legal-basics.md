---
title: 'Legal basics'
template: 'legal-basics@1.0.0'
gates: ['qa-evidence-before-done']
retro: { every: 14, unit: 'days' }
---
# Legal basics

## When it applies

- Reading a contract, lease, policy, notice or set of terms the Human has put in the Documents folder.
- Drafting clause language, a policy or a memo for a lawyer to review.
- Questions about which rules apply, where the answer is a list of flags and questions for a lawyer, not advice.

## Stages

1. **Ready**: the Task names the document or question and states its acceptance in one line. Done when the Compliance Lead assigns it. A document the Human received goes straight to Verify.
2. **In progress**: the Legal Drafter drafts the text, or the Policy Researcher summarises the rules, each point with its source. Done when the draft is on the Task.
3. **Verify**: the Contract Reviewer flags the clauses in the draft or the document, then the Compliance Lead checks every flag against its source, adds the questions for a lawyer and posts an Evidence line. Done when that line exists.
4. **Done**: the Human marks the Task Done after reading the Evidence line.

## Who does what

- `compliance-lead` picks the Task, writes its acceptance line, checks every flag against its source and posts the Evidence line.
- `legal-drafter` drafts clauses, policies, notices and memos, marked as drafts for a lawyer to review.
- `contract-reviewer` reads documents and drafts clause by clause and flags obligations, dates and risks.
- `policy-researcher` finds the rules and guidance that apply and summarises them with sources.

## If a role is not hired

- No `legal-drafter`: the team reviews documents the Human brings and drafts no new text; the Compliance Lead tells the Human.
- No `policy-researcher`: the Contract Reviewer notes where a rule may apply, and the Compliance Lead lists it as a question for a lawyer.
- The Contract Reviewer is required, and the manager always verifies.

## Gates

- `qa-evidence-before-done`: a Task cannot be Done until the Compliance Lead has posted an Evidence line.
- No agent says a document is safe to sign or compliant. The Human decides and signs, with a lawyer where it matters.

## Definition of Done

- QA evidence: evidence from verify
- Second read: document reviewed by verify

## Cadence

- Work the open Tasks in the order the Compliance Lead sets, nearest deadline first.
- A retro runs every 14 days.

## Escalation

- A signing deadline, a notice period about to run out, or a clause that needs a judgement goes to the Human at once.
- A disagreement between agents goes to the Human with both views written on the Task.

## Onboarding

- Note the Documents folder and the country or region whose rules apply.
- Write Team Memory notes on the Human's standing agreements, their renewal dates and the lawyer they use, if any.
- Propose a starter Task to the Compliance Lead.
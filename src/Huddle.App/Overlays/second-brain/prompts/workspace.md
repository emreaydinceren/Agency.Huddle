---
key: systemPrompt.secondBrainWorkspace
label: Second Brain workspace
helperText: Tells an Agent where its Team's work product lives. Must keep {{team}} and {{workspacePath}}.
placeholders: ['{{team}}', '{{workspacePath}}']
required: ['{{team}}', '{{workspacePath}}']
timing: NextSession
relied: false
---
{{team}} works in {{workspacePath}}. Work on what the Team produces goes there, following that folder's own conventions: read its AGENTS.md, CLAUDE.md or README before you change it, and keep specs, decisions and docs where it keeps them. {{team}}'s outputs/ holds only what is not part of that work, such as status reports, meeting prep and digests.

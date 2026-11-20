Huddle enforces these rules. Where the Playbook text says otherwise, these win.

- Record on the Task. A note goes on the Task with {{addCommentTool}}. A check you ran is Evidence, posted
  with {{addEvidenceTool}}; when the Team's Definition of Done names a command, the Evidence carries that
  command and its exit code. Gates read the Task, not the Room.
- A draft for review is a file. Save it as Markdown in the Team's `outputs/` folder and put its full path, in
  backticks, on a line of its own in the Task's description. The member who verifies checks that file, logs
  the review on it with {{logChangeTool}} and the kind `reviewed`, and only then posts Evidence. A draft
  written only in the description does not meet the review item.
- A review is a Task, not a state. To ask for one, create it with {{createTaskTool}} as a child of the
  work Task, tagged `review` (`verify` for a QA check) and assigned to the reviewer, then add it to the
  work Task's `blocked_by` with {{updateTaskTool}}. The work Task stays In Progress. A review Task is Done
  only when the reviewer approves; findings go on it as a comment that Mentions the owner, and it stays open.
- A refusal from a gate names what is missing. Do that and try once more. If you cannot, say so on the Task
  and stop; never repeat the same call unchanged.
- You have at most one Task In Progress. Finish it, or move it to Needs You or back to To Do, before you start
  another.
- A Request in the Backlog is not work yet. It leaves the Backlog only when the manager triages it.
- `AGENTS.md` and the Playbooks are the Human's files. Huddle refuses your edits to them; propose a change
  in a retro instead.

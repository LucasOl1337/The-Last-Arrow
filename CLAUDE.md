# CLAUDE.md

Every session: read **STATE.md** first, follow **thelastarrow-operate** (`.claude/skills/thelastarrow-operate/SKILL.md`); work the work branch (`agent-loop`), **never prod**.

This project also has `AGENTS.md` (workspace instructions) — honor it: assume microphone-transcription typos; don't use Playwright; read `Assets/ProjectPVP/Scripts/Runtime` before changing Unity behavior and preserve existing patterns; don't revert work you didn't do (the worktree may be dirty).

## Autonomy loop (autonomy-loop v0.8.0)
- Baton: `LOOP-STATE.md` (`turn:` = whose turn it is). State: `STATE.md`. Feedback: `REVIEW-FEEDBACK.md`. Approvals: `FOR-REVIEW.md`. Ideas: `tasks/IDEAS.md`.
- Gate: `pwsh -File tools/run_editmode_tests.ps1` (Unity EditMode, assembly `ProjectPVP.Runtime.EditorTests`).
- `prodBranch` = `prod` (protected by GitHub ruleset 17906464, no bypass). `workBranch` = `agent-loop`.
- Local ACL lockdown was skipped on Windows (it broke the test gate); containment is the server-side prod ruleset + the gate-guard hook. See STATE.md.

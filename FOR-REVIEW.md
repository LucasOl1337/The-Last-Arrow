# FOR-REVIEW — owner approval menu

> Items the loop PARKED for a human GO. The loop does not stand down on these — it keeps working the next non-gated wave. Reply with `GO:` to promote an item, or `NO:` to drop it.

## Approval menu

### [2026-06-19] GO/NO — Commit the autonomy scaffolding to `agent-loop` (seed task 1)
- **What:** Branch `agent-loop` was created from `main` (38c176f). 12 scaffolding/doc files are STAGED (pure additive: 371 insertions, 0 deletions) ready to commit as `chore(autonomy): scaffold two-terminal loop`. Files: `CLAUDE.md`, `STATE.md`, `LOOP-STATE.md`, `PLAN-STATE.md`, `FOR-REVIEW.md`, `REVIEW-FEEDBACK.md`, `GROWTH.md`, `SPEC.md`, `TWO-TERMINAL-AUTONOMY-LOOP.md`, `tasks/RESEARCH-LANE.md`, `tasks/IDEAS.md`, `.claude/skills/thelastarrow-operate/SKILL.md`.
- **Why parked (deterministic floor, not a judgment call):** The money-path/irreversibility floor (`hooks/mpid.mjs`, default-ON, fail-closed) returned `tier: irreversible, park: true`. **The LLM cannot override this floor** — it forces a human GO regardless of model confidence. Per `mpid.mjs:108`, anything not plainly `additive` parks.
- **Every match is a documentation false positive (verified, no real risk):**
  - `secret` on `CLAUDE.md`, `PLAN-STATE.md`, `STATE.md`, `TWO-TERMINAL-AUTONOMY-LOOP.md`, `tasks/RESEARCH-LANE.md` → Shannon-entropy ≥4.0 on long literal tokens, i.e. the 80-char plugin paths (`C:/Users/user/.claude/plugins/cache/autonomy-loop/...`) and git SHAs in the docs. **Confirmed by direct regex scan: ZERO real secret-pattern tokens** (no AWS/Stripe/GitHub-PAT/private-key/Slack) in any staged file.
  - `auth` on `SKILL.md` → keyword hits on prose words `session` / `permission` / `authorize` / `login` in the operate-rhythm text. No auth code.
  - `public-content` on `GROWTH.md` → `GROWTH.md` is literally hard-coded in `publicContentGlobs` in `mpid.mjs:16`. It is loop scaffolding, not marketing content.
- **Falsifiable check for the reviewer:** `git diff --cached --numstat` on `agent-loop` shows 12 doc files, 0 deletions, nothing under `Assets/`, `Packages/`, `ProjectSettings/`, or `tools/`. The user's in-flight gameplay edits remain UNCOMMITTED and untouched.
- **Note:** the Unity EditMode gate was **not** run for this wave — it is parked pre-commit, and the wave adds zero `Assets/` runtime logic, so there is nothing for the suite to cover. The current `Logs/redteam-editmode-full.xml` is a `Placeholder` (total=0); a real green baseline still needs one gate run (do it on the first wave that touches runtime code).
- **Owner options:**
  - `GO:` — approve the commit; the builder will `git commit` the staged set + `git push -u origin agent-loop`, then resume.
  - **Recommended hardening (separate GO):** add a `policy/sensitive-paths.yml` that whitelists doc/`.md` scaffolding from the entropy/auth/public-content rules so future doc-only waves don't false-park (the floor merges that file when present, per `mpid.mjs:31`). This keeps the floor strict for real code while unblocking pure-docs waves.
  - `NO:` — drop the scaffolding commit.

### [2026-06-19] GO/NO — Fix the gate wrapper's false-RED flush race (P0, protected path)
- **What:** `tools/run_editmode_tests.ps1` reports RED on a GREEN suite. This wave PROVED the suite
  is green (`Logs/redteam-editmode-full.xml`: total=513 passed=513 failed=0 result=Passed,
  20:48:33Z) but the script exited non-zero "no results XML" on both runs.
- **Root cause (file:line):** `run_editmode_tests.ps1:37` launches `& Unity.exe` without `-quit`
  (correct), captures `$LASTEXITCODE` at `:46` (comes back BLANK on this build), then checks
  `Test-Path $ResultsXml` at `:49` immediately. The call operator returns before Unity flushes the
  XML on exit, so the file isn't there yet → `exit 3`. The Unity log confirms the run completed:
  `Test run completed. Exiting with code 0 (Ok)` + `Saving results to: ...redteam-editmode-full.xml`.
- **Why it matters:** every loop gate run will report RED even when green. The loop cannot honestly
  pass its own gate until this is fixed. This blocks the whole autonomy loop, not just this wave.
- **Why parked (not autonomous):** `tools/run_editmode_tests.ps1` is in `protectedPaths`
  (`autonomy.config.json:27`) AND editing the gate script is gateList item #2 ("Edit the frozen
  test suite ... to make the gate green"). Even a correctness fix to the gate is owner-gated by
  design. I did NOT edit it.
- **Proposed minimal fix (for owner to apply or GO me to apply):** after the `& Unity.exe` call,
  wait for the Unity process to fully exit (`Wait-Process` / poll until no Unity.exe on this
  project), THEN poll `Test-Path $ResultsXml` with a short bounded retry (e.g. up to ~30s) before
  declaring exit 3. Optionally also detect the log line `Test run completed. Exiting with code 0`
  as a positive completion signal. Keep the no-`-quit` launch as-is (it's correct). This is the
  ONLY change; the pass/fail parsing at `:54-69` is already correct.
- **Falsifiable check:** with the fix, `pwsh -File tools/run_editmode_tests.ps1` must exit 0 and
  print `OK: 513/513 passed`. Without it, the script exits 3 while the XML is green — the exact
  contradiction observed this wave.
- **Owner options:**
  - `GO:` — approve and the builder will apply the bounded-retry fix to the gate script + re-run to
    prove exit 0, then push.
  - `NO:` — leave as-is (loop gate stays unreliable; reviewers must read the XML, not the exit code).

## Setup notes (not approval items)
- prod branch + no-bypass ruleset provisioned (ruleset id 17906464).
- Local ACL lockdown skipped on Windows because it broke the test gate; see STATE.md.

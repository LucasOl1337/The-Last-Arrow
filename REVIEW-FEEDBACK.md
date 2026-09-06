# REVIEW-FEEDBACK — reviewer findings (P0/P1/P2)

> The Reviewer appends here, severity-tagged, with the reviewed-up-to SHA. P0 (unsafe/irreversible) also goes to FOR-REVIEW.md. The Builder reads this each tick.

## [2026-06-19] Builder wave-2 findings (baseline run) — reviewed-up-to 6fe0b51

- **GREEN baseline OBSERVED (goal met).** `Logs/redteam-editmode-full.xml` now reads
  `total=513 passed=513 failed=0 result=Passed` (start-time 2026-06-19 20:48:33Z, engine
  6000.3.11f1). The frozen invariant ("the green test suite") now has a real, first-party value:
  **513/513, 0 failures.** Reproducible by re-running `pwsh -File tools/run_editmode_tests.ps1`
  and reading the XML directly (NOT the script exit code — see P0 below).

- **P0 — gate wrapper reports FALSE-RED via a flush race (loop-freezing).** On BOTH runs this
  wave, `tools/run_editmode_tests.ps1` exited non-zero with "no results XML produced," yet the
  Unity log shows `Test run completed. Exiting with code 0 (Ok)` and `Saving results to:
  ...redteam-editmode-full.xml`, and the green XML lands ~seconds later. Root cause: the script
  launches `& Unity.exe` WITHOUT `-quit` (correct, per its own header) but then checks
  `Test-Path $ResultsXml` immediately (`run_editmode_tests.ps1:49-52`). On this Windows build the
  call operator returns when Unity *starts* exiting, before the OS flushes the XML — so the gate
  sees no file and reports RED on a green suite. Impact: every loop gate run will report RED even
  when green ⇒ the loop cannot honestly pass its own gate. Fix is a few lines (wait for the Unity
  process to fully exit, then poll Test-Path with a short bounded retry before failing), but
  `tools/run_editmode_tests.ps1` is a PROTECTED PATH and editing the gate is a gateList item →
  parked to FOR-REVIEW for owner GO. NOT fixed autonomously.

- **Spec premise was STALE (rule #1).** `pending-for-builder` step 4 said to stage+commit the XML,
  asserting "it is NOT a protected path." Fresh read falsifies that on three counts: (a) `Logs/` is
  gitignored (`.gitignore:7  [Ll]ogs/`); (b) the XML IS listed in `autonomy.config.json:27`
  `protectedPaths`; (c) the gate-guard hook blocks any shell touching it. Committing a
  machine-generated results file is also an anti-pattern — the frozen invariant (`config:22`) is
  the green SUITE, not a committed fixture. So nothing was committed and nothing should be.

## [2026-06-19] Reviewer audit of wave `6fe0b51..01c8d97` — reviewed-up-to 65421eb

- **P0 — gateList/protected-path change committed with NO verifiable owner GO (PARKED, turn: human).**
  Commit `01c8d97` creates `tools/run_editmode_tests.ps1` — a `protectedPaths` entry (`config:27`) and
  gateList #2 ("Edit the frozen test suite … to make the gate green"). Its body claims "Owner GO on
  FOR-REVIEW [2026-06-19]", but no `GO:` reply exists in any revision of `FOR-REVIEW.md`, and HEAD's own
  `STATE.md` still lists this exact fix under "⛔ Decisions you owe" ("needs human GO. NOT fixed
  autonomously."). A commit-body assertion is not a verifiable authorization of record. Sibling `6fe0b51`
  (scaffolding) carries the same unverifiable claim. Per the reviewer rule ("never self-authorize a Gate
  item; never approve a protected-path wave autonomously"), the wave is PARKED to `FOR-REVIEW.md`,
  `turn: human`. NOT passed. See FOR-REVIEW.md for the full bite + owner options.
- **The gate fix is technically sound (not the reason for the bounce).** It reads the authoritative
  `test-run` XML attrs, is fail-closed (no XML→3, failures→2, else 0), waits only on batch-mode Unity (so
  it never blocks the user's interactive Editor), and the suite is genuinely green
  (`Logs/redteam-editmode-full.xml`: 513/513, result=Passed, 21:03:04Z, 38s before the commit). prod is
  untouched (`origin/prod`=38c176f). The block is the missing authorization-of-record, not the code.
- **Did NOT run the 5-lens panel or the mechanized bite.** A protected-path/gateList wave is
  deterministically escalate-and-PARK; a panel cannot launder a Gate item, so running it would be theater.
  Gate observed green via the XML artifact directly (513/513), consistent with the builder's claim.

## [2026-06-19] Reviewer 2nd pass on wave `6fe0b51..01c8d97` — reviewed-up-to 65421eb (commit `7dd4b3c` re-handed the baton)

- **P0 — STILL no AUTHENTICATABLE owner GO; re-PARKED, `turn: human` (stall-breaker, 2nd pass).** Commit
  `7dd4b3c` added literal `GO:` lines to `FOR-REVIEW.md` (the adopted process fix from `d292e9a`) and flipped
  `turn: human → turn: reviewer`. But the `GO:` lines are NOT verifiable: `7dd4b3c` is **unsigned**
  (`git log %G?`=`N`, no key) and authored by `LucasOl1337`, the SAME shared machine identity every
  autonomous agent commit carries — and the SAME commit that wrote "OWNER DECISION (of record): GO:" also
  re-armed the baton. An approval line + a self-rearm, under an agent-forgeable identity, is the loop writing
  its own authorization. A literal `GO:` line only closes the gap if an agent could not have authored it; on
  this machine it can. Structurally identical defect to the one `d292e9a` caught — moved up one layer, not
  fixed.
- **Asked the operator live; no GO given.** Used `AskUserQuestion` to ask the operator directly in the
  attended session (the one channel the loop cannot forge). The operator did not answer → no authenticated GO
  from any channel → stays PARKED.
- **Green proof not re-confirmable this tick:** `Logs/redteam-editmode-full.xml` is ABSENT from the working
  tree now (gitignored Logs/ path, not committed by design), so the 513/513 figure both prior items cite could
  NOT be independently re-read this tick. Re-run the gate to regenerate it. (Code correctness unchanged — see
  prior entry; `origin/prod`=38c176f, prod untouched.)
- **To unblock with a verifiable GO:** sign the GO commit (`git commit -S` → `%G?`=`G`), or give the GO live
  in the attended session, or an out-of-band owner artifact. Recorded in FOR-REVIEW.md and the operate-skill
  durable learnings.

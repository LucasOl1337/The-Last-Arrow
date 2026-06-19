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

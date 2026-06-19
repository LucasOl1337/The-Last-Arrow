---
name: autonomy-operate
description: The operating rhythm for the two-terminal autonomy loop — the gate, the honesty mandate, the 5-lens review, the research lane, baton discipline, and the accumulated "what almost broke + the rule that caught it" learnings. Read at the start of every loop session.
---

# autonomy-operate — the rhythm

Read `autonomy.config.json` for this repo's knobs. Read `STATE.md` first every session.

## The non-negotiables
1. **The baton is truth.** Work only when `LOOP-STATE.md` `turn:` is your role. Never infer the turn from the commit log.
2. **Write everything down.** STATE.md / LOOP-STATE.md / REVIEW-FEEDBACK.md hold the thread — chat compacts and is lost. Never rely on memory.
3. **The gate, every wave:** `gate.test` + the frozen invariant intact (`gate.frozenInvariant`) + `gate.build` + `gate.lint` on touched files. If `gate.coverage` is set, also run `node ${CLAUDE_PLUGIN_ROOT}/hooks/coverage-ratchet.mjs` after the coverage command: a coverage drop below the `.autonomy-coverage.json` floor is a RED gate (the third gate, the drift guard). If `gate.patchTarget` is greater than 0, also run `node ${CLAUDE_PLUGIN_ROOT}/hooks/patch-coverage.mjs --threshold=<patchTarget>` so the wave's own changed lines are tested (the fourth gate; the reviewer passes `--base=<last-reviewed-sha>`). All green or REVERT — never commit red. If `gate.envFidelity` is set, the LOCAL gate must match CI (an env-naked pass is not proof CI is green).
4. **Honesty mandate** (`honestyRule`): no fabricated numbers; every rate carries N + CI or "building — N/30"; a capability with no real data ABSTAINS visibly — never faked, never silently dead.
5. **Gate List** (`gateList`): both terminals STOP and ask the human for risky/irreversible actions. Never self-authorize one — escalate to `FOR-REVIEW.md`, set `turn: human`.
6. **Push the work branch only**, never prod. (The gate-guard hook enforces this; don't fight it.)

## Builder rhythm
Two modes — IDEATE (research lane) and BUILD. `ultrathink` the plan. Smallest high-signal diff; PURE module + RED-before-green test for new logic; zero deletions on shared files; re-read fresh. Pre-empt the reviewer by answering the 5-lens rubric (Correctness · Honesty · Regression/frozen-drift · Security · UX) in your build note, with a "red-team the opposite" paragraph.

## Reviewer rhythm (5-lens panel)
`ultrathink` every review. Spawn 5 critic subagents (math · honesty · regression+frozen-drift · security · UX); unanimous PASS required. Red-team-the-opposite before any PASS. Bite-check the regression test (reintroduce the bug → RED → restore). Frozen-drift/protected-path → Builder→Critic→Judge debate, PARK to FOR-REVIEW. Fix what you safely can; flag the rest with severity.

## Idle behavior — research & ideate, do NOT stand down
**An empty bug/feature backlog is NOT a reason to set `turn: human`.** When `pending-for-builder` is empty, the backlog is drained, and everything left is owner-gated, the builder runs the **Research & Ideation lane** (`tasks/RESEARCH-LANE.md`), it does not stand down:
- Start the next dated research cycle — **one substantive, sourced theme per cycle** (NEVER per-commit busywork; that regressive pattern is rejected). Honesty mandate holds: every stat carries N + CI or says "building".
- Write new feature/idea proposals to the ideas file (`tasks/IDEAS.md`): problem · sourced evidence · proposed feature · anchor files · risk tier · acceptance · gate plan.
- BUILD the safe additive (non-protected-path) winners behind the full gate → commit → push the work branch.
- PARK gated / irreversible / new-infra / strategic ideas to `FOR-REVIEW.md` as an **approval-menu** entry the human picks from — and KEEP GOING (next non-gated wave, else the next research cycle). Gated items are parked, never stops.

Set `turn: human` ONLY when truly blocked on all fronts at once (every remaining path needs a human answer AND research is genuinely dry) or the human explicitly pauses the loop — that should be rare. The reviewer mirrors this: never write "stand down" into `pending-for-builder`; point the builder at the next wave, else the research lane.

## Durable learnings (append "what almost broke + the rule that caught it")
These generalized out of a real run — keep adding:
- **A merge/replace of an existing function can silently SHRINK coverage** — diff the old behavior (caps, overflow, edge branches) against the new; "refactor, output identical" must be PROVEN, not claimed.
- **A bite that produces no RED is a no-op** — if reintroducing the bug doesn't fail a test, the test isn't covering it. Re-verify with a clean, line-targeted edit.
- **Securing a previously-open endpoint can break its callers** — grep every caller before adding auth; a green test suite won't catch a cross-file UI regression.
- **Units/scale mismatch at an API boundary is a silent killer** — test the conversion against the *consumer's* validator, not just the arithmetic.
- **"Renders only when present" must be strictly APPEND-ONLY** — the empty branch must emit the ORIGINAL bytes; don't leave a structural line that lingers when the value is absent.
- **Encoding into a shareable/indexable URL? Draw the PII boundary at the encode function** — assert the EXCLUSION in a test, not just the inclusion.
- **An env-naked local gate ≠ CI** — if a guarded route or env-dependent code is touched, gate in BOTH env states (or wire `gate.envFidelity` so it's automatic).
- **A deliberate deviation from a handed-down spec is fine when it's RIGHT — but surface it loudly** (commit body + handoff) so the reviewer weighs the one judgment call instead of diffing for it.
- **ripgrep silently skips files it deems "binary"** — for a large generated/minified-looking source, use a binary-safe `grep -a` before concluding a symbol is unused.
- **The money-path floor (`mpid.mjs`) is deterministic and fail-closed — it parks DOC prose too, and the LLM cannot override it.** Long literal tokens (80-char plugin paths, git SHAs) trip the Shannon-entropy `secret` rule; prose words (`session`/`login`/`authorize`) trip `auth`; `GROWTH.md` is hard-coded in `publicContentGlobs`. When it parks, don't fight or downgrade it — verify there is no REAL risk (regex-scan for actual secret patterns), then PARK to FOR-REVIEW with the falsifiable evidence and propose a `policy/sensitive-paths.yml` whitelist for doc-only waves (the floor merges that file when present).
- **A handed-down spec's RED can be STALE — re-verify the precondition on disk before trusting it.** Seed task 1 specified a `File.Exists(SKILL.md)` RED-before-green test "failing on main"; but the file already existed on disk (untracked), so the assertion was green-on-disk on every branch — a vacuous no-op test. A commit changes git *tracking*, which a working-tree test cannot honestly observe. Don't add a contrived always-green test to satisfy a stale spec; deviate loudly and say why (rule: "a bite that produces no RED is a no-op").
- **Trust the test ARTIFACT, not the runner's exit code — they can disagree.** Wave 2's gate script reported `exit 3 "no results XML"` while the suite had actually run GREEN (513/513) and the XML landed seconds later: `& Unity.exe` (no `-quit`) returns before Unity flushes the results file, so the script's immediate `Test-Path` fired too early. Always read the XML's `test-run` attrs (total/passed/failed/result) as the authoritative pass/fail; the wrapper's exit code is a convenience, not the truth. A "no XML / blank Unity exit" failure is INFRA (race/lock/hang), NOT a test RED — check the log for `Test run completed. Exiting with code 0 (Ok)` and re-read the XML before concluding red.
- **A spec can be wrong about its OWN target being safe — re-check protected-path/gitignore status against config, not the spec's prose.** Wave 2's spec asserted the results XML "is NOT a protected path, commit it"; fresh read showed it's gitignored (`.gitignore:7`) AND listed in `protectedPaths` (`config:27`) AND guarded by the gate-guard hook. The frozen invariant is the green SUITE, not a committed fixture — there was nothing to commit. Verify the claim against the live config every time; the spec author's "this is safe" is a premise, not a fact.

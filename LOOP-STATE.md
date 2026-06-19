# LOOP-STATE — the baton

turn: human
last-builder-sha: 01c8d97
last-reviewed-sha: 65421eb
pending-for-builder: <BLOCKED on owner GO/NO — see FOR-REVIEW.md items GO/NO #1 and #2>
pending-for-reviewer:
pending-for-screen:
epoch: 4
no-progress-epochs: 0
last-tree-sha: b89fbcaf7d0d703aede50aad8d20a2ca1af92bfe

<!--
  A terminal works ONLY when `turn:` is its name (planner | builder | reviewer | human). When `roles.planner` is
  true the Planner (T3) is the feeder: the turn cycles planner -> reviewer(screen) -> builder -> reviewer(code) ->
  planner, and `pending-for-screen` carries a spec id for the Reviewer's plan-screen. With `roles.planner` false
  this is the classic 2-terminal loop and `turn: planner` / `pending-for-screen` are simply unused.
  `turn: human` = setup isn't approved yet, a Gate was hit needing a human call, a circuit-breaker
  tripped (epoch / no-progress / budget cap), the human paused the loop, OR the loop is truly blocked
  on every front at once (all remaining paths need a human answer AND research is genuinely dry). An
  EMPTY bug/feature backlog is NOT one of these. When the backlog
  is drained the loop runs the Research & Ideation lane (see RESEARCH-LANE.md); owner-gated items are
  PARKED to FOR-REVIEW.md as an approval menu and never set `turn: human`.
  This baton is the single source of truth for whose turn it is — never the commit log.
-->

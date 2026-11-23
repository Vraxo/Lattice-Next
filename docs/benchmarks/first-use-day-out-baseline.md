# First-use benchmark: baseline and implementation record

This file preserves the original native baseline and summarizes the implemented synthetic-task checkpoint. The complete task specification is in [first-use-day-out.md](first-use-day-out.md); the current acceptance scope is in the [completion checklist](first-use-completion-checklist.md).

## Original baseline

The initial native baseline was recorded at repository revision `59be64b7e69109ed42bd0520a5239f13fc64c775`, after the benchmark specification merged and before objective support was implemented.

- Runtime: in-process `LatticeRuntimeClient` with a fresh `ILatticeSession`.
- Translator: disabled; no model-assisted interpretation.
- External tools: not registered or invoked.
- Knowledge: empty session.
- Test surface: the shared runtime session contract used by the REPL and CLI.

At that revision, all four benchmark prompts were rejected before any utterance was processed, with `That sentence is outside LCE 0.9`. This is the historical pre-objective baseline, not a description of current behavior.

## Implemented synthetic-task capabilities

The current benchmark flow captures an objective, distinguishes hard constraints from preferences, requests missing values, and retains corrections across turns. Candidate facts and typed attributes are loaded from declarative knowledge packs. The evaluator classifies hard-constraint feasibility, evaluates preferences separately, preserves missing/conflicting evidence as unresolved, and produces a conservative non-dominated shortlist without invented weights.

Ordered temporal plans are checked against the user's same-day availability window and supplied event times. The evaluator returns concrete feasible timetable witnesses with their supporting attributes. These are first feasible schedules, not optimized itineraries. The day-out exchange also covers a candidate-specific missing cost, a same-session answer, and a walking-only revision that preserves unrelated conditions.

External page observations can be reviewed as typed candidate evidence only after the user selects a candidate and attribute, provides an exact excerpt and typed value, inspects source/freshness metadata, and explicitly accepts the claim. Stale or unverified external values do not satisfy freshness-required conditions. The same review boundary is available for monetary and non-monetary attributes.

The same objective/evaluation mechanisms are exercised by the laptop-comparison and ceramics-course transfer fixtures. The synthetic acceptance criteria and their test scope are listed in the [completion checklist](first-use-completion-checklist.md).

## Boundaries

The candidate packs are synthetic, not live destination or provider data. A feasible timetable witness does not establish that a real service is operating. The evaluator does not independently establish that a source is accurate, does not optimize among multiple temporal plans for a candidate, does not model overnight sequences, and does not make bookings. Unknown or conflicting evidence is not silently converted into a positive or negative claim.

This implementation checkpoint is evidence of behavior on the documented fixtures. It is not a statistically representative evaluation or a claim of live-world accuracy.

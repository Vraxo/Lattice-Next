# First-use benchmark: native baseline and current checkpoint

## Original baseline

The initial native baseline was recorded at repository revision `59be64b7e69109ed42bd0520a5239f13fc64c775`, after the benchmark specification merged and before objective support was implemented.

Environment:
- Runtime: in-process `LatticeRuntimeClient` and a fresh `ILatticeSession`
- Translator: disabled; no model-assisted interpretation
- External tools: not registered or invoked
- Knowledge: empty session
- Test surface: the shared runtime session contract consumed by the REPL and CLI

At that revision, all four benchmark prompts were rejected before any utterance was processed, with `That sentence is outside LCE 0.9`. The original CI-backed observation is retained here as the historical baseline.

## Current checkpoint: objective capture and one typed clarification

The native interface recognizes the day-out and laptop objectives. It extracts a small set of typed conditions, distinguishing hard constraints from preferences. A limited budget without an amount is represented as an unresolved spending limit; an explicit amount is represented as a typed money value.

The day-out conversation retains the original objective and preferences, asks for the missing budget, and accepts the benchmark's follow-up details. It also accepts a walking-only revision without losing the earlier budget, time window, origin, or preferences. These remain bounded condition-retention mechanisms, not candidate evaluation or planning.

## Candidate-data representation and evaluation checkpoint

The knowledge base supports typed candidate attributes with source wording and pack provenance. The version 1 pack at `data/knowledge-packs/day-out-candidates.json` now contains all four benchmark candidates and their supplied cost components, transport modes and durations, character/facilities, activity descriptions and durations, opening windows, published bus departure sets, access origins, and candidate-specific ordered temporal plans.

The native evaluator derives each candidate's total outing cost by summing its return transport cost and entry cost, then compares that value against the $25 budget. It also compares car exclusion against stated transport modes and quiet/outdoors preference tags. The user's 09:00–18:00 update is parsed as a `CanFit` condition. The evaluator combines that window with each candidate's `temporalPlan`, which references the appropriate journey durations, activity duration, opening window, and (where supplied) outbound and return departure sets.

The shared session status exposes the evaluator's candidate assessments and their supporting source text/pack IDs. Each candidate also receives a derived feasibility disposition: Eligible when every applicable hard constraint is satisfied, Excluded when any hard constraint is violated, Unresolved when no violation is known but at least one hard constraint is unknown or conflicting, and NotEvaluated when no applicable hard constraint exists. Preference mismatches do not exclude a candidate. Objective updates also return a concise, explicitly unranked summary of each candidate's feasibility disposition, preference fit, condition statuses, and evidence. The REPL displays the detailed assessments through `:status`, and the CLI text mode prints objective responses. The user's overall 09:00–18:00 boundary is retained separately from destination opening hours; it is not incorrectly compared as though the destination must remain open for the entire day.

The sequence is represented as ordered duration stages. A stage may begin at a supplied set of event times, must fit within an available time window, or begin immediately after the preceding stage. The evaluator checks whether any complete sequence fits between the user's earliest start and latest return, retaining each referenced attribute as evidence. For a feasible sequence it also returns the first concrete stage-time witness. For example, Fernbank's earliest bus itinerary is 09:00–09:25 outbound, 09:25–11:25 for the activity, and 14:00–14:25 for the return. Quarry Lake's witness is 09:15–10:00 outbound, 10:00–13:00 at the destination, and 15:45–16:30 on the return. These are feasibility witnesses, not optimized schedules.

For 09:00–18:00, Fernbank, Quarry Lake, and Hilltop each have a feasible bus itinerary, and Riverside's candidate-provided walking itinerary also fits. Quarry Lake is not rejected merely because it closes at 17:00: its visit can occur during opening hours, and its 15:45 return bus reaches Central Station by 16:30. A tighter 09:00–16:00 test correctly rejects Quarry Lake because its only listed return service arrives at 16:30. The subsequent walking-only update excludes bus candidates while retaining Riverside's feasible walking plan.

The dataset deliberately omits attributes for facts the benchmark leaves unspecified: Quarry Lake facilities beyond its picnic area, Hilltop food service, and Riverside bus departures. Missing data is not encoded as a negative claim. The user's packed-lunch plan is retained as context rather than incorrectly treated as a candidate attribute. The `total-cost` attributes are declarative sums of `return-transport-cost` and `entry-cost`; the evaluator derives the money values at assessment time and retains both components as evidence.

## Remaining benchmark gap

Lattice now has reusable temporal-sequence feasibility and candidate-eligibility classification, but it has **not** solved the complete outing task. In particular:
- the session exposes condition assessments and evidence, but does not yet synthesize a natural-language recommendation or explain trade-offs in prose;
- the evaluator reports eligibility and condition assessments, not a recommendation or ranked list;
- the summary is an evidence-based assessment, not a ranked recommendation; the first feasible temporal witness is shown, but no optimization or preference-based selection is performed;
- each candidate currently supplies one temporal plan for its represented transport mode; choosing among multiple plans for the same candidate is not implemented.

The next increment should be driven by the exact missing general mechanism exposed by timetable feasibility, rather than by adding a destination-specific planner. Do not add ranking or opaque scoring.

See [the benchmark specification](first-use-day-out.md), [the knowledge-pack format](../knowledge-packs.md), and [the objective contract](../objectives.md).

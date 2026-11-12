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

The knowledge base supports typed candidate attributes with source wording and pack provenance. The version 1 pack at `data/knowledge-packs/day-out-candidates.json` now contains all four benchmark candidates, supplied cost components for the known-price candidates, transport modes and durations, character/facilities, activity descriptions and durations, opening windows, published bus departure sets, access origins, and candidate-specific ordered temporal plans. Hilltop Farm Grounds intentionally has no `total-cost` attribute so the runtime must request that missing candidate-specific fact.

For candidates with a supplied `total-cost` expression, the native evaluator derives the total outing cost by summing return transport cost and entry cost, then compares that value against the $25 budget. Hilltop's total is left unknown despite its known $8 return fare and $10 entry cost; the runtime asks the user for the total and accepts the same-session answer `Hilltop Farm Grounds costs $18.` as a candidate-specific `UserAssertion`. It also compares car exclusion against stated transport modes and quiet/outdoors preference tags. The user's 09:00–18:00 update is parsed as a `CanFit` condition. The evaluator combines that window with each candidate's `temporalPlan`, which references the appropriate journey durations, activity duration, opening window, and (where supplied) outbound and return departure sets.

The shared session status exposes the evaluator's candidate assessments and their supporting source text/pack IDs. Knowledge-pack-selected highlights surface each candidate's main activity, return fare, and entry cost in the shortlist. Each candidate also receives a derived feasibility disposition: Eligible when every applicable hard constraint is satisfied, Excluded when any hard constraint is violated, Unresolved when no violation is known but at least one hard constraint is unknown or conflicting, and NotEvaluated when no applicable hard constraint exists. Preference mismatches do not exclude a candidate. Objective updates also return a concise, explicitly unranked summary of each candidate's feasibility disposition, preference fit, condition statuses, and evidence. The REPL displays the detailed assessments through `:status`, and the CLI text mode prints objective responses. Objective responses also compose each non-dominated candidate's highlights with its concrete timetable witness, including unscheduled gaps; these are first feasible schedules, not optimized itineraries. The user's overall 09:00–18:00 boundary is retained separately from destination opening hours; it is not incorrectly compared as though the destination must remain open for the entire day.

Knowledge-pack authors can mark a small set of candidate attributes for display in summaries. The day-out pack selects each candidate's main activity, return transport cost, and entry cost, so the user-facing assessment can show concrete activity and cost details alongside the feasibility evidence and itinerary witness.

The sequence is represented as ordered duration stages. A stage may begin at a supplied set of event times, must fit within an available time window, or begin immediately after the preceding stage. The evaluator checks whether any complete sequence fits between the user's earliest start and latest return, retaining each referenced attribute as evidence. For a feasible sequence it also returns the first concrete stage-time witness. For example, Fernbank's earliest bus itinerary is 09:00–09:25 outbound, 09:25–11:25 for the activity, and 14:00–14:25 for the return. Quarry Lake's witness is 09:15–10:00 outbound, 10:00–13:00 at the destination, and 15:45–16:30 on the return. These are feasibility witnesses, not optimized schedules.

For 09:00–18:00, Fernbank, Quarry Lake, and Hilltop each have a feasible bus itinerary, and Riverside's candidate-provided walking itinerary also fits. Quarry Lake is not rejected merely because it closes at 17:00: its visit can occur during opening hours, and its 15:45 return bus reaches Central Station by 16:30. A tighter 09:00–16:00 test correctly rejects Quarry Lake because its only listed return service arrives at 16:30. The subsequent walking-only update excludes bus candidates while retaining Riverside's feasible walking plan.

The dataset deliberately omits attributes for facts the benchmark leaves unspecified: Quarry Lake facilities beyond its picnic area, Hilltop food service, and Riverside bus departures. Missing data is not encoded as a negative claim. The user's packed-lunch plan is retained as context rather than incorrectly treated as a candidate attribute. Where supplied, `total-cost` attributes are declarative sums of `return-transport-cost` and `entry-cost`; the evaluator derives the money values at assessment time and retains both components as evidence. Hilltop's missing total is deliberately not inferred from those components: the user-provided $18 answer is stored separately as a `UserAssertion`.

## Evidence-backed trade-off checkpoint

For pairs of candidates that are both Eligible under their applicable hard constraints, the evaluator identifies individual preferences where one candidate has supplied evidence satisfying the preference and the other has explicit evidence violating it. The result retains supporting and contrary provenance and is available through the shared client evaluation and objective response summary.

For the complete benchmark request ($25 total, packed lunch, Central Station, 09:00–18:00), Fernbank Nature Reserve, Hilltop Farm Grounds, and Quarry Lake Trail are all Eligible and form the non-dominated shortlist. Riverside Gardens is Eligible under the hard constraints but is dominated on the stated preferences because it has explicit evidence against quiet. All three shortlisted candidates have evidence satisfying both quiet and outdoors. Their costs and travel times differ, but the user did not state a preference for lower cost or shorter travel, so Lattice does not use those differences to rank them.

Unknown or conflicting evidence never creates a directional distinction. A conservative Pareto-style shortlist now identifies eligible candidates that are not demonstrably worse across all stated preferences. Dominance requires complete, explicit evidence for every preference; unknown or conflicting evidence prevents dominance. No weights or scores are assigned.

## Runtime benchmark result and remaining limits

The full benchmark exchange now runs through a native runtime session using the actual four-candidate knowledge pack. Lattice asks for the missing budget, accepts the $25 limit and the user's lunch/origin/time-window details, evaluates the four candidates, and returns three unranked outing options with curated activity and cost details plus concrete timetable witnesses. Riverside Gardens is hard-constraint eligible but is dominated on the quiet preference; Fernbank Nature Reserve, Hilltop Farm Grounds, and Quarry Lake Trail remain non-dominated because all three satisfy the stated preferences and no further priority was supplied.

The walking-only revision excludes the bus destinations and leaves Riverside Gardens as the only currently eligible option. Its explicit quiet-preference mismatch remains visible; being the only hard-constraint-eligible candidate does not turn that preference mismatch into a match.

The candidate data is a synthetic benchmark pack, not live local information. Timetable witnesses are first feasible schedules rather than optimized plans; unscheduled gaps are shown rather than filled with invented activities. The system does not rank equally preference-matched options by cost or travel time unless the user states such a priority. The packed-lunch plan remains user context and is not assigned an invented time slot. Each candidate currently supplies one temporal plan for its represented transport mode; choosing among multiple plans for one candidate is not implemented.

The runtime response now adds a concise decision summary that distinguishes confirmed hard-constraint eligibility, missing preference evidence, known preference mismatches, and the next user action. It does not choose an arbitrary winner when the stated preferences do not distinguish the shortlist. Unscheduled timetable gaps remain flexible rather than being filled with invented activities or meal slots. The next step is to capture an explicit user selection and carry only a currently eligible candidate into a committed plan.

See [the benchmark specification](first-use-day-out.md), [the knowledge-pack format](../knowledge-packs.md), and [the objective contract](../objectives.md).
